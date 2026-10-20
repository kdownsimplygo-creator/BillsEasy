using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class VoucherRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly FinancialYearRepository _financialYearRepository;
    private readonly AccountGroupRepository _accountGroupRepository;
    private readonly LedgerRepository _ledgerRepository;
    private readonly VoucherRepository _voucherRepository;

    public VoucherRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_VchTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _financialYearRepository = new FinancialYearRepository(_connectionFactory);
        _accountGroupRepository = new AccountGroupRepository(_connectionFactory);
        _ledgerRepository = new LedgerRepository(_connectionFactory);
        _voucherRepository = new VoucherRepository(_connectionFactory);
    }

    private async Task<(Company Company, FinancialYear FY, Ledger Cash, Ledger Sales)> SeedAccountingFoundationAsync()
    {
        var company = Company.Create(
            "Voucher Test Co",
            "Voucher Test Co",
            null,
            "Gujarat",
            "24",
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 1));
        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "tester";
        await _companyRepository.AddAsync(company);

        var fy = company.FinancialYears.First();

        var assetGroup = AccountGroup.Create(company.Id, "Cash & Bank", GroupNature.Asset, isPrimary: true);
        assetGroup.CreatedAt = DateTime.UtcNow;
        assetGroup.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(assetGroup);

        var incomeGroup = AccountGroup.Create(company.Id, "Revenue", GroupNature.Income, isPrimary: true);
        incomeGroup.CreatedAt = DateTime.UtcNow;
        incomeGroup.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(incomeGroup);

        var cash = Ledger.Create(company.Id, assetGroup.Id, "Main Cash A/c");
        cash.CreatedAt = DateTime.UtcNow;
        cash.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(cash);

        var sales = Ledger.Create(company.Id, incomeGroup.Id, "Counter Sales A/c");
        sales.CreatedAt = DateTime.UtcNow;
        sales.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(sales);

        return (company, fy, cash, sales);
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_PersistsAndRetrievesVoucherWithOrderedLines()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var voucher = Voucher.Create(
            company.Id,
            fy.Id,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 1),
            "REF-001",
            "Cash Sale Receipt");

        voucher.AddLine(cash.Id, DebitCreditType.Debit, new Money(5000.75m), "Cash received");
        voucher.AddLine(sales.Id, DebitCreditType.Credit, new Money(5000.75m), "Sales booked");
        voucher.Post(fy, "admin", new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));

        await _voucherRepository.AddAsync(voucher);

        var retrieved = await _voucherRepository.GetByIdAsync(company.Id, voucher.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(voucher.Id);
        retrieved.CompanyId.Should().Be(company.Id);
        retrieved.FinancialYearId.Should().Be(fy.Id);
        retrieved.VoucherNumber.Should().Be("RC-001");
        retrieved.Type.Should().Be(VoucherType.Receipt);
        retrieved.Status.Should().Be(VoucherStatus.Posted);
        retrieved.VoucherDate.Should().Be(new DateOnly(2026, 5, 1));
        retrieved.TotalAmount.Amount.Should().Be(5000.75m);
        retrieved.ReferenceNumber.Should().Be("REF-001");
        retrieved.Narration.Should().Be("Cash Sale Receipt");
        retrieved.PostedBy.Should().Be("admin");

        retrieved.Lines.Should().HaveCount(2);
        retrieved.Lines[0].SequenceNumber.Should().Be(1);
        retrieved.Lines[0].LedgerId.Should().Be(cash.Id);
        retrieved.Lines[0].Type.Should().Be(DebitCreditType.Debit);
        retrieved.Lines[0].Amount.Amount.Should().Be(5000.75m);

        retrieved.Lines[1].SequenceNumber.Should().Be(2);
        retrieved.Lines[1].LedgerId.Should().Be(sales.Id);
        retrieved.Lines[1].Type.Should().Be(DebitCreditType.Credit);
        retrieved.Lines[1].Amount.Amount.Should().Be(5000.75m);
    }

    [Fact]
    public async Task AddAsync_WhenDuplicateNumberInSameFY_ThrowsSqliteException()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var voucher1 = Voucher.Create(company.Id, fy.Id, "RC-DUP", VoucherType.Receipt, new DateOnly(2026, 5, 1));
        voucher1.AddLine(cash.Id, DebitCreditType.Debit, new Money(100m));
        voucher1.AddLine(sales.Id, DebitCreditType.Credit, new Money(100m));
        voucher1.Post(fy);
        await _voucherRepository.AddAsync(voucher1);

        var voucher2 = Voucher.Create(company.Id, fy.Id, "rc-dup", VoucherType.Receipt, new DateOnly(2026, 5, 2));
        voucher2.AddLine(cash.Id, DebitCreditType.Debit, new Money(200m));
        voucher2.AddLine(sales.Id, DebitCreditType.Credit, new Money(200m));
        voucher2.Post(fy);

        var act = async () => await _voucherRepository.AddAsync(voucher2);
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*UNIQUE constraint failed*");
    }

    [Fact]
    public async Task AddAsync_WhenFailsPartway_RollsBackTransactionEntirely()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var voucher = Voucher.Create(company.Id, fy.Id, "ROLLBACK-001", VoucherType.Receipt, new DateOnly(2026, 5, 1));
        voucher.AddLine(cash.Id, DebitCreditType.Debit, new Money(100m));
        // Intentionally reference non-existent ledger to trigger FK failure on second line
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(100m));

        var act = async () => await _voucherRepository.AddAsync(voucher);
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");

        // Verify that NO voucher header was committed
        var retrieved = await _voucherRepository.GetByIdAsync(company.Id, voucher.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_WithFilters_FiltersCorrectly()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var v1 = Voucher.Create(company.Id, fy.Id, "V-001", VoucherType.Receipt, new DateOnly(2026, 5, 1));
        v1.AddLine(cash.Id, DebitCreditType.Debit, new Money(100m));
        v1.AddLine(sales.Id, DebitCreditType.Credit, new Money(100m));
        v1.Post(fy);
        await _voucherRepository.AddAsync(v1);

        var v2 = Voucher.Create(company.Id, fy.Id, "V-002", VoucherType.Payment, new DateOnly(2026, 6, 1));
        v2.AddLine(sales.Id, DebitCreditType.Debit, new Money(200m));
        v2.AddLine(cash.Id, DebitCreditType.Credit, new Money(200m));
        v2.Post(fy);
        await _voucherRepository.AddAsync(v2);

        var receipts = await _voucherRepository.GetAllAsync(company.Id, type: VoucherType.Receipt);
        receipts.Should().HaveCount(1);
        receipts[0].VoucherNumber.Should().Be("V-001");

        var dateFiltered = await _voucherRepository.GetAllAsync(company.Id, fromDate: new DateOnly(2026, 5, 15));
        dateFiltered.Should().HaveCount(1);
        dateFiltered[0].VoucherNumber.Should().Be("V-002");
    }

    [Fact]
    public async Task DeleteAsync_CascadesToVoucherLines()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var voucher = Voucher.Create(company.Id, fy.Id, "DEL-001", VoucherType.Receipt, new DateOnly(2026, 5, 1));
        voucher.AddLine(cash.Id, DebitCreditType.Debit, new Money(100m));
        voucher.AddLine(sales.Id, DebitCreditType.Credit, new Money(100m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        await _voucherRepository.DeleteAsync(company.Id, voucher.Id);

        var retrieved = await _voucherRepository.GetByIdAsync(company.Id, voucher.Id);
        retrieved.Should().BeNull();

        // Verify lines are cascade deleted
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM VoucherLines WHERE VoucherId = @VoucherId;";
        cmd.Parameters.AddWithValue("@VoucherId", voucher.Id.ToString());
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task ADR10_DeleteCompany_WhenVoucherExists_FailsWithRestrictConstraint()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var voucher = Voucher.Create(company.Id, fy.Id, "RESTRICT-CO", VoucherType.Receipt, new DateOnly(2026, 5, 1));
        voucher.AddLine(cash.Id, DebitCreditType.Debit, new Money(100m));
        voucher.AddLine(sales.Id, DebitCreditType.Credit, new Money(100m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        // Attempting direct SQL delete of Company -> RESTRICT FK blocks it
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();
        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Companies WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", company.Id.ToString());
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19);
    }

    [Fact]
    public async Task ADR10_DeleteFinancialYear_WhenVoucherExists_FailsWithRestrictConstraint()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var voucher = Voucher.Create(company.Id, fy.Id, "RESTRICT-FY", VoucherType.Receipt, new DateOnly(2026, 5, 1));
        voucher.AddLine(cash.Id, DebitCreditType.Debit, new Money(100m));
        voucher.AddLine(sales.Id, DebitCreditType.Credit, new Money(100m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        // Attempting direct SQL delete of FinancialYear -> RESTRICT FK blocks it
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();
        var act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM FinancialYears WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", fy.Id.ToString());
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19);
    }

    [Fact]
    public async Task ADR10_DeleteLedger_WhenReferencedInVoucherLine_FailsWithRestrictConstraint()
    {
        await _initializer.InitializeAsync();
        var (company, fy, cash, sales) = await SeedAccountingFoundationAsync();

        var voucher = Voucher.Create(company.Id, fy.Id, "RESTRICT-LDG", VoucherType.Receipt, new DateOnly(2026, 5, 1));
        voucher.AddLine(cash.Id, DebitCreditType.Debit, new Money(100m));
        voucher.AddLine(sales.Id, DebitCreditType.Credit, new Money(100m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        // Attempting delete of referenced Cash ledger -> RESTRICT FK blocks it
        var act = async () => await _ledgerRepository.DeleteAsync(company.Id, cash.Id);

        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch { }
    }
}
