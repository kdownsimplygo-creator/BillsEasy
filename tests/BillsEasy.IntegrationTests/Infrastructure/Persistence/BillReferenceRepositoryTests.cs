using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
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

public class BillReferenceRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly FinancialYearRepository _financialYearRepository;
    private readonly AccountGroupRepository _accountGroupRepository;
    private readonly LedgerRepository _ledgerRepository;
    private readonly VoucherRepository _voucherRepository;
    private readonly BillReferenceRepository _billReferenceRepository;
    private readonly BillAllocationRepository _billAllocationRepository;
    private readonly BillWiseService _billWiseService;
    private readonly PostingService _postingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BillReferenceRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_BillTest_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _financialYearRepository = new FinancialYearRepository(_connectionFactory);
        _accountGroupRepository = new AccountGroupRepository(_connectionFactory);
        _ledgerRepository = new LedgerRepository(_connectionFactory);
        _voucherRepository = new VoucherRepository(_connectionFactory);
        _billReferenceRepository = new BillReferenceRepository(_connectionFactory);
        _billAllocationRepository = new BillAllocationRepository(_connectionFactory);

        _currentUserService = new StandaloneCurrentUserService();
        _dateTimeProvider = new DateTimeProvider();

        _billWiseService = new BillWiseService(
            _billReferenceRepository,
            _billAllocationRepository,
            _voucherRepository,
            _companyRepository,
            _ledgerRepository,
            _currentUserService,
            _dateTimeProvider);

        _postingService = new PostingService(
            _voucherRepository,
            _companyRepository,
            _financialYearRepository,
            _ledgerRepository,
            _currentUserService,
            _dateTimeProvider,
            _billReferenceRepository,
            _billAllocationRepository);
    }

    private async Task<(Company Company, FinancialYear FY, Ledger CustomerA, Ledger CustomerB, Ledger Bank)> SeedCompanyAndLedgersAsync(string companyName = "Test Co")
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName,
            companyName,
            null,
            "Gujarat",
            "24",
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 1));
        company.CreatedAt = DateTime.UtcNow;
        company.CreatedBy = "tester";
        await _companyRepository.AddAsync(company);

        var fy = company.FinancialYears.First();

        var debtorsGroup = AccountGroup.Create(company.Id, "Sundry Debtors", GroupNature.Asset, isPrimary: true);
        debtorsGroup.CreatedAt = DateTime.UtcNow;
        debtorsGroup.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(debtorsGroup);

        var bankGroup = AccountGroup.Create(company.Id, "Bank Accounts", GroupNature.Asset, isPrimary: true);
        bankGroup.CreatedAt = DateTime.UtcNow;
        bankGroup.CreatedBy = "tester";
        await _accountGroupRepository.AddAsync(bankGroup);

        var customerA = Ledger.Create(company.Id, debtorsGroup.Id, "Customer A", billWiseEnabled: true);
        customerA.CreatedAt = DateTime.UtcNow;
        customerA.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(customerA);

        var customerB = Ledger.Create(company.Id, debtorsGroup.Id, "Customer B", billWiseEnabled: true);
        customerB.CreatedAt = DateTime.UtcNow;
        customerB.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(customerB);

        var bank = Ledger.Create(company.Id, bankGroup.Id, "HDFC Bank", billWiseEnabled: false);
        bank.CreatedAt = DateTime.UtcNow;
        bank.CreatedBy = "tester";
        await _ledgerRepository.AddAsync(bank);

        return (company, fy, customerA, customerB, bank);
    }

    [Fact]
    public async Task AddAndGetById_PersistsAndRetrievesBillReference()
    {
        var (company, _, customerA, _, _) = await SeedCompanyAndLedgersAsync();

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-2026-001",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(5000m),
            DebitCreditType.Debit,
            new DateOnly(2026, 5, 15));
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";

        await _billReferenceRepository.AddAsync(bill);

        var loaded = await _billReferenceRepository.GetByIdAsync(company.Id, bill.Id);
        loaded.Should().NotBeNull();
        loaded!.ReferenceNumber.Should().Be("INV-2026-001");
        loaded.ReferenceType.Should().Be(BillReferenceType.NewRef);
        loaded.Status.Should().Be(BillReferenceStatus.Active);
        loaded.OriginalAmount.Amount.Should().Be(5000m);
        loaded.EntryType.Should().Be(DebitCreditType.Debit);
        loaded.BillDate.Should().Be(new DateOnly(2026, 4, 15));
        loaded.DueDate.Should().Be(new DateOnly(2026, 5, 15));
    }

    [Fact]
    public async Task UniqueConstraint_DuplicateReferenceNumberPerCustomer_ThrowsSqliteException()
    {
        var (company, _, customerA, _, _) = await SeedCompanyAndLedgersAsync();

        var bill1 = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-100",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000m),
            DebitCreditType.Debit);
        bill1.CreatedAt = DateTime.UtcNow;
        bill1.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill1);

        var bill2 = BillReference.Create(
            company.Id,
            customerA.Id,
            "inv-100", // Case-insensitive duplicate
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 20),
            new Money(2000m),
            DebitCreditType.Debit);
        bill2.CreatedAt = DateTime.UtcNow;
        bill2.CreatedBy = "tester";

        var act = async () => await _billReferenceRepository.AddAsync(bill2);
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*UNIQUE constraint failed*");
    }

    [Fact]
    public async Task DifferentLedgers_CanShareSameReferenceNumber()
    {
        var (company, _, customerA, customerB, _) = await SeedCompanyAndLedgersAsync();

        var billA = BillReference.Create(
            company.Id,
            customerA.Id,
            "BILL-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000m),
            DebitCreditType.Debit);
        billA.CreatedAt = DateTime.UtcNow;
        billA.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(billA);

        var billB = BillReference.Create(
            company.Id,
            customerB.Id,
            "BILL-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(2000m),
            DebitCreditType.Debit);
        billB.CreatedAt = DateTime.UtcNow;
        billB.CreatedBy = "tester";

        var act = async () => await _billReferenceRepository.AddAsync(billB);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DynamicOutstandingBalance_CalculatesZeroPartialAndFullAllocations()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        // 1. Create Bill of 1000
        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-301",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(1000m),
            DebitCreditType.Debit);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        // Initial balance check: Allocated = 0, Outstanding = 1000
        var list0 = await _billReferenceRepository.GetActiveBillsByLedgerAsync(company.Id, customerA.Id);
        list0.Should().HaveCount(1);
        list0[0].TotalAllocated.Amount.Should().Be(0m);
        list0[0].OutstandingAmount.Amount.Should().Be(1000m);

        // Create settlement voucher with 2 lines: Bank Dr 400, Customer A Cr 400
        var voucher1 = Voucher.Create(company.Id, fy.Id, "RCT-001", VoucherType.Receipt, new DateOnly(2026, 4, 15));
        var bankLine = voucher1.AddLine(bank.Id, DebitCreditType.Debit, new Money(400m));
        var customerLine1 = voucher1.AddLine(customerA.Id, DebitCreditType.Credit, new Money(400m));
        voucher1.Post(fy);
        await _voucherRepository.AddAsync(voucher1);

        // Partial Allocation of 400
        var alloc1 = BillAllocation.Create(company.Id, customerLine1.Id, bill.Id, new Money(400m), "tester");
        await _billAllocationRepository.AddAsync(alloc1);

        var list1 = await _billReferenceRepository.GetActiveBillsByLedgerAsync(company.Id, customerA.Id);
        list1[0].TotalAllocated.Amount.Should().Be(400m);
        list1[0].OutstandingAmount.Amount.Should().Be(600m);

        // Create second settlement voucher: Bank Dr 600, Customer A Cr 600
        var voucher2 = Voucher.Create(company.Id, fy.Id, "RCT-002", VoucherType.Receipt, new DateOnly(2026, 4, 20));
        voucher2.AddLine(bank.Id, DebitCreditType.Debit, new Money(600m));
        var customerLine2 = voucher2.AddLine(customerA.Id, DebitCreditType.Credit, new Money(600m));
        voucher2.Post(fy);
        await _voucherRepository.AddAsync(voucher2);

        // Full settlement allocation of remaining 600
        var alloc2 = BillAllocation.Create(company.Id, customerLine2.Id, bill.Id, new Money(600m), "tester");
        await _billAllocationRepository.AddAsync(alloc2);

        var list2 = await _billReferenceRepository.GetActiveBillsByLedgerAsync(company.Id, customerA.Id);
        list2[0].TotalAllocated.Amount.Should().Be(1000m);
        list2[0].OutstandingAmount.Amount.Should().Be(0m);
    }

    [Fact]
    public async Task ValidSettlement_ViaService_UpdatesDynamicOutstandingBalance()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        var billDto = await _billWiseService.CreateBillReferenceAsync(new CreateBillReferenceDto(
            company.Id,
            customerA.Id,
            "INV-SVC-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            750m,
            DebitCreditType.Debit));

        var voucher = Voucher.Create(company.Id, fy.Id, "RCT-SVC-1", VoucherType.Receipt, new DateOnly(2026, 4, 12));
        voucher.AddLine(bank.Id, DebitCreditType.Debit, new Money(500m));
        var custLine = voucher.AddLine(customerA.Id, DebitCreditType.Credit, new Money(500m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        var allocDto = await _billWiseService.AllocateBillAsync(new CreateBillAllocationDto(
            company.Id,
            custLine.Id,
            billDto.Id,
            500m));

        allocDto.AllocatedAmount.Should().Be(500m);

        var pending = await _billWiseService.GetPendingBillsAsync(company.Id, customerA.Id);
        pending.Should().HaveCount(1);
        pending[0].TotalAllocated.Should().Be(500m);
        pending[0].OutstandingAmount.Should().Be(250m);
    }

    [Fact]
    public async Task InvalidCrossLedgerSettlement_ThrowsDomainException()
    {
        var (company, fy, customerA, customerB, bank) = await SeedCompanyAndLedgersAsync();

        // Customer A has a bill
        var billA = await _billWiseService.CreateBillReferenceAsync(new CreateBillReferenceDto(
            company.Id,
            customerA.Id,
            "INV-A-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            1000m,
            DebitCreditType.Debit));

        // Customer B pays
        var voucherB = Voucher.Create(company.Id, fy.Id, "RCT-B-1", VoucherType.Receipt, new DateOnly(2026, 4, 12));
        voucherB.AddLine(bank.Id, DebitCreditType.Debit, new Money(500m));
        var custLineB = voucherB.AddLine(customerB.Id, DebitCreditType.Credit, new Money(500m));
        voucherB.Post(fy);
        await _voucherRepository.AddAsync(voucherB);

        // Attempt Customer B receipt settling Customer A bill
        var act = () => _billWiseService.AllocateBillAsync(new CreateBillAllocationDto(
            company.Id,
            custLineB.Id,
            billA.Id,
            500m));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Cross-ledger allocation is prohibited*");
    }

    [Fact]
    public async Task InvalidCrossCompanySettlement_ThrowsDomainException()
    {
        var (company1, _, customerA, _, _) = await SeedCompanyAndLedgersAsync("Company 1");
        var (company2, fy2, _, _, bank2) = await SeedCompanyAndLedgersAsync("Company 2");

        var bill1 = await _billWiseService.CreateBillReferenceAsync(new CreateBillReferenceDto(
            company1.Id,
            customerA.Id,
            "INV-C1-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            1000m,
            DebitCreditType.Debit));

        var sundryDebtorsGroup2 = AccountGroup.Create(company2.Id, "Sundry Debtors 2", GroupNature.Asset, isPrimary: true);
        await _accountGroupRepository.AddAsync(sundryDebtorsGroup2);
        var customerInCompany2 = Ledger.Create(company2.Id, sundryDebtorsGroup2.Id, "Customer in Co 2", billWiseEnabled: true);
        await _ledgerRepository.AddAsync(customerInCompany2);

        var voucher2 = Voucher.Create(company2.Id, fy2.Id, "RCT-C2-1", VoucherType.Receipt, new DateOnly(2026, 4, 12));
        voucher2.AddLine(bank2.Id, DebitCreditType.Debit, new Money(500m));
        var custLine2 = voucher2.AddLine(customerInCompany2.Id, DebitCreditType.Credit, new Money(500m));
        voucher2.Post(fy2);
        await _voucherRepository.AddAsync(voucher2);

        var act = () => _billWiseService.AllocateBillAsync(new CreateBillAllocationDto(
            company1.Id,
            custLine2.Id,
            bill1.Id,
            500m));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Cross-company allocation is prohibited*");
    }

    [Fact]
    public async Task OverAllocation_ThrowsDomainException_AndLeavesZeroAllocations()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        var bill = await _billWiseService.CreateBillReferenceAsync(new CreateBillReferenceDto(
            company.Id,
            customerA.Id,
            "INV-OVER-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            500m,
            DebitCreditType.Debit));

        var voucher = Voucher.Create(company.Id, fy.Id, "RCT-OVER-1", VoucherType.Receipt, new DateOnly(2026, 4, 12));
        voucher.AddLine(bank.Id, DebitCreditType.Debit, new Money(1000m));
        var custLine = voucher.AddLine(customerA.Id, DebitCreditType.Credit, new Money(1000m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        // Attempting to allocate 600 against 500 bill
        var act = () => _billWiseService.AllocateBillAsync(new CreateBillAllocationDto(
            company.Id,
            custLine.Id,
            bill.Id,
            600m));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*exceeds current outstanding balance*");

        var allocations = await _billAllocationRepository.GetByBillReferenceIdAsync(company.Id, bill.Id);
        allocations.Should().BeEmpty();
    }

    [Fact]
    public async Task ADR10_CompanyDelete_RestrictedWhenBillReferencesExist()
    {
        var (company, _, customerA, _, _) = await SeedCompanyAndLedgersAsync();

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-RESTRICT-CO",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(500m),
            DebitCreditType.Debit);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        var act = async () => await _companyRepository.DeleteAsync(company.Id);
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task ADR10_LedgerDelete_RestrictedWhenBillReferencesExist_AndHasBillReferencesAsyncReturnsTrue()
    {
        var (company, _, customerA, _, _) = await SeedCompanyAndLedgersAsync();

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-RESTRICT-LDG",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(500m),
            DebitCreditType.Debit);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        var hasBills = await _ledgerRepository.HasBillReferencesAsync(company.Id, customerA.Id);
        hasBills.Should().BeTrue();

        var act = async () => await _ledgerRepository.DeleteAsync(company.Id, customerA.Id);
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task ADR10_VoucherLineDelete_RestrictedWhenSourceVoucherLineForBillReference()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        var voucher = Voucher.Create(company.Id, fy.Id, "SALES-001", VoucherType.Sales, new DateOnly(2026, 4, 10));
        var custLine = voucher.AddLine(customerA.Id, DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(bank.Id, DebitCreditType.Credit, new Money(1000m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-SRC-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(1000m),
            DebitCreditType.Debit,
            sourceVoucherLineId: custLine.Id);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        // Deleting the source line directly via SQL should fail with RESTRICT
        await using var connection = (SqliteConnection)await _connectionFactory.CreateConnectionAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM VoucherLines WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", custLine.Id.ToString());

        var act = async () => await cmd.ExecuteNonQueryAsync();
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task ADR10_VoucherLineDelete_CascadesToBillAllocations()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-CASC-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(1000m),
            DebitCreditType.Debit);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        var voucher = Voucher.Create(company.Id, fy.Id, "RCT-CASC-1", VoucherType.Receipt, new DateOnly(2026, 4, 12));
        voucher.AddLine(bank.Id, DebitCreditType.Debit, new Money(400m));
        var custLine = voucher.AddLine(customerA.Id, DebitCreditType.Credit, new Money(400m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        var alloc = BillAllocation.Create(company.Id, custLine.Id, bill.Id, new Money(400m), "tester");
        await _billAllocationRepository.AddAsync(alloc);

        var before = await _billAllocationRepository.GetByBillReferenceIdAsync(company.Id, bill.Id);
        before.Should().HaveCount(1);

        // Delete the voucher (which cascades to VoucherLines, and then VoucherLines cascade to BillAllocations)
        await _voucherRepository.DeleteAsync(company.Id, voucher.Id);

        var after = await _billAllocationRepository.GetByBillReferenceIdAsync(company.Id, bill.Id);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task ADR10_BillReferenceDelete_RestrictedWhenBillAllocationsExist()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-RESTRICT-ALLOC",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(1000m),
            DebitCreditType.Debit);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        var voucher = Voucher.Create(company.Id, fy.Id, "RCT-RESTRICT-1", VoucherType.Receipt, new DateOnly(2026, 4, 12));
        voucher.AddLine(bank.Id, DebitCreditType.Debit, new Money(400m));
        var custLine = voucher.AddLine(customerA.Id, DebitCreditType.Credit, new Money(400m));
        voucher.Post(fy);
        await _voucherRepository.AddAsync(voucher);

        var alloc = BillAllocation.Create(company.Id, custLine.Id, bill.Id, new Money(400m), "tester");
        await _billAllocationRepository.AddAsync(alloc);

        // Attempt deleting bill reference while allocation exists -> RESTRICT blocks it
        var act = async () => await _billReferenceRepository.DeleteAsync(company.Id, bill.Id);
        await act.Should().ThrowAsync<SqliteException>()
            .WithMessage("*FOREIGN KEY constraint failed*");
    }

    [Fact]
    public async Task CancellationLifecycle_OriginatingVoucher_BlockedWhenSettlementsExist()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        // 1. Originating sales voucher
        var salesVoucher = Voucher.Create(company.Id, fy.Id, "SLS-CANC-1", VoucherType.Sales, new DateOnly(2026, 4, 10));
        var custLine = salesVoucher.AddLine(customerA.Id, DebitCreditType.Debit, new Money(1000m));
        salesVoucher.AddLine(bank.Id, DebitCreditType.Credit, new Money(1000m));
        salesVoucher.Post(fy);
        await _voucherRepository.AddAsync(salesVoucher);

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-CANC-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(1000m),
            DebitCreditType.Debit,
            sourceVoucherLineId: custLine.Id);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        // 2. Receipt voucher settles 300
        var rctVoucher = Voucher.Create(company.Id, fy.Id, "RCT-CANC-1", VoucherType.Receipt, new DateOnly(2026, 4, 15));
        rctVoucher.AddLine(bank.Id, DebitCreditType.Debit, new Money(300m));
        var rctCustLine = rctVoucher.AddLine(customerA.Id, DebitCreditType.Credit, new Money(300m));
        rctVoucher.Post(fy);
        await _voucherRepository.AddAsync(rctVoucher);

        var alloc = BillAllocation.Create(company.Id, rctCustLine.Id, bill.Id, new Money(300m), "tester");
        await _billAllocationRepository.AddAsync(alloc);

        // Attempting to cancel sales voucher must be rejected because active allocations exist
        var act = () => _postingService.CancelVoucherAsync(company.Id, salesVoucher.Id);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*has active settlement allocations*");
    }

    [Fact]
    public async Task CancellationLifecycle_OriginatingVoucher_SoftCancelsBill_WhenZeroSettlements()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        var salesVoucher = Voucher.Create(company.Id, fy.Id, "SLS-CANC-2", VoucherType.Sales, new DateOnly(2026, 4, 10));
        var custLine = salesVoucher.AddLine(customerA.Id, DebitCreditType.Debit, new Money(1000m));
        salesVoucher.AddLine(bank.Id, DebitCreditType.Credit, new Money(1000m));
        salesVoucher.Post(fy);
        await _voucherRepository.AddAsync(salesVoucher);

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-CANC-2",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(1000m),
            DebitCreditType.Debit,
            sourceVoucherLineId: custLine.Id);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        // Zero settlements exist. Cancelling sales voucher should succeed and soft-cancel bill
        await _postingService.CancelVoucherAsync(company.Id, salesVoucher.Id);

        var loadedBill = await _billReferenceRepository.GetByIdAsync(company.Id, bill.Id);
        loadedBill.Should().NotBeNull();
        loadedBill!.Status.Should().Be(BillReferenceStatus.Cancelled);

        // Cancelled bill is excluded from active pending bills
        var activeBills = await _billReferenceRepository.GetActiveBillsByLedgerAsync(company.Id, customerA.Id);
        activeBills.Should().BeEmpty();
    }

    [Fact]
    public async Task CancellationLifecycle_SettlementVoucher_RestoresBillOutstandingBalance()
    {
        var (company, fy, customerA, _, bank) = await SeedCompanyAndLedgersAsync();

        var bill = BillReference.Create(
            company.Id,
            customerA.Id,
            "INV-RESTORE-1",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 10),
            new Money(1000m),
            DebitCreditType.Debit);
        bill.CreatedAt = DateTime.UtcNow;
        bill.CreatedBy = "tester";
        await _billReferenceRepository.AddAsync(bill);

        var rctVoucher = Voucher.Create(company.Id, fy.Id, "RCT-RESTORE-1", VoucherType.Receipt, new DateOnly(2026, 4, 15));
        rctVoucher.AddLine(bank.Id, DebitCreditType.Debit, new Money(400m));
        var rctCustLine = rctVoucher.AddLine(customerA.Id, DebitCreditType.Credit, new Money(400m));
        rctVoucher.Post(fy);
        await _voucherRepository.AddAsync(rctVoucher);

        var alloc = BillAllocation.Create(company.Id, rctCustLine.Id, bill.Id, new Money(400m), "tester");
        await _billAllocationRepository.AddAsync(alloc);

        var balBefore = await _billReferenceRepository.GetOutstandingBalanceAsync(company.Id, bill.Id);
        balBefore.Amount.Should().Be(600m);

        // Cancel settlement voucher -> deletes its allocations -> dynamically restores bill balance
        await _postingService.CancelVoucherAsync(company.Id, rctVoucher.Id);

        var balAfter = await _billReferenceRepository.GetOutstandingBalanceAsync(company.Id, bill.Id);
        balAfter.Amount.Should().Be(1000m);
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
