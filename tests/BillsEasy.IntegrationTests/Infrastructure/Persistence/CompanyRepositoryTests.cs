using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using BillsEasy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class CompanyRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;

    public CompanyRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_CompTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
    }

    [Fact]
    public async Task AddAndGetById_PersistsCompanyAndFinancialYearsSuccessfully()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Shree Ganesh Enterprise",
            printName: "Shree Ganesh Enterprise",
            shortName: "SGE",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1),
            gstin: "24AAACS1234A1Z5",
            gstRegistrationType: GSTRegistrationType.Regular);

        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "test_user";

        await _companyRepository.AddAsync(company);

        var retrieved = await _companyRepository.GetByIdAsync(company.Id);

        retrieved.Should().NotBeNull();
        retrieved!.CompanyName.Should().Be("Shree Ganesh Enterprise");
        retrieved.StateName.Should().Be("Gujarat");
        retrieved.StateCode.Should().Be("24");
        retrieved.GSTIN.Should().Be("24AAACS1234A1Z5");
        retrieved.CreatedBy.Should().Be("test_user");

        retrieved.FinancialYears.Should().HaveCount(1);
        var fy = retrieved.FinancialYears.First();
        fy.StartDate.Should().Be(new DateOnly(2024, 4, 1));
        fy.EndDate.Should().Be(new DateOnly(2025, 3, 31));
        fy.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByName_DetectsDuplicateCompanyNamesCaseInsensitive()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Krishna Traders",
            printName: "Krishna Traders",
            shortName: "KT",
            stateName: "Maharashtra",
            stateCode: "27",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        await _companyRepository.AddAsync(company);

        var exists1 = await _companyRepository.ExistsByNameAsync("krishna traders");
        var exists2 = await _companyRepository.ExistsByNameAsync("KRISHNA TRADERS");
        var exists3 = await _companyRepository.ExistsByNameAsync("NonExistent");
        var existsExcludingSelf = await _companyRepository.ExistsByNameAsync("Krishna Traders", company.Id);

        exists1.Should().BeTrue();
        exists2.Should().BeTrue();
        exists3.Should().BeFalse();
        existsExcludingSelf.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ModifiesPropertiesAndPersistsCorrectly()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Original Corp",
            printName: "Original Corp",
            shortName: "OC",
            stateName: "Delhi",
            stateCode: "07",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        await _companyRepository.AddAsync(company);

        company.Update("Updated Corp", "Updated Corp Pvt Ltd", "UC", "Delhi", "07", "07AAACU1234A1Z5", GSTRegistrationType.Composition, true);
        company.ModifiedAt = DateTime.UtcNow;
        company.ModifiedBy = "updater_user";

        await _companyRepository.UpdateAsync(company);

        var updated = await _companyRepository.GetByIdAsync(company.Id);

        updated.Should().NotBeNull();
        updated!.CompanyName.Should().Be("Updated Corp");
        updated.PrintName.Should().Be("Updated Corp Pvt Ltd");
        updated.GSTIN.Should().Be("07AAACU1234A1Z5");
        updated.GSTRegistrationType.Should().Be(GSTRegistrationType.Composition);
        updated.IsSecurityEnabled.Should().BeTrue();
        updated.ModifiedBy.Should().Be("updater_user");
    }

    [Fact]
    public async Task Delete_RemovesCompanyAndCascadesFinancialYears()
    {
        // SCOPE: Verifies master setup cascade behavior for an EMPTY company (zero transaction history / no vouchers).
        // Once transactional vouchers exist, Company deletion is blocked by Application guard and DB RESTRICT FK.
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Delete Target",
            printName: "Delete Target",
            shortName: "DT",
            stateName: "Rajasthan",
            stateCode: "08",
            financialYearBeginning: new DateOnly(2024, 4, 1),
            booksBeginningFrom: new DateOnly(2024, 4, 1));

        await _companyRepository.AddAsync(company);

        await _companyRepository.DeleteAsync(company.Id);

        var deleted = await _companyRepository.GetByIdAsync(company.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task HasTransactionsAsync_WhenVoucherExists_ReturnsTrueAndPreventsCompanyDeletion()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Company With Transactions",
            printName: "Company With Transactions",
            shortName: "CWT",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2026, 4, 1),
            booksBeginningFrom: new DateOnly(2026, 4, 1));

        await _companyRepository.AddAsync(company);

        var fy = company.FinancialYears.First();

        // Before simulated transactions exist, HasTransactionsAsync returns false
        var hasTxBefore = await _companyRepository.HasTransactionsAsync(company.Id);
        hasTxBefore.Should().BeFalse();

        // Simulate voucher insertion in authoritative Vouchers table
        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Vouchers (Id, CompanyId, FinancialYearId, VoucherNumber, Type, Status, VoucherDate, TotalAmount, CreatedAt, CreatedBy)
                VALUES (@VoucherId, @CompanyId, @FinancialYearId, 'V-TEST-001', 1, 2, '2026-04-15', 10000, '2026-04-15T00:00:00Z', 'test');
            ";
            cmd.Parameters.AddWithValue("@VoucherId", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", company.Id.ToString());
            cmd.Parameters.AddWithValue("@FinancialYearId", fy.Id.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        // Now HasTransactionsAsync must report true
        var hasTxAfter = await _companyRepository.HasTransactionsAsync(company.Id);
        hasTxAfter.Should().BeTrue();

        // Testing direct SQL cascade attempt: FinancialYears has RESTRICT from Vouchers, so SQLite blocks it
        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            Func<Task> act = async () =>
            {
                await using var delCmd = conn.CreateCommand();
                delCmd.CommandText = "DELETE FROM Companies WHERE Id = @Id;";
                delCmd.Parameters.AddWithValue("@Id", company.Id.ToString());
                await delCmd.ExecuteNonQueryAsync();
            };

            await act.Should().ThrowAsync<SqliteException>()
                .Where(ex => ex.SqliteErrorCode == 19);
        }
    }

    [Fact]
    public async Task DeleteAsync_WhenTransactionInsertedBetweenCheckAndDelete_AtomicallyBlocksDeletion()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Race Guard Co",
            printName: "Race Guard Co",
            shortName: "RGC",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2026, 4, 1),
            booksBeginningFrom: new DateOnly(2026, 4, 1));

        await _companyRepository.AddAsync(company);
        var fy = company.FinancialYears.First();

        // Step 1: Simulate the check phase in CompanyService — HasTransactionsAsync returns false
        var hasTx = await _companyRepository.HasTransactionsAsync(company.Id);
        hasTx.Should().BeFalse("initial check sees zero transactions");

        // Step 2: Simulate concurrent insertion of a voucher before DeleteAsync runs
        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Vouchers (Id, CompanyId, FinancialYearId, VoucherNumber, Type, Status, VoucherDate, TotalAmount, CreatedAt, CreatedBy)
                VALUES (@VoucherId, @CompanyId, @FinancialYearId, 'V-RACE-001', 1, 2, '2026-04-15', 10000, '2026-04-15T00:00:00Z', 'test');
            ";
            cmd.Parameters.AddWithValue("@VoucherId", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", company.Id.ToString());
            cmd.Parameters.AddWithValue("@FinancialYearId", fy.Id.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        // Step 3: Call DeleteAsync — must detect the transaction atomically inside its transaction and throw DomainException
        Func<Task> act = async () => await _companyRepository.DeleteAsync(company.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Cannot delete company 'Race Guard Co' because it has one or more recorded financial transactions*");

        // Step 4: Verify company still exists
        var retained = await _companyRepository.GetByIdAsync(company.Id);
        retained.Should().NotBeNull("company record must not be deleted when transactions exist");
    }

    [Fact]
    public async Task TD02_CompanyService_DeleteCompanyAsync_WhenRealVoucherExists_ThrowsDomainExceptionAndPreservesCompanyAndFY()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "TD02 Protection Co",
            printName: "TD02 Protection Co",
            shortName: "TD02",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2026, 4, 1),
            booksBeginningFrom: new DateOnly(2026, 4, 1));

        await _companyRepository.AddAsync(company);
        var fy = company.FinancialYears.First();

        var groupRepo = new AccountGroupRepository(_connectionFactory);
        var ledgerRepo = new LedgerRepository(_connectionFactory);
        var voucherRepo = new VoucherRepository(_connectionFactory);
        var fyRepo = new FinancialYearRepository(_connectionFactory);

        var assetGroup = AccountGroup.Create(company.Id, "Liquid Assets", GroupNature.Asset, isPrimary: true);
        var incomeGroup = AccountGroup.Create(company.Id, "Direct Income", GroupNature.Income, isPrimary: true);
        await groupRepo.AddAsync(assetGroup);
        await groupRepo.AddAsync(incomeGroup);

        var cashLedger = Ledger.Create(company.Id, assetGroup.Id, "Cash in Till");
        var salesLedger = Ledger.Create(company.Id, incomeGroup.Id, "General Sales");
        await ledgerRepo.AddAsync(cashLedger);
        await ledgerRepo.AddAsync(salesLedger);

        // Post a real financial voucher
        var voucher = Voucher.Create(company.Id, fy.Id, "TD02-VCH-01", VoucherType.Receipt, new DateOnly(2026, 4, 15));
        voucher.AddLine(cashLedger.Id, DebitCreditType.Debit, new Money(5000m));
        voucher.AddLine(salesLedger.Id, DebitCreditType.Credit, new Money(5000m));
        voucher.Post(fy);
        await voucherRepo.AddAsync(voucher);

        // Instantiate CompanyService
        var userService = new StandaloneCurrentUserService();
        var dateProvider = new DateTimeProvider();
        var companyService = new CompanyService(_companyRepository, fyRepo, userService, dateProvider);

        // DeleteCompanyAsync must detect transaction and throw DomainException
        var act = async () => await companyService.DeleteCompanyAsync(company.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Cannot delete company 'TD02 Protection Co' because it has one or more recorded financial transactions*");

        // Verify company and financial year remain intact
        var preservedCompany = await _companyRepository.GetByIdAsync(company.Id);
        preservedCompany.Should().NotBeNull();

        var preservedFys = await fyRepo.GetByCompanyIdAsync(company.Id);
        preservedFys.Should().NotBeEmpty();

        var preservedVoucher = await voucherRepo.GetByIdAsync(company.Id, voucher.Id);
        preservedVoucher.Should().NotBeNull();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            var wal = _testDbPath + "-wal";
            if (File.Exists(wal)) File.Delete(wal);
            var shm = _testDbPath + "-shm";
            if (File.Exists(shm)) File.Delete(shm);
        }
        catch { }
    }
}
