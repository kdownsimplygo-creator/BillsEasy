using System;
using System.Collections.Generic;
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
using BillsEasy.IntegrationTests.Workflows.RealDb;
using FluentAssertions;

namespace BillsEasy.IntegrationTests.Workflows.Simulation;

public class Class01to08SimulationRunner
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;
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

    private readonly CompanyService _companyService;
    private readonly AccountGroupService _accountGroupService;
    private readonly LedgerService _ledgerService;
    private readonly UnitService _unitService;
    private readonly ItemGroupService _itemGroupService;
    private readonly ItemService _itemService;
    private readonly PostingService _postingService;
    private readonly BillWiseService _billWiseService;
    private readonly PurchaseService _purchaseService;
    private readonly FinancialYearService _financialYearService;

    public SimulationContext Context { get; } = new();
    public SimulationReconciler Reconciler { get; }

    public Class01to08SimulationRunner(ISqliteDbConnectionFactory connectionFactory)
    {
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(connectionFactory);
        _connectionFactory = connectionFactory;
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

        _accountGroupService = new AccountGroupService(_accountGroupRepository, userService, dateProvider);
        _ledgerService = new LedgerService(_ledgerRepository, _accountGroupRepository, userService, dateProvider);

        _companyService = new CompanyService(
            _companyRepository,
            _financialYearRepository,
            userService,
            dateProvider,
            _accountGroupService,
            _ledgerService);

        _financialYearService = new FinancialYearService(
            _financialYearRepository,
            _companyRepository,
            userService,
            dateProvider);

        _unitService = new UnitService(_unitRepository, userService, dateProvider);
        _itemGroupService = new ItemGroupService(_itemGroupRepository, userService, dateProvider);
        _itemService = new ItemService(_itemRepository, _itemGroupRepository, _unitRepository, userService, dateProvider);

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

        Reconciler = new SimulationReconciler(
            _voucherRepository,
            _ledgerRepository,
            _billReferenceRepository,
            _stockMovementRepository);
    }

    private void EnsureSafeTarget()
    {
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(_connectionFactory);
    }

    public async Task InitializeDatabaseAsync()
    {
        EnsureSafeTarget();
        await _initializer.InitializeAsync();
    }

    public async Task RunStage01_CompanyAndFinancialYearAsync()
    {
        EnsureSafeTarget();
        var companyDto = await _companyService.CreateCompanyAsync(
            companyName: "Shree Krishna Electronics",
            printName: "Shree Krishna Electronics",
            shortName: "SKE",
            stateName: "Gujarat",
            stateCode: "24",
            financialYearBeginning: new DateOnly(2023, 4, 1),
            booksBeginningFrom: new DateOnly(2023, 4, 1));

        Context.CompanyId = companyDto.Id;
        Context.Company = companyDto;

        var fys = await _financialYearService.GetFinancialYearsByCompanyAsync(Context.CompanyId);
        fys.Should().HaveCount(1);
        Context.FinancialYearId = fys[0].Id;

        // Verify default account groups were seeded
        var groups = await _accountGroupRepository.GetAllAsync(Context.CompanyId);
        groups.Should().NotBeEmpty();

        Context.CapitalGroupId = groups.First(g => g.Name.Equals("Capital Account", StringComparison.OrdinalIgnoreCase)).Id;
        Context.BankGroupId = groups.First(g => g.Name.Equals("Bank Accounts", StringComparison.OrdinalIgnoreCase)).Id;
        Context.DebtorGroupId = groups.First(g => g.Name.Equals("Sundry Debtors", StringComparison.OrdinalIgnoreCase)).Id;
        Context.CreditorGroupId = groups.First(g => g.Name.Equals("Sundry Creditors", StringComparison.OrdinalIgnoreCase)).Id;
        Context.IndirectExpGroupId = groups.First(g => g.Name.Equals("Indirect Expenses", StringComparison.OrdinalIgnoreCase)).Id;
        Context.PurchaseAccountsGroupId = groups.First(g => g.Name.Equals("Purchase Accounts", StringComparison.OrdinalIgnoreCase)).Id;

        // Verify seeded ledgers
        var defaultLedgers = await _ledgerRepository.GetAllAsync(Context.CompanyId);
        Context.CashLedgerId = defaultLedgers.First(l => l.Name.Equals("Cash", StringComparison.OrdinalIgnoreCase)).Id;
        Context.PurchaseLedgerId = defaultLedgers.First(l => l.Name.Equals("Purchase", StringComparison.OrdinalIgnoreCase)).Id;
        var seededFreight = defaultLedgers.FirstOrDefault(l => l.Name.Equals("Freight & Forwarding Charges", StringComparison.OrdinalIgnoreCase));
        if (seededFreight != null)
        {
            Context.FreightLedgerId = seededFreight.Id;
        }
    }

    public async Task RunStage02_AccountMastersAsync()
    {
        EnsureSafeTarget();
        // Capital Account: Opening Credit = ₹11,23,000 (Corrected to achieve perfect balance!)
        var capital = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.CapitalGroupId,
            Name: "Capital Account",
            OpeningBalance: 1123000m,
            OpeningBalanceType: DebitCreditType.Credit));
        Context.CapitalLedgerId = capital.Id;

        // Bank of India — Current Account: Opening Debit = ₹7,00,000
        var bank = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.BankGroupId,
            Name: "Bank of India — Current Account",
            OpeningBalance: 700000m,
            OpeningBalanceType: DebitCreditType.Debit));
        Context.BankLedgerId = bank.Id;

        // Update pre-seeded Cash ledger Opening Balance: ₹1,00,000 Debit
        var cashGroup = await _accountGroupRepository.GetByIdAsync(Context.CompanyId, (await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.CashLedgerId))!.GroupId);
        await _ledgerService.UpdateLedgerAsync(new UpdateLedgerDto(
            Id: Context.CashLedgerId,
            CompanyId: Context.CompanyId,
            GroupId: cashGroup!.Id,
            Name: "Cash",
            OpeningBalance: 100000m,
            OpeningBalanceType: DebitCreditType.Debit));

        // Debtors
        var raj = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.DebtorGroupId,
            Name: "Raj Electronics",
            OpeningBalance: 75000m,
            OpeningBalanceType: DebitCreditType.Debit));
        Context.RajLedgerId = raj.Id;

        var apex = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.DebtorGroupId,
            Name: "Apex Computers",
            OpeningBalance: 50000m,
            OpeningBalanceType: DebitCreditType.Debit));
        Context.ApexLedgerId = apex.Id;

        var shivam = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.DebtorGroupId,
            Name: "Shivam Traders",
            OpeningBalance: 0m,
            OpeningBalanceType: DebitCreditType.Debit));
        Context.ShivamLedgerId = shivam.Id;

        // Creditors (Bill-Wise Enabled)
        var yashika = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.CreditorGroupId,
            Name: "Yashika Pvt Ltd.",
            OpeningBalance: 80000m,
            OpeningBalanceType: DebitCreditType.Credit,
            BillWiseEnabled: true));
        Context.YashikaLedgerId = yashika.Id;

        var techWorld = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.CreditorGroupId,
            Name: "TechWorld Distributors",
            OpeningBalance: 45000m,
            OpeningBalanceType: DebitCreditType.Credit,
            BillWiseEnabled: true));
        Context.TechWorldLedgerId = techWorld.Id;

        var globalSupplies = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.CreditorGroupId,
            Name: "Global Computer Supplies",
            OpeningBalance: 0m,
            OpeningBalanceType: DebitCreditType.Credit,
            BillWiseEnabled: true));
        Context.GlobalSuppliesLedgerId = globalSupplies.Id;

        // Expenses
        var officeExp = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.IndirectExpGroupId,
            Name: "Office Expenses",
            OpeningBalance: 0m));
        Context.OfficeExpLedgerId = officeExp.Id;

        // Freight & Forwarding Charges: reuse if pre-seeded, otherwise create under Indirect Expenses
        if (Context.FreightLedgerId == Guid.Empty)
        {
            var freight = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
                CompanyId: Context.CompanyId,
                GroupId: Context.IndirectExpGroupId,
                Name: "Freight & Forwarding Charges",
                OpeningBalance: 0m));
            Context.FreightLedgerId = freight.Id;
        }

        var bankCharges = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: Context.CompanyId,
            GroupId: Context.IndirectExpGroupId,
            Name: "Bank Charges",
            OpeningBalance: 0m));
        Context.BankChargesLedgerId = bankCharges.Id;
    }

    public async Task RunStage03_InventoryMastersAsync()
    {
        EnsureSafeTarget();
        // Units
        var pcs = await _unitService.CreateUnitAsync(new CreateUnitDto(Context.CompanyId, "Pcs", "Pieces", 0));
        Context.PcsUnitId = pcs.Id;

        var nos = await _unitService.CreateUnitAsync(new CreateUnitDto(Context.CompanyId, "NOS", "Numbers", 0));
        Context.NosUnitId = nos.Id;

        var box = await _unitService.CreateUnitAsync(new CreateUnitDto(Context.CompanyId, "Box", "Boxes", 0));
        Context.BoxUnitId = box.Id;

        // Item Groups: Computer Accessories root, with Monitors, Laptops, Networking children
        var rootGroup = await _itemGroupService.CreateGroupAsync(new CreateItemGroupDto(Context.CompanyId, "Computer Accessories"));
        Context.AccessoriesGroupId = rootGroup.Id;

        var monitorsGroup = await _itemGroupService.CreateGroupAsync(new CreateItemGroupDto(Context.CompanyId, "Monitors", rootGroup.Id));
        Context.MonitorsGroupId = monitorsGroup.Id;

        var laptopsGroup = await _itemGroupService.CreateGroupAsync(new CreateItemGroupDto(Context.CompanyId, "Laptops", rootGroup.Id));
        Context.LaptopsGroupId = laptopsGroup.Id;

        var netGroup = await _itemGroupService.CreateGroupAsync(new CreateItemGroupDto(Context.CompanyId, "Networking", rootGroup.Id));
        Context.NetworkingGroupId = netGroup.Id;

        // Items with opening stock values
        var item1 = await _itemService.CreateItemAsync(new CreateItemDto(
            CompanyId: Context.CompanyId,
            ItemGroupId: monitorsGroup.Id,
            UnitId: pcs.Id,
            Name: "Samsung M21' Monitor",
            PurchasePrice: 5500m,
            OpeningStockQuantity: 10m,
            OpeningStockRate: 5000m,
            OpeningStockValue: 50000m));
        Context.SamsungMonitorItemId = item1.Id;

        var item2 = await _itemService.CreateItemAsync(new CreateItemDto(
            CompanyId: Context.CompanyId,
            ItemGroupId: monitorsGroup.Id,
            UnitId: pcs.Id,
            Name: "Dell 24-inch Monitor",
            PurchasePrice: 7500m,
            OpeningStockQuantity: 5m,
            OpeningStockRate: 7000m,
            OpeningStockValue: 35000m));
        Context.DellMonitorItemId = item2.Id;

        var item3 = await _itemService.CreateItemAsync(new CreateItemDto(
            CompanyId: Context.CompanyId,
            ItemGroupId: laptopsGroup.Id,
            UnitId: nos.Id,
            Name: "Lenovo Business Laptop",
            PurchasePrice: 40000m,
            OpeningStockQuantity: 3m,
            OpeningStockRate: 38000m,
            OpeningStockValue: 114000m));
        Context.LenovoLaptopItemId = item3.Id;

        var item4 = await _itemService.CreateItemAsync(new CreateItemDto(
            CompanyId: Context.CompanyId,
            ItemGroupId: laptopsGroup.Id,
            UnitId: nos.Id,
            Name: "HP Business Laptop",
            PurchasePrice: 48000m,
            OpeningStockQuantity: 2m,
            OpeningStockRate: 45000m,
            OpeningStockValue: 90000m));
        Context.HpLaptopItemId = item4.Id;

        var item5 = await _itemService.CreateItemAsync(new CreateItemDto(
            CompanyId: Context.CompanyId,
            ItemGroupId: netGroup.Id,
            UnitId: pcs.Id,
            Name: "TP-Link Router",
            PurchasePrice: 2500m,
            OpeningStockQuantity: 10m,
            OpeningStockRate: 2000m,
            OpeningStockValue: 20000m));
        Context.TpLinkRouterItemId = item5.Id;

        var item6 = await _itemService.CreateItemAsync(new CreateItemDto(
            CompanyId: Context.CompanyId,
            ItemGroupId: netGroup.Id,
            UnitId: pcs.Id,
            Name: "D-Link 8 Port Switch",
            PurchasePrice: 3200m,
            OpeningStockQuantity: 5m,
            OpeningStockRate: 2800m,
            OpeningStockValue: 14000m));
        Context.DLinkSwitchItemId = item6.Id;
    }

    public async Task RunStage04_OpeningBalancesAsync()
    {
        EnsureSafeTarget();
        var cap = await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.CapitalLedgerId);
        cap!.OpeningBalance.Amount.Should().Be(1123000m);
        cap.OpeningBalanceType.Should().Be(DebitCreditType.Credit);

        var bank = await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.BankLedgerId);
        bank!.OpeningBalance.Amount.Should().Be(700000m);
        bank.OpeningBalanceType.Should().Be(DebitCreditType.Debit);

        var cash = await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.CashLedgerId);
        cash!.OpeningBalance.Amount.Should().Be(100000m);
        cash.OpeningBalanceType.Should().Be(DebitCreditType.Debit);

        var raj = await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.RajLedgerId);
        raj!.OpeningBalance.Amount.Should().Be(75000m);
        raj.OpeningBalanceType.Should().Be(DebitCreditType.Debit);

        var apex = await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.ApexLedgerId);
        apex!.OpeningBalance.Amount.Should().Be(50000m);
        apex.OpeningBalanceType.Should().Be(DebitCreditType.Debit);

        var yash = await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.YashikaLedgerId);
        yash!.OpeningBalance.Amount.Should().Be(80000m);
        yash.OpeningBalanceType.Should().Be(DebitCreditType.Credit);

        var tech = await _ledgerRepository.GetByIdAsync(Context.CompanyId, Context.TechWorldLedgerId);
        tech!.OpeningBalance.Amount.Should().Be(45000m);
        tech.OpeningBalanceType.Should().Be(DebitCreditType.Credit);
    }

    public async Task RunStage05_OpeningStockAndBillReferencesAsync()
    {
        EnsureSafeTarget();
        // Opening Bill References
        var yashOpeningRef = await _billWiseService.CreateBillReferenceAsync(new CreateBillReferenceDto(
            CompanyId: Context.CompanyId,
            LedgerId: Context.YashikaLedgerId,
            ReferenceNumber: "OPEN-YASHIKA-001",
            ReferenceType: BillReferenceType.OpeningRef,
            BillDate: new DateOnly(2023, 4, 1),
            OriginalAmount: 80000m,
            EntryType: DebitCreditType.Credit));
        Context.BillReferences.Add(yashOpeningRef);

        var techOpeningRef = await _billWiseService.CreateBillReferenceAsync(new CreateBillReferenceDto(
            CompanyId: Context.CompanyId,
            LedgerId: Context.TechWorldLedgerId,
            ReferenceNumber: "OPEN-TECHWORLD-001",
            ReferenceType: BillReferenceType.OpeningRef,
            BillDate: new DateOnly(2023, 4, 1),
            OriginalAmount: 45000m,
            EntryType: DebitCreditType.Credit));
        Context.BillReferences.Add(techOpeningRef);

        // Opening Stock Movements
        var openingMovements = new List<StockMovement>
        {
            new(Guid.NewGuid(), Context.CompanyId, Context.SamsungMonitorItemId, new DateOnly(2023, 4, 1),
                StockMovementType.OpeningStock, "OpeningStock", Context.SamsungMonitorItemId, 10m, Context.PcsUnitId,
                Money.FromDecimal(5000m), "Main Store"),
            new(Guid.NewGuid(), Context.CompanyId, Context.DellMonitorItemId, new DateOnly(2023, 4, 1),
                StockMovementType.OpeningStock, "OpeningStock", Context.DellMonitorItemId, 5m, Context.PcsUnitId,
                Money.FromDecimal(7000m), "Main Store"),
            new(Guid.NewGuid(), Context.CompanyId, Context.LenovoLaptopItemId, new DateOnly(2023, 4, 1),
                StockMovementType.OpeningStock, "OpeningStock", Context.LenovoLaptopItemId, 3m, Context.NosUnitId,
                Money.FromDecimal(38000m), "Main Store"),
            new(Guid.NewGuid(), Context.CompanyId, Context.HpLaptopItemId, new DateOnly(2023, 4, 1),
                StockMovementType.OpeningStock, "OpeningStock", Context.HpLaptopItemId, 2m, Context.NosUnitId,
                Money.FromDecimal(45000m), "Main Store"),
            new(Guid.NewGuid(), Context.CompanyId, Context.TpLinkRouterItemId, new DateOnly(2023, 4, 1),
                StockMovementType.OpeningStock, "OpeningStock", Context.TpLinkRouterItemId, 10m, Context.PcsUnitId,
                Money.FromDecimal(2000m), "Main Store"),
            new(Guid.NewGuid(), Context.CompanyId, Context.DLinkSwitchItemId, new DateOnly(2023, 4, 1),
                StockMovementType.OpeningStock, "OpeningStock", Context.DLinkSwitchItemId, 5m, Context.PcsUnitId,
                Money.FromDecimal(2800m), "Main Store"),
        };

        await _stockMovementRepository.AddRangeAsync(openingMovements);

        // Verify initial opening quantities
        (await _stockMovementRepository.GetCurrentQuantityAsync(Context.CompanyId, Context.SamsungMonitorItemId)).Should().Be(10m);
        (await _stockMovementRepository.GetCurrentQuantityAsync(Context.CompanyId, Context.DellMonitorItemId)).Should().Be(5m);
        (await _stockMovementRepository.GetCurrentQuantityAsync(Context.CompanyId, Context.LenovoLaptopItemId)).Should().Be(3m);
        (await _stockMovementRepository.GetCurrentQuantityAsync(Context.CompanyId, Context.HpLaptopItemId)).Should().Be(2m);
        (await _stockMovementRepository.GetCurrentQuantityAsync(Context.CompanyId, Context.TpLinkRouterItemId)).Should().Be(10m);
        (await _stockMovementRepository.GetCurrentQuantityAsync(Context.CompanyId, Context.DLinkSwitchItemId)).Should().Be(5m);
    }

    public async Task RunStage06_Day1to3PurchasesAsync()
    {
        EnsureSafeTarget();
        // T1: 03-Apr-2023 - Purchase 1 from Yashika (₹68,000)
        var p1 = await _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/YASH/001",
            InvoiceDate: new DateOnly(2023, 4, 3),
            SupplierId: Context.YashikaLedgerId,
            Series: "Main",
            MaterialCentre: "Main Store",
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 5m, Context.PcsUnitId, 5500m),
                new(Context.LenovoLaptopItemId, 1m, Context.NosUnitId, 40000m)
            },
            Sundries: new List<CreatePurchaseInvoiceSundryDto>
            {
                new(Context.FreightLedgerId, null, 500m)
            }));
        Context.PurchaseInvoices.Add(p1);
        p1.GrandTotal.Should().Be(68000m);

        // T2: 04-Apr-2023 - Purchase 2 from TechWorld (₹86,000)
        var p2 = await _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/TECH/001",
            InvoiceDate: new DateOnly(2023, 4, 4),
            SupplierId: Context.TechWorldLedgerId,
            Series: "Main",
            MaterialCentre: "Main Store",
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.DellMonitorItemId, 8m, Context.PcsUnitId, 7500m),
                new(Context.TpLinkRouterItemId, 10m, Context.PcsUnitId, 2500m)
            },
            Sundries: new List<CreatePurchaseInvoiceSundryDto>
            {
                new(Context.FreightLedgerId, null, 1000m)
            }));
        Context.PurchaseInvoices.Add(p2);
        p2.GrandTotal.Should().Be(86000m);

        // T3: 05-Apr-2023 - Purchase 3 from Global Computer Supplies (₹1,61,500)
        var p3 = await _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/GLOB/001",
            InvoiceDate: new DateOnly(2023, 4, 5),
            SupplierId: Context.GlobalSuppliesLedgerId,
            Series: "Main",
            MaterialCentre: "Main Store",
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.HpLaptopItemId, 3m, Context.NosUnitId, 48000m),
                new(Context.DLinkSwitchItemId, 5m, Context.PcsUnitId, 3200m)
            },
            Sundries: new List<CreatePurchaseInvoiceSundryDto>
            {
                new(Context.FreightLedgerId, null, 1500m)
            }));
        Context.PurchaseInvoices.Add(p3);
        p3.GrandTotal.Should().Be(161500m);
    }

    public async Task RunStage07_Day4to5PaymentsAsync()
    {
        EnsureSafeTarget();
        // T4: 06-Apr-2023 - Payment to Yashika (₹30,000 via Bank of India)
        var pay1 = await _postingService.PostVoucherAsync(new CreateVoucherDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            VoucherNumber: "PAY/2023/001",
            Type: VoucherType.Payment,
            VoucherDate: new DateOnly(2023, 4, 6),
            ReferenceNumber: null,
            Narration: "Partial payment to Yashika Pvt Ltd",
            Lines: new List<CreateVoucherLineDto>
            {
                new(Context.YashikaLedgerId, DebitCreditType.Debit, 30000m),
                new(Context.BankLedgerId, DebitCreditType.Credit, 30000m)
            }));
        Context.Vouchers.Add(pay1);

        // Allocate against PUR/YASH/001
        var yashPur1Bill = await _billReferenceRepository.GetByReferenceNumberAsync(Context.CompanyId, Context.YashikaLedgerId, "PUR/YASH/001");
        yashPur1Bill.Should().NotBeNull();
        var yashDebLine = pay1.Lines.First(l => l.LedgerId == Context.YashikaLedgerId);
        var alloc1 = await _billWiseService.AllocateBillAsync(new CreateBillAllocationDto(
            CompanyId: Context.CompanyId,
            VoucherLineId: yashDebLine.Id,
            BillReferenceId: yashPur1Bill!.Id,
            AllocatedAmount: 30000m));
        Context.BillAllocations.Add(alloc1);

        // T5: 07-Apr-2023 - Payment to TechWorld (₹50,000 via Bank of India)
        var pay2 = await _postingService.PostVoucherAsync(new CreateVoucherDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            VoucherNumber: "PAY/2023/002",
            Type: VoucherType.Payment,
            VoucherDate: new DateOnly(2023, 4, 7),
            ReferenceNumber: null,
            Narration: "Payment to TechWorld Distributors",
            Lines: new List<CreateVoucherLineDto>
            {
                new(Context.TechWorldLedgerId, DebitCreditType.Debit, 50000m),
                new(Context.BankLedgerId, DebitCreditType.Credit, 50000m)
            }));
        Context.Vouchers.Add(pay2);

        // Allocate against PUR/TECH/001
        var techPur1Bill = await _billReferenceRepository.GetByReferenceNumberAsync(Context.CompanyId, Context.TechWorldLedgerId, "PUR/TECH/001");
        techPur1Bill.Should().NotBeNull();
        var techDebLine = pay2.Lines.First(l => l.LedgerId == Context.TechWorldLedgerId);
        var alloc2 = await _billWiseService.AllocateBillAsync(new CreateBillAllocationDto(
            CompanyId: Context.CompanyId,
            VoucherLineId: techDebLine.Id,
            BillReferenceId: techPur1Bill!.Id,
            AllocatedAmount: 50000m));
        Context.BillAllocations.Add(alloc2);
    }

    public async Task RunStage08_Day6to7ReceiptsAsync()
    {
        EnsureSafeTarget();
        // T6: 08-Apr-2023 - Customer Receipt from Raj Electronics (₹40,000 into Bank)
        var rec1 = await _postingService.PostVoucherAsync(new CreateVoucherDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            VoucherNumber: "REC/2023/001",
            Type: VoucherType.Receipt,
            VoucherDate: new DateOnly(2023, 4, 8),
            ReferenceNumber: null,
            Narration: "Receipt from Raj Electronics",
            Lines: new List<CreateVoucherLineDto>
            {
                new(Context.BankLedgerId, DebitCreditType.Debit, 40000m),
                new(Context.RajLedgerId, DebitCreditType.Credit, 40000m)
            }));
        Context.Vouchers.Add(rec1);

        // T7: 09-Apr-2023 - Customer Receipt from Apex Computers (₹20,000 into Cash)
        var rec2 = await _postingService.PostVoucherAsync(new CreateVoucherDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            VoucherNumber: "REC/2023/002",
            Type: VoucherType.Receipt,
            VoucherDate: new DateOnly(2023, 4, 9),
            ReferenceNumber: null,
            Narration: "Receipt from Apex Computers",
            Lines: new List<CreateVoucherLineDto>
            {
                new(Context.CashLedgerId, DebitCreditType.Debit, 20000m),
                new(Context.ApexLedgerId, DebitCreditType.Credit, 20000m)
            }));
        Context.Vouchers.Add(rec2);
    }

    public async Task RunStage09_Day8to9JournalAndPaymentAsync()
    {
        EnsureSafeTarget();
        // T8: 10-Apr-2023 - Office Expenses against Cash (₹12,500 via Journal)
        var jrn = await _postingService.PostVoucherAsync(new CreateVoucherDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            VoucherNumber: "JRN/2023/001",
            Type: VoucherType.Journal,
            VoucherDate: new DateOnly(2023, 4, 10),
            ReferenceNumber: null,
            Narration: "Office expenses paid in cash",
            Lines: new List<CreateVoucherLineDto>
            {
                new(Context.OfficeExpLedgerId, DebitCreditType.Debit, 12500m),
                new(Context.CashLedgerId, DebitCreditType.Credit, 12500m)
            }));
        Context.Vouchers.Add(jrn);

        // T9: 11-Apr-2023 - Bank Charges against Bank (₹2,500 via Payment)
        var payCharges = await _postingService.PostVoucherAsync(new CreateVoucherDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            VoucherNumber: "PAY/2023/003",
            Type: VoucherType.Payment,
            VoucherDate: new DateOnly(2023, 4, 11),
            ReferenceNumber: null,
            Narration: "Bank charges debited by Bank of India",
            Lines: new List<CreateVoucherLineDto>
            {
                new(Context.BankChargesLedgerId, DebitCreditType.Debit, 2500m),
                new(Context.BankLedgerId, DebitCreditType.Credit, 2500m)
            }));
        Context.Vouchers.Add(payCharges);
    }

    public async Task RunStage10_Day10PurchaseAsync()
    {
        EnsureSafeTarget();
        // T10: 12-Apr-2023 - Second Purchase from Yashika (₹69,500)
        var p4 = await _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/YASH/002",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Series: "Main",
            MaterialCentre: "Main Store",
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 4m, Context.PcsUnitId, 5400m),
                new(Context.HpLaptopItemId, 1m, Context.NosUnitId, 47500m)
            },
            Sundries: new List<CreatePurchaseInvoiceSundryDto>
            {
                new(Context.FreightLedgerId, null, 400m)
            }));
        Context.PurchaseInvoices.Add(p4);
        p4.GrandTotal.Should().Be(69500m);
    }

    public async Task RunStage11_ComprehensiveReconciliationAsync()
    {
        EnsureSafeTarget();
        await Reconciler.VerifyAccountingEqualityAsync(Context.CompanyId);
        await Reconciler.VerifyCashReconciliationAsync(Context.CompanyId, Context.CashLedgerId);
        await Reconciler.VerifyBankReconciliationAsync(Context.CompanyId, Context.BankLedgerId);
        await Reconciler.VerifyCustomerReconciliationAsync(Context.CompanyId, Context.RajLedgerId, Context.ApexLedgerId);
        await Reconciler.VerifySupplierAndBillWiseReconciliationAsync(Context.CompanyId, Context.YashikaLedgerId, Context.TechWorldLedgerId, Context.GlobalSuppliesLedgerId);
        await Reconciler.VerifyPurchasesAndFreightReconciliationAsync(Context.CompanyId, Context.PurchaseLedgerId, Context.FreightLedgerId);
        await Reconciler.VerifyPhysicalInventoryReconciliationAsync(Context.CompanyId, Context);
    }

    public async Task RunStage12_NegativeTestsAsync()
    {
        EnsureSafeTarget();
        // N1: Duplicate purchase invoice number
        var actN1 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/YASH/001",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m)
            }));
        await actN1.Should().ThrowAsync<DomainException>("N1: Duplicate purchase invoice number must be rejected");

        // N2: Cross-company supplier
        var fakeCompanyId = Guid.NewGuid();
        var actN2 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/NEG/002",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Guid.NewGuid(), // Non-existent or other company
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m)
            }));
        await actN2.Should().ThrowAsync<DomainException>("N2: Cross-company/missing supplier must be rejected");

        // N3: Cross-company item
        var actN3 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/NEG/003",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Guid.NewGuid(), 1m, Context.PcsUnitId, 5000m)
            }));
        await actN3.Should().ThrowAsync<DomainException>("N3: Cross-company/missing item must be rejected");

        // N4: Locked financial year
        await using (var conn = (Microsoft.Data.Sqlite.SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE FinancialYears SET IsLocked = 1 WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", Context.FinancialYearId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        var actN4 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/NEG/004",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m)
            }));
        await actN4.Should().ThrowAsync<DomainException>("N4: Transaction in locked FY must be rejected");

        // Unlock FY for remaining checks
        await using (var conn = (Microsoft.Data.Sqlite.SqliteConnection)await _connectionFactory.CreateConnectionAsync())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE FinancialYears SET IsLocked = 0 WHERE Id = @Id;";
            cmd.Parameters.AddWithValue("@Id", Context.FinancialYearId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        // N5: Unbalanced voucher
        var actN5 = () => _postingService.PostVoucherAsync(new CreateVoucherDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            VoucherNumber: "JRN/UNBALANCED/01",
            Type: VoucherType.Journal,
            VoucherDate: new DateOnly(2023, 4, 12),
            ReferenceNumber: null,
            Narration: "Unbalanced voucher test",
            Lines: new List<CreateVoucherLineDto>
            {
                new(Context.OfficeExpLedgerId, DebitCreditType.Debit, 1000m),
                new(Context.CashLedgerId, DebitCreditType.Credit, 500m)
            }));
        await actN5.Should().ThrowAsync<DomainException>("N5: Unbalanced voucher must be rejected");

        // N6: Quantity = 0
        var actN6 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/NEG/006",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 0m, Context.PcsUnitId, 5000m)
            }));
        await actN6.Should().ThrowAsync<DomainException>("N6: Zero quantity must be rejected");

        // N7: Negative quantity
        var actN7 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/NEG/007",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, -5m, Context.PcsUnitId, 5000m)
            }));
        await actN7.Should().ThrowAsync<DomainException>("N7: Negative quantity must be rejected");

        // N8: Missing Purchase ledger (simulated in a separate isolated company/scope)
        var dummyComp = Company.Create("No Purchase Co", "No Purchase Co", null, "Gujarat", "24", new DateOnly(2023, 4, 1), new DateOnly(2023, 4, 1));
        await _companyRepository.AddAsync(dummyComp);
        var dummyFy = dummyComp.FinancialYears.First();
        var dummyGroup = AccountGroup.Create(dummyComp.Id, "Sundry Creditors", GroupNature.Liability, isPrimary: true);
        await _accountGroupRepository.AddAsync(dummyGroup);
        var dummySupp = new Ledger(Guid.NewGuid(), dummyComp.Id, dummyGroup.Id, "Dummy Supp");
        await _ledgerRepository.AddAsync(dummySupp);
        var dummyUnit = new Unit(Guid.NewGuid(), dummyComp.Id, "Pcs", "Pieces", 0);
        await _unitRepository.AddAsync(dummyUnit);
        var dummyItemGroup = new ItemGroup(Guid.NewGuid(), dummyComp.Id, "General");
        await _itemGroupRepository.AddAsync(dummyItemGroup);
        var dummyItem = new Item(Guid.NewGuid(), dummyComp.Id, dummyItemGroup.Id, dummyUnit.Id, "Dummy Item");
        await _itemRepository.AddAsync(dummyItem);

        var actN8 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: dummyComp.Id,
            FinancialYearId: dummyFy.Id,
            InvoiceNumber: "PUR/NOPUR/01",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: dummySupp.Id,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(dummyItem.Id, 1m, dummyUnit.Id, 100m)
            }));
        await actN8.Should().ThrowAsync<DomainException>("N8: Missing Purchase ledger must be rejected");

        // N9: Cross-company sundry ledger
        var actN9 = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/NEG/009",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m)
            },
            Sundries: new List<CreatePurchaseInvoiceSundryDto>
            {
                new(Guid.NewGuid(), null, 500m) // Cross-company/missing sundry ledger
            }));
        await actN9.Should().ThrowAsync<DomainException>("N9: Cross-company sundry ledger must be rejected");
    }

    public async Task RunStage13_AtomicityRollbackTestsAsync()
    {
        EnsureSafeTarget();
        var userService = new StandaloneCurrentUserService();
        var dateProvider = new DateTimeProvider();

        // Failure Point A: Voucher persistence fails
        var faultyVoucherRepo = new FaultyTestRepositories.FaultyVoucherRepository(_voucherRepository, throwOnAdd: true);
        var faultyPostingService = new PostingService(
            faultyVoucherRepo, _companyRepository, _financialYearRepository, _ledgerRepository, userService, dateProvider);
        var serviceA = new PurchaseService(
            _connectionFactory, _purchaseInvoiceRepository, _stockMovementRepository, faultyVoucherRepo,
            faultyPostingService, _billWiseService, _companyRepository, _financialYearRepository,
            _ledgerRepository, _accountGroupRepository, _itemRepository, _unitRepository, userService, dateProvider);

        var actA = () => serviceA.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId, FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/TX/FAIL-A", InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m) }));
        await actA.Should().ThrowAsync<InvalidOperationException>();
        await AssertZeroRowsForInvoiceAsync("PUR/TX/FAIL-A");

        // Failure Point B: BillReference persistence fails
        var faultyBillRepo = new FaultyTestRepositories.FaultyBillReferenceRepository(_billReferenceRepository, throwOnAdd: true);
        var faultyBillService = new BillWiseService(
            faultyBillRepo, _billAllocationRepository, _voucherRepository, _companyRepository, _ledgerRepository, userService, dateProvider);
        var serviceB = new PurchaseService(
            _connectionFactory, _purchaseInvoiceRepository, _stockMovementRepository, _voucherRepository,
            _postingService, faultyBillService, _companyRepository, _financialYearRepository,
            _ledgerRepository, _accountGroupRepository, _itemRepository, _unitRepository, userService, dateProvider);

        var actB = () => serviceB.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId, FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/TX/FAIL-B", InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m) }));
        await actB.Should().ThrowAsync<InvalidOperationException>();
        await AssertZeroRowsForInvoiceAsync("PUR/TX/FAIL-B");

        // Failure Point C: PurchaseInvoice persistence fails
        var faultyInvoiceRepo = new FaultyTestRepositories.FaultyPurchaseInvoiceRepository(_purchaseInvoiceRepository, throwOnAdd: true);
        var serviceC = new PurchaseService(
            _connectionFactory, faultyInvoiceRepo, _stockMovementRepository, _voucherRepository,
            _postingService, _billWiseService, _companyRepository, _financialYearRepository,
            _ledgerRepository, _accountGroupRepository, _itemRepository, _unitRepository, userService, dateProvider);

        var actC = () => serviceC.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId, FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/TX/FAIL-C", InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m) }));
        await actC.Should().ThrowAsync<InvalidOperationException>();
        await AssertZeroRowsForInvoiceAsync("PUR/TX/FAIL-C");

        // Failure Point D: StockMovement persistence fails
        var faultyStockRepo = new FaultyTestRepositories.FaultyStockMovementRepository(_stockMovementRepository, throwOnAdd: true);
        var serviceD = new PurchaseService(
            _connectionFactory, _purchaseInvoiceRepository, faultyStockRepo, _voucherRepository,
            _postingService, _billWiseService, _companyRepository, _financialYearRepository,
            _ledgerRepository, _accountGroupRepository, _itemRepository, _unitRepository, userService, dateProvider);

        var actD = () => serviceD.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId, FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/TX/FAIL-D", InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: Context.YashikaLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m) }));
        await actD.Should().ThrowAsync<InvalidOperationException>();
        await AssertZeroRowsForInvoiceAsync("PUR/TX/FAIL-D");
    }

    private async Task AssertZeroRowsForInvoiceAsync(string invoiceNumber)
    {
        await using var conn = (Microsoft.Data.Sqlite.SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoices WHERE InvoiceNumber = @Num;";
            cmd.Parameters.AddWithValue("@Num", invoiceNumber);
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM Vouchers WHERE VoucherNumber = @Num;";
            cmd.Parameters.AddWithValue("@Num", invoiceNumber);
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM BillReferences WHERE ReferenceNumber = @Num;";
            cmd.Parameters.AddWithValue("@Num", invoiceNumber);
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);
        }
    }

    public async Task RunStage14_IsolationAndDatabaseIntegrityAsync()
    {
        EnsureSafeTarget();
        // 1. Company Isolation Test: Company B
        var compB = await _companyService.CreateCompanyAsync(
            companyName: "Test Electronics Pvt Ltd.",
            printName: "Test Electronics Pvt Ltd.",
            shortName: "TEPL",
            stateName: "Maharashtra",
            stateCode: "27",
            financialYearBeginning: new DateOnly(2023, 4, 1),
            booksBeginningFrom: new DateOnly(2023, 4, 1));

        var groupsB = await _accountGroupRepository.GetAllAsync(compB.Id);
        var creditorGroupB = groupsB.First(g => g.Name.Equals("Sundry Creditors", StringComparison.OrdinalIgnoreCase));
        var suppB = await _ledgerService.CreateLedgerAsync(new CreateLedgerDto(
            CompanyId: compB.Id,
            GroupId: creditorGroupB.Id,
            Name: "Supplier B Only",
            BillWiseEnabled: true));

        // Attempt cross-company operation: Use Supplier B in Company A's purchase
        var actCrossComp = () => _purchaseService.CreatePurchaseInvoiceAsync(new CreatePurchaseInvoiceDto(
            CompanyId: Context.CompanyId,
            FinancialYearId: Context.FinancialYearId,
            InvoiceNumber: "PUR/CROSS/01",
            InvoiceDate: new DateOnly(2023, 4, 12),
            SupplierId: suppB.Id, // Belonging to Company B
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(Context.SamsungMonitorItemId, 1m, Context.PcsUnitId, 5000m)
            }));
        await actCrossComp.Should().ThrowAsync<DomainException>("Cross-company supplier must be rejected");

        // Verify Company B state unaffected
        var compBSuppliers = await _ledgerRepository.GetAllAsync(compB.Id);
        compBSuppliers.Should().Contain(l => l.Name == "Supplier B Only");

        // 2. Financial Year Isolation Test
        var existingFy = await _financialYearRepository.GetByIdAsync(Context.FinancialYearId);
        existingFy.Should().NotBeNull();
        // Voucher uniqueness per company + FY boundary verified in N1 and PostVoucherAsync

        // 3. Database Integrity Audit Queries
        await using var conn = (Microsoft.Data.Sqlite.SqliteConnection)await _connectionFactory.CreateConnectionAsync();

        // 0 orphan VoucherLines
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM VoucherLines WHERE VoucherId NOT IN (SELECT Id FROM Vouchers);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "0 orphan VoucherLines");
        }

        // 0 orphan BillReferences
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM BillReferences WHERE SourceVoucherLineId IS NOT NULL AND SourceVoucherLineId NOT IN (SELECT Id FROM VoucherLines);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "0 orphan BillReferences");
        }

        // 0 orphan BillAllocations
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM BillAllocations WHERE VoucherLineId NOT IN (SELECT Id FROM VoucherLines) OR BillReferenceId NOT IN (SELECT Id FROM BillReferences);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "0 orphan BillAllocations");
        }

        // 0 orphan PurchaseInvoiceItemLines
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoiceItemLines WHERE PurchaseInvoiceId NOT IN (SELECT Id FROM PurchaseInvoices);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "0 orphan PurchaseInvoiceItemLines");
        }

        // 0 orphan PurchaseInvoiceSundryLines
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoiceSundryLines WHERE PurchaseInvoiceId NOT IN (SELECT Id FROM PurchaseInvoices);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "0 orphan PurchaseInvoiceSundryLines");
        }

        // 0 orphan StockMovements
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM StockMovements WHERE SourceDocumentType = 'PurchaseInvoice' AND SourceDocumentId NOT IN (SELECT Id FROM PurchaseInvoices);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0, "0 orphan StockMovements");
        }

        // 0 unbalanced posted vouchers
        var allVouchers = await _voucherRepository.GetAllAsync(Context.CompanyId);
        foreach (var v in allVouchers)
        {
            v.IsBalanced.Should().BeTrue($"Voucher {v.VoucherNumber} must be balanced");
        }

        // 0 negative physical inventory quantities
        var allItems = await _itemRepository.GetAllAsync(Context.CompanyId);
        foreach (var item in allItems)
        {
            var qty = await _stockMovementRepository.GetCurrentQuantityAsync(Context.CompanyId, item.Id);
            qty.Should().BeGreaterThanOrEqualTo(0, $"Item {item.Name} must not have negative inventory");
        }
    }
}
