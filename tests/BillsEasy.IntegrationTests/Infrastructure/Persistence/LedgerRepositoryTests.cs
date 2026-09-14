using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public class LedgerRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly AccountGroupRepository _accountGroupRepository;
    private readonly LedgerRepository _ledgerRepository;

    public LedgerRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_LedgTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _accountGroupRepository = new AccountGroupRepository(_connectionFactory);
        _ledgerRepository = new LedgerRepository(_connectionFactory);
    }

    private async Task<(Company Company, AccountGroup Group)> CreateCompanyAndGroupAsync(string compName = "Test Co", string grpName = "Sundry Debtors")
    {
        var company = Company.Create(
            compName, compName, null, "Gujarat", "24",
            new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));
        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "tester";
        await _companyRepository.AddAsync(company);

        var group = AccountGroup.Create(company.Id, grpName, GroupNature.Asset, isPrimary: true);
        group.CreatedAt = DateTime.UtcNow;
        group.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(group);

        return (company, group);
    }

    [Fact]
    public async Task AddAndGetById_PersistsLedgerSuccessfully()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var ledger = Ledger.Create(
            company.Id,
            group.Id,
            "Rohit Sharma",
            printName: "Rohit Sharma Enterprises",
            alias: "ROHIT",
            openingBalance: 25000.75m,
            openingBalanceType: DebitCreditType.Debit,
            billWiseEnabled: true,
            creditPeriodDays: 45,
            creditLimit: 100000m,
            addressLine1: "101 Trade Center",
            city: "Surat",
            stateName: "Gujarat",
            stateCode: "24",
            pinCode: "395001",
            gstin: "24AAACS1234A1Z5",
            gstRegistrationType: GSTRegistrationType.Regular,
            pan: "AAACS1234A",
            contactPerson: "Rohit",
            mobileNumber: "9876543210",
            email: "rohit@trade.com",
            bankName: "HDFC Bank",
            bankAccountNumber: "50100012345678",
            bankIfsc: "HDFC0000123",
            bankBranch: "Surat Main");

        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "tester";

        await _ledgerRepository.AddAsync(ledger);

        var retrieved = await _ledgerRepository.GetByIdAsync(company.Id, ledger.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Rohit Sharma");
        retrieved.PrintName.Should().Be("Rohit Sharma Enterprises");
        retrieved.Alias.Should().Be("ROHIT");
        retrieved.OpeningBalance.Should().Be(new Money(25000.75m));
        retrieved.OpeningBalanceType.Should().Be(DebitCreditType.Debit);
        retrieved.BillWiseEnabled.Should().BeTrue();
        retrieved.CreditPeriodDays.Should().Be(45);
        retrieved.CreditLimit.Should().Be(new Money(100000m));
        retrieved.City.Should().Be("Surat");
        retrieved.GSTIN.Should().Be("24AAACS1234A1Z5");
        retrieved.BankName.Should().Be("HDFC Bank");
        retrieved.CreatedBy.Should().Be("tester");
    }

    [Fact]
    public async Task AddRange_PersistsMultipleLedgers()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var l1 = Ledger.Create(company.Id, group.Id, "Customer A", openingBalance: 1000m);
        var l2 = Ledger.Create(company.Id, group.Id, "Customer B", openingBalance: 2000m);
        l1.CreatedAt = DateTime.UtcNow;
        l1.CreatedBy = "tester";
        l2.CreatedAt = DateTime.UtcNow;
        l2.CreatedBy = "tester";

        await _ledgerRepository.AddRangeAsync(new[] { l1, l2 });

        var all = await _ledgerRepository.GetAllAsync(company.Id);
        all.Should().HaveCount(2);

        var groupLedgers = await _ledgerRepository.GetByGroupIdAsync(company.Id, group.Id);
        groupLedgers.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExistsByName_EnforcesCompanyIsolation()
    {
        await _initializer.InitializeAsync();
        var (compA, grpA) = await CreateCompanyAndGroupAsync("Company A", "Group A");
        var (compB, grpB) = await CreateCompanyAndGroupAsync("Company B", "Group B");

        var ledgerA = Ledger.Create(compA.Id, grpA.Id, "Standard Ledger");
        ledgerA.CreatedAt = DateTime.UtcNow;
        ledgerA.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(ledgerA);

        var existsInA = await _ledgerRepository.ExistsByNameAsync(compA.Id, "Standard Ledger");
        var existsInB = await _ledgerRepository.ExistsByNameAsync(compB.Id, "Standard Ledger");

        existsInA.Should().BeTrue();
        existsInB.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateNameInSameCompany_ThrowsSqliteException()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var l1 = Ledger.Create(company.Id, group.Id, "Unique Ledger");
        l1.CreatedAt = DateTime.UtcNow;
        l1.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(l1);

        var l2 = Ledger.Create(company.Id, group.Id, "unique ledger"); // Case-insensitive collision
        l2.CreatedAt = DateTime.UtcNow;
        l2.CreatedBy = "tester";

        var act = async () => await _ledgerRepository.AddAsync(l2);

        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*UNIQUE constraint failed: Ledgers.CompanyId, Ledgers.Name*");
    }

    [Fact]
    public async Task DeleteAccountGroup_WhenReferencedByLedger_ThrowsSqliteException()
    {
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var ledger = Ledger.Create(company.Id, group.Id, "Dependent Ledger");
        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(ledger);

        // Attempt to delete AccountGroup referenced by Ledger -> RESTRICT foreign key constraint triggers
        var act = async () => await _accountGroupRepository.DeleteAsync(company.Id, group.Id);

        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task DeleteCompany_CascadesToLedgers()
    {
        // SCOPE: Verifies master setup cascade behavior for an EMPTY company (zero transaction history / no vouchers).
        // Once transactional vouchers exist, Company deletion is blocked by Application guard and DB RESTRICT FK.
        await _initializer.InitializeAsync();
        var (company, group) = await CreateCompanyAndGroupAsync();

        var ledger = Ledger.Create(company.Id, group.Id, "Ledger To Cascade");
        ledger.CreatedAt = DateTime.UtcNow;
        ledger.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(ledger);

        // Delete Company
        await _companyRepository.DeleteAsync(company.Id);

        var remaining = await _ledgerRepository.GetAllAsync(company.Id);
        remaining.Should().BeEmpty();
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
        catch
        {
            // Best effort cleanup in temp directory
        }
    }
}