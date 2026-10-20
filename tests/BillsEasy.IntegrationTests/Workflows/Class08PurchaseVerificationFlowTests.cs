using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using BillsEasy.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BillsEasy.IntegrationTests.Workflows;

public class Class08PurchaseVerificationFlowTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly DatabaseInitializer _initializer;
    private readonly CompanyRepository _companyRepository;
    private readonly FinancialYearRepository _financialYearRepository;
    private readonly AccountGroupRepository _accountGroupRepository;
    private readonly LedgerRepository _ledgerRepository;
    private readonly UnitRepository _unitRepository;
    private readonly ItemGroupRepository _itemGroupRepository;
    private readonly ItemRepository _itemRepository;
    private readonly VoucherRepository _voucherRepository;
    private readonly BillReferenceRepository _billReferenceRepository;
    private readonly BillAllocationRepository _billAllocationRepository;
    private readonly PurchaseInvoiceRepository _purchaseInvoiceRepository;
    private readonly StockMovementRepository _stockMovementRepository;

    private readonly PostingService _postingService;
    private readonly BillWiseService _billWiseService;
    private readonly PurchaseService _purchaseService;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _financialYearId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _purchaseLedgerId = Guid.NewGuid();
    private readonly Guid _freightLedgerId = Guid.NewGuid();
    private readonly Guid _pcsUnitId = Guid.NewGuid();
    private readonly Guid _nosUnitId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();
    private readonly Guid _monitorItemId = Guid.NewGuid();
    private readonly Guid _laptopItemId = Guid.NewGuid();

    public Class08PurchaseVerificationFlowTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_C08Verif_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _initializer = new DatabaseInitializer(_connectionFactory);
        _companyRepository = new CompanyRepository(_connectionFactory);
        _financialYearRepository = new FinancialYearRepository(_connectionFactory);
        _accountGroupRepository = new AccountGroupRepository(_connectionFactory);
        _ledgerRepository = new LedgerRepository(_connectionFactory);
        _unitRepository = new UnitRepository(_connectionFactory);
        _itemGroupRepository = new ItemGroupRepository(_connectionFactory);
        _itemRepository = new ItemRepository(_connectionFactory);
        _voucherRepository = new VoucherRepository(_connectionFactory);
        _billReferenceRepository = new BillReferenceRepository(_connectionFactory);
        _billAllocationRepository = new BillAllocationRepository(_connectionFactory);
        _purchaseInvoiceRepository = new PurchaseInvoiceRepository(_connectionFactory);
        _stockMovementRepository = new StockMovementRepository(_connectionFactory);

        var userService = new StandaloneCurrentUserService();
        var dateProvider = new DateTimeProvider();

        _postingService = new PostingService(
            _voucherRepository,
            _companyRepository,
            _financialYearRepository,
            _ledgerRepository,
            userService,
            dateProvider,
            _billReferenceRepository,
            _billAllocationRepository);

        _billWiseService = new BillWiseService(
            _billReferenceRepository,
            _billAllocationRepository,
            _voucherRepository,
            _companyRepository,
            _ledgerRepository,
            userService,
            dateProvider);
        _purchaseService = new PurchaseService(
            _connectionFactory,
            _purchaseInvoiceRepository,
            _stockMovementRepository,
            _voucherRepository,
            _postingService,
            _billWiseService,
            _companyRepository,
            _financialYearRepository,
            _ledgerRepository,
            _accountGroupRepository,
            _itemRepository,
            _unitRepository,
            userService,
            dateProvider);
    }

    [Fact]
    public async Task CompleteClass08PurchaseWorkflow_ExecutesAndVerifiesAllSubsystems()
    {
        await _initializer.InitializeAsync();

        // 1. Company Setup: Shree Krishna Traders
        var company = Company.Create(
            companyName: "Shree Krishna Traders",
            printName: "Shree Krishna Traders",
            shortName: "SKT",
            stateName: "Delhi",
            stateCode: "07",
            financialYearBeginning: new DateOnly(2026, 4, 1),
            booksBeginningFrom: new DateOnly(2026, 4, 1));
        await _companyRepository.AddAsync(company);

        // 2. Financial Year
        var fy = company.FinancialYears.First();

        // 3. Account Groups
        var creditorGroup = AccountGroup.Create(company.Id, "Sundry Creditors", GroupNature.Liability, isPrimary: true);
        var purchaseGroup = AccountGroup.Create(company.Id, "Purchase Accounts", GroupNature.Expense, isPrimary: true);
        var expenseGroup = AccountGroup.Create(company.Id, "Indirect Expenses", GroupNature.Expense, isPrimary: true);
        await _accountGroupRepository.AddAsync(creditorGroup);
        await _accountGroupRepository.AddAsync(purchaseGroup);
        await _accountGroupRepository.AddAsync(expenseGroup);

        // 4. Ledgers
        var supplier = new Ledger(
            _supplierId, company.Id, creditorGroup.Id, "Yashika Pvt Ltd",
            billWiseEnabled: true, creditPeriodDays: 30);
        var purchaseLedger = new Ledger(_purchaseLedgerId, company.Id, purchaseGroup.Id, "Purchase");
        var freightLedger = new Ledger(_freightLedgerId, company.Id, expenseGroup.Id, "Freight & Forwarding Charges");
        await _ledgerRepository.AddAsync(supplier);
        await _ledgerRepository.AddAsync(purchaseLedger);
        await _ledgerRepository.AddAsync(freightLedger);

        // 5. Units & Item Group
        var pcsUnit = new Unit(_pcsUnitId, company.Id, "Pcs", "Pieces", 0);
        var nosUnit = new Unit(_nosUnitId, company.Id, "NOS", "Numbers", 0);
        await _unitRepository.AddAsync(pcsUnit);
        await _unitRepository.AddAsync(nosUnit);

        var generalGroup = ItemGroup.Create(company.Id, "Electronics & IT");
        await _itemGroupRepository.AddAsync(generalGroup);

        // 6. Items
        var monitorItem = new Item(_monitorItemId, company.Id, generalGroup.Id, _pcsUnitId, "Samsung 19 Inch LED Monitor");
        var laptopItem = new Item(_laptopItemId, company.Id, generalGroup.Id, _nosUnitId, "Dell Laptop Inspiron 15");
        await _itemRepository.AddAsync(monitorItem);
        await _itemRepository.AddAsync(laptopItem);

        // 7. Purchase Invoice Creation (BUSY Class 8 Scenario)
        // 5 Pcs Samsung Monitor @ 5,500 = 27,500
        // 1 NOS Dell Laptop @ 40,000 = 40,000
        // Freight = 500
        // Grand Total = 68,000
        var invoiceDto = new CreatePurchaseInvoiceDto(
            CompanyId: company.Id,
            FinancialYearId: fy.Id,
            InvoiceNumber: "INV/2026/001",
            InvoiceDate: new DateOnly(2026, 5, 10),
            SupplierId: _supplierId,
            Series: "Main",
            MaterialCentre: "Main Store",
            Narration: "Being goods purchased from Yashika Pvt Ltd as per invoice",
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(_monitorItemId, 5m, _pcsUnitId, 5500m),
                new(_laptopItemId, 1m, _nosUnitId, 40000m)
            },
            Sundries: new List<CreatePurchaseInvoiceSundryDto>
            {
                new(_freightLedgerId, null, 500m)
            },
            Transport: new TransportDetailsDto(
                TransportName: "Blue Dart",
                GrRrNumber: "BD-99882",
                TransportDate: new DateOnly(2026, 5, 10),
                VehicleNumber: "DL-01-AB-1234",
                StationFromPlace: "Delhi"));

        // Act: Create Purchase Invoice through Application Service
        var result = await _purchaseService.CreatePurchaseInvoiceAsync(invoiceDto);

        // Assert 1: Purchase Invoice Aggregate DTO
        result.Should().NotBeNull();
        result.InvoiceNumber.Should().Be("INV/2026/001");
        result.ItemSubtotal.Should().Be(67500m);
        result.SundrySubtotal.Should().Be(500m);
        result.GrandTotal.Should().Be(68000m);
        result.Status.Should().Be(PurchaseInvoiceStatus.Active);
        result.Lines.Should().HaveCount(2);
        result.Sundries.Should().HaveCount(1);
        result.Transport.Should().NotBeNull();
        result.Transport!.TransportName.Should().Be("Blue Dart");
        result.Transport.GrRrNumber.Should().Be("BD-99882");

        // Assert 2: Financial Voucher & Posting
        result.VoucherId.Should().NotBeNull();
        var voucher = await _voucherRepository.GetByIdAsync(company.Id, result.VoucherId!.Value);
        voucher.Should().NotBeNull();
        voucher!.Type.Should().Be(VoucherType.Purchase);
        voucher.VoucherNumber.Should().Be("INV/2026/001");
        voucher.TotalDebit.Amount.Should().Be(68000m);
        voucher.TotalCredit.Amount.Should().Be(68000m);
        voucher.IsBalanced.Should().BeTrue();
        voucher.Lines.Should().HaveCount(3);

        var drPurchaseLine = voucher.Lines.FirstOrDefault(l => l.LedgerId == _purchaseLedgerId);
        drPurchaseLine.Should().NotBeNull();
        drPurchaseLine!.Type.Should().Be(DebitCreditType.Debit);
        drPurchaseLine.Amount.Amount.Should().Be(67500m);

        var drFreightLine = voucher.Lines.FirstOrDefault(l => l.LedgerId == _freightLedgerId);
        drFreightLine.Should().NotBeNull();
        drFreightLine!.Type.Should().Be(DebitCreditType.Debit);
        drFreightLine.Amount.Amount.Should().Be(500m);

        var crSupplierLine = voucher.Lines.FirstOrDefault(l => l.LedgerId == _supplierId);
        crSupplierLine.Should().NotBeNull();
        crSupplierLine!.Type.Should().Be(DebitCreditType.Credit);
        crSupplierLine.Amount.Amount.Should().Be(68000m);

        // Assert 3: Bill-Wise Reference Created
        var refRecord = await _billReferenceRepository.GetByReferenceNumberAsync(company.Id, _supplierId, "INV/2026/001");
        refRecord.Should().NotBeNull();
        refRecord!.ReferenceNumber.Should().Be("INV/2026/001");
        refRecord.ReferenceType.Should().Be(BillReferenceType.NewRef);
        refRecord.OriginalAmount.Amount.Should().Be(68000m);
        refRecord.EntryType.Should().Be(DebitCreditType.Credit);

        // Assert 4: Physical Stock Movements Created at Main Store
        var monitorQty = await _stockMovementRepository.GetCurrentQuantityAsync(company.Id, _monitorItemId, "Main Store");
        monitorQty.Should().Be(5m);

        var laptopQty = await _stockMovementRepository.GetCurrentQuantityAsync(company.Id, _laptopItemId, "Main Store");
        laptopQty.Should().Be(1m);

        var monitorMovements = await _stockMovementRepository.GetByItemIdAsync(company.Id, _monitorItemId);
        monitorMovements.Should().HaveCount(1);
        monitorMovements[0].Quantity.Should().Be(5m);
        monitorMovements[0].Rate.Amount.Should().Be(5500m);
        monitorMovements[0].MaterialCentre.Should().Be("Main Store");
        monitorMovements[0].MovementType.Should().Be(StockMovementType.PurchaseInward);

        var laptopMovements = await _stockMovementRepository.GetByItemIdAsync(company.Id, _laptopItemId);
        laptopMovements.Should().HaveCount(1);
        laptopMovements[0].Quantity.Should().Be(1m);
        laptopMovements[0].Rate.Amount.Should().Be(40000m);
        laptopMovements[0].MaterialCentre.Should().Be("Main Store");
        laptopMovements[0].MovementType.Should().Be(StockMovementType.PurchaseInward);

        // Assert 5: Day Book Reconciliation via Voucher Repository
        var dayBookVouchers = await _voucherRepository.GetAllAsync(
            company.Id,
            fromDate: new DateOnly(2026, 5, 10),
            toDate: new DateOnly(2026, 5, 10));
        dayBookVouchers.Should().HaveCount(1);
        var dayBookVoucher = dayBookVouchers.First();
        dayBookVoucher.Type.Should().Be(VoucherType.Purchase);
        dayBookVoucher.VoucherNumber.Should().Be("INV/2026/001");
        dayBookVoucher.TotalDebit.Amount.Should().Be(68000m);
        dayBookVoucher.TotalCredit.Amount.Should().Be(68000m);

        // Assert 6: Supplier Ledger Reconciliation via Voucher Repository
        var supplierVouchers = await _voucherRepository.GetAllAsync(
            company.Id,
            ledgerId: _supplierId);
        supplierVouchers.Should().HaveCount(1);
        var supplierVoucher = supplierVouchers.First();
        var supplierLineInVoucher = supplierVoucher.Lines.First(l => l.LedgerId == _supplierId);
        supplierLineInVoucher.Type.Should().Be(DebitCreditType.Credit);
        supplierLineInVoucher.Amount.Amount.Should().Be(68000m);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
