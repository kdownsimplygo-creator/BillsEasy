using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class DatabaseConstraintIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly LedgerRepository _ledgerRepository;

    public DatabaseConstraintIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_DbConstraints_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _ledgerRepository = new LedgerRepository(_connectionFactory);
    }

    [Fact]
    public async Task Companies_CaseInsensitiveNameUniqueness_DatabaseRejectsDuplicate()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        // 1. Insert "ABC Traders"
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@Id, 'ABC Traders', 'ABC Traders', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Insert "abc traders" (case-insensitive collision)
        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@Id, 'abc traders', 'abc traders', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // 19 = SQLITE_CONSTRAINT (UNIQUE constraint failed: Companies.CompanyName)
    }

    [Fact]
    public async Task FinancialYears_MultipleActiveYears_DatabaseRejectsSecondActiveYear()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        var companyId = Guid.NewGuid().ToString();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Test Co', 'Test Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert first active FY
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO FinancialYears (Id, CompanyId, YearName, StartDate, EndDate, IsActive, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, 'FY 2026-27', '2026-04-01', '2027-03-31', 1, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert second active FY for the same company outside the transactional service path
        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FinancialYears (Id, CompanyId, YearName, StartDate, EndDate, IsActive, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, 'FY 2027-28', '2027-04-01', '2028-03-31', 1, '2027-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // UNIQUE constraint failed on partial index UQ_FinancialYears_Company_OneActive
    }

    [Fact]
    public async Task AccountGroups_NatureEnum_CheckConstraint_RejectsOutOfRangeValue()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        var companyId = Guid.NewGuid().ToString();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Group Test Co', 'Group Test Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        }

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AccountGroups (Id, CompanyId, Name, Nature, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, 'Invalid Group', 99, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: Nature BETWEEN 1 AND 5
    }

    [Fact]
    public async Task Ledgers_OpeningBalanceTypeEnum_CheckConstraint_RejectsOutOfRangeValue()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        var companyId = Guid.NewGuid().ToString();
        var groupId = Guid.NewGuid().ToString();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Ledger Test Co', 'Ledger Test Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');

                INSERT INTO AccountGroups (Id, CompanyId, Name, Nature, CreatedAt, CreatedBy)
                VALUES (@GroupId, @CompanyId, 'Assets', 1, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            cmd.Parameters.AddWithValue("@GroupId", groupId);
            await cmd.ExecuteNonQueryAsync();
        }

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Ledgers (Id, CompanyId, GroupId, Name, PrintName, OpeningBalance, OpeningBalanceType, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, @GroupId, 'Invalid Type Ledger', 'Invalid Type Ledger', 1000, 3, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId);
            cmd.Parameters.AddWithValue("@GroupId", groupId);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: OpeningBalanceType IN (1, 2)
    }

    [Fact]
    public async Task Companies_GSTRegistrationTypeEnum_CheckConstraint_RejectsOutOfRangeValue()
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, GSTRegistrationType, CreatedAt, CreatedBy)
                VALUES (@Id, 'Invalid GST Co', 'Invalid GST Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', 0, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: GSTRegistrationType BETWEEN 1 AND 4
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task Companies_GSTRegistrationTypeEnum_CheckConstraint_RejectsValuesAbove4(int invalidGstType)
    {
        await _initializer.InitializeAsync();
        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, GSTRegistrationType, CreatedAt, CreatedBy)
                VALUES (@Id, 'Invalid GST Co Above 4', 'Invalid GST Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', @GSTType, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@GSTType", invalidGstType);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: GSTRegistrationType BETWEEN 1 AND 4
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Ledgers_GSTRegistrationTypeEnum_CheckConstraint_RejectsOutOfRangeValues(int invalidGstType)
    {
        await _initializer.InitializeAsync();
        var companyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        // Seed valid company and group first
        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'GST Test Co', 'GST Test Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');

                INSERT INTO AccountGroups (Id, CompanyId, Name, Nature, CreatedAt, CreatedBy)
                VALUES (@GroupId, @CompanyId, 'Sundry Debtors', 1, '2026-04-01T00:00:00Z', 'admin');
            ";
            setupCmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            setupCmd.Parameters.AddWithValue("@GroupId", groupId.ToString());
            await setupCmd.ExecuteNonQueryAsync();
        }

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Ledgers (Id, CompanyId, GroupId, Name, PrintName, GSTRegistrationType, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, @GroupId, 'Invalid GST Ledger', 'Invalid GST Ledger', @GSTType, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());
            cmd.Parameters.AddWithValue("@GSTType", invalidGstType);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: GSTRegistrationType BETWEEN 1 AND 4
    }

    [Theory]
    [InlineData("2026-04-01", "2026-04-01")] // Equal: EndDate == StartDate
    [InlineData("2026-04-01", "2026-03-31")] // Inverted: EndDate < StartDate
    public async Task FinancialYears_DateRange_CheckConstraint_RejectsEndDateLessThanOrEqualToStartDate(string startDate, string endDate)
    {
        await _initializer.InitializeAsync();
        var companyId = Guid.NewGuid();

        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        // Seed valid company
        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'FY Constraint Co', 'FY Constraint Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');
            ";
            setupCmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            await setupCmd.ExecuteNonQueryAsync();
        }

        Func<Task> act = async () =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FinancialYears (Id, CompanyId, YearName, StartDate, EndDate, CreatedAt, CreatedBy)
                VALUES (@Id, @CompanyId, 'Invalid FY', @StartDate, @EndDate, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            cmd.Parameters.AddWithValue("@StartDate", startDate);
            cmd.Parameters.AddWithValue("@EndDate", endDate);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19); // CHECK constraint failed: EndDate > StartDate
    }

    [Fact]
    public async Task Ledgers_MonetaryRoundTripPrecision_StoresAndReadsExactPaise()
    {
        await _initializer.InitializeAsync();
        var companyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Money Co', 'Money Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');

                INSERT INTO AccountGroups (Id, CompanyId, Name, Nature, CreatedAt, CreatedBy)
                VALUES (@GroupId, @CompanyId, 'Current Assets', 1, '2026-04-01T00:00:00Z', 'admin');
            ";
            cmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            cmd.Parameters.AddWithValue("@GroupId", groupId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        var ledger = Ledger.Create(
            companyId,
            groupId,
            "Precision Test Ledger",
            openingBalance: 1234.56m,
            openingBalanceType: DebitCreditType.Debit,
            creditLimit: 98765.43m);

        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "admin";

        await _ledgerRepository.AddAsync(ledger);

        // Verify underlying storage is exact INTEGER paise
        await using (var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT OpeningBalance, CreditLimit FROM Ledgers WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", ledger.Id.ToString());
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            var rawOpBal = reader.GetInt64(0);
            var rawCreditLimit = reader.GetInt64(1);

            rawOpBal.Should().Be(123456L, "Opening balance ₹1234.56 must be stored as 123456 paise");
            rawCreditLimit.Should().Be(9876543L, "Credit limit ₹98765.43 must be stored as 9876543 paise");
        }

        // Verify retrieval via repository reproduces exact decimal
        var retrieved = await _ledgerRepository.GetByIdAsync(companyId, ledger.Id);
        retrieved.Should().NotBeNull();
        retrieved!.OpeningBalance.Should().Be(new Money(1234.56m));
        retrieved.CreditLimit.Should().Be(new Money(98765.43m));
    }

    [Fact]
    public async Task AccountingIntegrity_ForeignKeys_WhenChildTableUsesOnDeleteRestrict_BlocksCompanyDeletion()
    {
        // Proves that when Phase 1.5 transaction tables (Vouchers) reference Company/FinancialYear
        // with ON DELETE RESTRICT, SQLite physically blocks Company deletion, preventing silent history loss.
        await _initializer.InitializeAsync();
        var companyId = Guid.NewGuid();

        await using var conn = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        await using (var setupCmd = conn.CreateCommand())
        {
            setupCmd.CommandText = @"
                INSERT INTO Companies (Id, CompanyName, PrintName, StateName, StateCode, FinancialYearBeginning, BooksBeginningFrom, CreatedAt, CreatedBy)
                VALUES (@CompanyId, 'Protected Audit Co', 'Protected Audit Co', 'Gujarat', '24', '2026-04-01', '2026-04-01', '2026-04-01T00:00:00Z', 'admin');

                -- Create temporary table with ON DELETE RESTRICT representing future Phase 1.5 Voucher constraint
                CREATE TABLE TestAccountingVoucherAuditLock (
                    Id TEXT PRIMARY KEY,
                    CompanyId TEXT NOT NULL,
                    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT
                );

                INSERT INTO TestAccountingVoucherAuditLock (Id, CompanyId)
                VALUES (@AuditLockId, @CompanyId);
            ";
            setupCmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            setupCmd.Parameters.AddWithValue("@AuditLockId", Guid.NewGuid().ToString());
            await setupCmd.ExecuteNonQueryAsync();
        }

        // Attempting to delete the Company must fail with Foreign Key violation (error 19)
        Func<Task> act = async () =>
        {
            await using var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM Companies WHERE Id = @CompanyId;";
            delCmd.Parameters.AddWithValue("@CompanyId", companyId.ToString());
            await delCmd.ExecuteNonQueryAsync();
        };

        await act.Should().ThrowAsync<SqliteException>()
            .Where(ex => ex.SqliteErrorCode == 19);
    }

    public void Dispose()
    {
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
