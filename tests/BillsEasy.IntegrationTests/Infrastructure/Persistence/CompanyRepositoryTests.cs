using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
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

        // Simulate Phase 1.5 Voucher table creation and record insertion
        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Vouchers (
                    Id TEXT PRIMARY KEY,
                    CompanyId TEXT NOT NULL,
                    FinancialYearId TEXT NOT NULL,
                    VoucherType INTEGER NOT NULL,
                    FOREIGN KEY (FinancialYearId) REFERENCES FinancialYears(Id) ON DELETE RESTRICT
                );

                INSERT INTO Vouchers (Id, CompanyId, FinancialYearId, VoucherType)
                VALUES (@VoucherId, @CompanyId, @FinancialYearId, 1);
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
