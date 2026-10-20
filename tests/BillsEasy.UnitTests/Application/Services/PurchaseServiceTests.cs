using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class PurchaseServiceTests : IDisposable
{
    private readonly ISqliteDbConnectionFactory _connectionFactory = Substitute.For<ISqliteDbConnectionFactory>();
    private readonly IPurchaseInvoiceRepository _purchaseInvoiceRepo = Substitute.For<IPurchaseInvoiceRepository>();
    private readonly IStockMovementRepository _stockMovementRepo = Substitute.For<IStockMovementRepository>();
    private readonly IVoucherRepository _voucherRepo = Substitute.For<IVoucherRepository>();
    private readonly IPostingService _postingService = Substitute.For<IPostingService>();
    private readonly IBillWiseService _billWiseService = Substitute.For<IBillWiseService>();
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly IFinancialYearRepository _financialYearRepo = Substitute.For<IFinancialYearRepository>();
    private readonly ILedgerRepository _ledgerRepo = Substitute.For<ILedgerRepository>();
    private readonly IAccountGroupRepository _accountGroupRepo = Substitute.For<IAccountGroupRepository>();
    private readonly IItemRepository _itemRepo = Substitute.For<IItemRepository>();
    private readonly IUnitRepository _unitRepo = Substitute.For<IUnitRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();

    private readonly PurchaseService _service;
    private readonly SqliteConnection _sqliteConnection;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _financialYearId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _purchaseLedgerId = Guid.NewGuid();
    private readonly Guid _freightLedgerId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();

    private readonly Company _validCompany;
    private readonly FinancialYear _validFinancialYear;
    private readonly Ledger _supplierLedger;
    private readonly Ledger _purchaseLedger;
    private readonly Ledger _freightLedger;
    private readonly Item _validItem;
    private readonly Unit _validUnit;

    public PurchaseServiceTests()
    {
        _sqliteConnection = new SqliteConnection("Data Source=:memory:");
        _sqliteConnection.Open();
        _connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DbConnection>(_sqliteConnection));

        _userService.UserName.Returns("tester");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 5, 10, 10, 0, 0, DateTimeKind.Utc));

        _validCompany = Company.Create(
            "Acme Corp", "Acme Corp", null, "Delhi", "07",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 1));

        _validFinancialYear = new FinancialYear(
            _financialYearId, _companyId, "FY 2026-27",
            new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31),
            isActive: true, isLocked: false);

        var creditorGroup = AccountGroup.Create(_companyId, "Sundry Creditors", GroupNature.Liability, isPrimary: true);
        var purchaseGroup = AccountGroup.Create(_companyId, "Purchase Accounts", GroupNature.Expense, isPrimary: true);
        var expenseGroup = AccountGroup.Create(_companyId, "Indirect Expenses", GroupNature.Expense, isPrimary: true);

        _supplierLedger = new Ledger(_supplierId, _companyId, creditorGroup.Id, "Yashika Pvt Ltd", billWiseEnabled: true);
        _purchaseLedger = new Ledger(_purchaseLedgerId, _companyId, purchaseGroup.Id, "Purchase");
        _freightLedger = new Ledger(_freightLedgerId, _companyId, expenseGroup.Id, "Freight & Forwarding Charges");

        var itemGroup = ItemGroup.Create(_companyId, "Electronics");
        _validUnit = new Unit(_unitId, _companyId, "Pcs", "Pieces", 0);
        _validItem = new Item(_itemId, _companyId, itemGroup.Id, _unitId, "Samsung 19 Inch Monitor");

        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(_validFinancialYear);
        _ledgerRepo.GetByIdAsync(_companyId, _supplierId, Arg.Any<CancellationToken>()).Returns(_supplierLedger);
        _ledgerRepo.GetByIdAsync(_companyId, _freightLedgerId, Arg.Any<CancellationToken>()).Returns(_freightLedger);
        _ledgerRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<Ledger> { _purchaseLedger, _supplierLedger, _freightLedger });
        _accountGroupRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup> { creditorGroup, purchaseGroup, expenseGroup });
        _itemRepo.GetByIdAsync(_companyId, _itemId, Arg.Any<CancellationToken>()).Returns(_validItem);
        _unitRepo.GetByIdAsync(_companyId, _unitId, Arg.Any<CancellationToken>()).Returns(_validUnit);

        _service = new PurchaseService(
            _connectionFactory,
            _purchaseInvoiceRepo,
            _stockMovementRepo,
            _voucherRepo,
            _postingService,
            _billWiseService,
            _companyRepo,
            _financialYearRepo,
            _ledgerRepo,
            _accountGroupRepo,
            _itemRepo,
            _unitRepo,
            _userService,
            _dateProvider);
    }

    public void Dispose()
    {
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WithValidDataAndBillWiseSupplier_SucceedsAndCallsAllServices()
    {
        // Arrange
        var invoiceDate = new DateOnly(2026, 5, 10);
        var voucherLineId = Guid.NewGuid();
        var postedVoucherDto = new VoucherDto(
            Guid.NewGuid(), _companyId, _financialYearId, "INV-001",
            VoucherType.Purchase, VoucherStatus.Posted, invoiceDate,
            "INV-001", "Purchase of monitors", 28000,
            new List<VoucherLineDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), _purchaseLedger.Id, "Purchase", 1, DebitCreditType.Debit, 27500, null),
                new(Guid.NewGuid(), Guid.NewGuid(), _freightLedgerId, "Freight", 2, DebitCreditType.Debit, 500, null),
                new(voucherLineId, Guid.NewGuid(), _supplierId, "Yashika Pvt Ltd", 3, DebitCreditType.Credit, 28000, null)
            },
            _dateProvider.UtcNow, "tester", _dateProvider.UtcNow, "tester");

        _postingService.PostVoucherAsync(Arg.Any<CreateVoucherDto>(), Arg.Any<DbTransaction>(), Arg.Any<CancellationToken>())
            .Returns(postedVoucherDto);

        var dto = new CreatePurchaseInvoiceDto(
            _companyId,
            _financialYearId,
            "INV-001",
            invoiceDate,
            _supplierId,
            Series: "Main",
            MaterialCentre: "Main Store",
            Narration: "Purchase of monitors",
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(_itemId, 5, _unitId, 5500)
            },
            Sundries: new List<CreatePurchaseInvoiceSundryDto>
            {
                new(_freightLedgerId, null, 500)
            },
            Transport: new TransportDetailsDto("Blue Dart", "BD-1234", invoiceDate, "DL-01-A-1234", "Delhi"));

        // Act
        var result = await _service.CreatePurchaseInvoiceAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.InvoiceNumber.Should().Be("INV-001");
        result.GrandTotal.Should().Be(28000);
        result.ItemSubtotal.Should().Be(27500);
        result.SundrySubtotal.Should().Be(500);
        result.SupplierName.Should().Be("Yashika Pvt Ltd");
        result.Transport.Should().NotBeNull();
        result.Transport!.TransportName.Should().Be("Blue Dart");

        // Verify calls
        await _postingService.Received(1).PostVoucherAsync(
            Arg.Is<CreateVoucherDto>(v => v.VoucherNumber == "INV-001" && v.Type == VoucherType.Purchase),
            Arg.Any<DbTransaction>(),
            Arg.Any<CancellationToken>());

        await _billWiseService.Received(1).CreateBillReferenceAsync(
            Arg.Is<CreateBillReferenceDto>(b => b.ReferenceNumber == "INV-001" && b.ReferenceType == BillReferenceType.NewRef && b.OriginalAmount == 28000),
            Arg.Any<DbTransaction>(),
            Arg.Any<CancellationToken>());

        await _purchaseInvoiceRepo.Received(1).AddAsync(
            Arg.Is<PurchaseInvoice>(pi => pi.InvoiceNumber == "INV-001" && pi.GrandTotal.Amount == 28000),
            Arg.Any<DbTransaction>(),
            Arg.Any<CancellationToken>());

        await _stockMovementRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<StockMovement>>(m => System.Linq.Enumerable.Count(m) == 1),
            Arg.Any<DbTransaction>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenSupplierBillWiseDisabled_DoesNotCreateBillReference()
    {
        // Arrange
        var nonBillWiseSupplier = Ledger.Create(_companyId, _supplierLedger.GroupId, "Non BillWise Supplier", billWiseEnabled: false);
        _ledgerRepo.GetByIdAsync(_companyId, _supplierId, Arg.Any<CancellationToken>()).Returns(nonBillWiseSupplier);

        var postedVoucherDto = new VoucherDto(
            Guid.NewGuid(), _companyId, _financialYearId, "INV-002",
            VoucherType.Purchase, VoucherStatus.Posted, new DateOnly(2026, 5, 10),
            "INV-002", null, 5500,
            new List<VoucherLineDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), _purchaseLedger.Id, "Purchase", 1, DebitCreditType.Debit, 5500, null),
                new(Guid.NewGuid(), Guid.NewGuid(), _supplierId, "Non BillWise Supplier", 2, DebitCreditType.Credit, 5500, null)
            },
            _dateProvider.UtcNow, "tester", _dateProvider.UtcNow, "tester");

        _postingService.PostVoucherAsync(Arg.Any<CreateVoucherDto>(), Arg.Any<DbTransaction>(), Arg.Any<CancellationToken>())
            .Returns(postedVoucherDto);

        var dto = new CreatePurchaseInvoiceDto(
            _companyId,
            _financialYearId,
            "INV-002",
            new DateOnly(2026, 5, 10),
            _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(_itemId, 1, _unitId, 5500)
            });

        // Act
        var result = await _service.CreatePurchaseInvoiceAsync(dto);

        // Assert
        result.Should().NotBeNull();
        await _billWiseService.DidNotReceive().CreateBillReferenceAsync(
            Arg.Any<CreateBillReferenceDto>(),
            Arg.Any<DbTransaction>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenFinancialYearLocked_ThrowsDomainException()
    {
        // Arrange
        var lockedFy = new FinancialYear(_financialYearId, _companyId, "FY 2026-27",
            new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31), isActive: true, isLocked: true);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(lockedFy);

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-003", new DateOnly(2026, 5, 10), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*locked*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenDateOutsideFinancialYear_ThrowsDomainException()
    {
        // Arrange
        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-004", new DateOnly(2025, 3, 31), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*outside financial year*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenDuplicateInvoiceNumber_ThrowsDomainException()
    {
        // Arrange
        _purchaseInvoiceRepo.ExistsByInvoiceNumberAsync(_companyId, _financialYearId, "INV-DUPLICATE", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-DUPLICATE", new DateOnly(2026, 5, 10), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenMissingPurchaseLedger_ThrowsDomainException()
    {
        // Arrange
        _ledgerRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(new List<Ledger> { _supplierLedger }); // No Purchase ledger

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-005", new DateOnly(2026, 5, 10), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Purchase ledger*not found*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenNoLines_ThrowsDomainException()
    {
        // Arrange
        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-006", new DateOnly(2026, 5, 10), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto>());

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*at least one item line*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenLineQuantityZero_ThrowsDomainException()
    {
        // Arrange
        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-007", new DateOnly(2026, 5, 10), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 0, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*quantity must be greater than zero*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenLinePriceNegative_ThrowsDomainException()
    {
        // Arrange
        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-008", new DateOnly(2026, 5, 10), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, -500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*price cannot be negative*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenSundryAmountZero_ThrowsDomainException()
    {
        // Arrange
        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-009", new DateOnly(2026, 5, 10), _supplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) },
            Sundries: new List<CreatePurchaseInvoiceSundryDto> { new(_freightLedgerId, null, 0) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Sundry amount must be greater than zero*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenSupplierIsSundryDebtor_ThrowsDomainExceptionWithDescriptiveMessage()
    {
        // Arrange
        var debtorGroup = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset, isPrimary: true);
        var debtorLedgerId = Guid.NewGuid();
        var debtorLedger = new Ledger(debtorLedgerId, _companyId, debtorGroup.Id, "Apex Computers", billWiseEnabled: false);

        _ledgerRepo.GetByIdAsync(_companyId, debtorLedgerId, Arg.Any<CancellationToken>()).Returns(debtorLedger);
        _accountGroupRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup>
        {
            AccountGroup.Create(_companyId, "Sundry Creditors", GroupNature.Liability, isPrimary: true),
            debtorGroup
        });

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-DEBTOR-01", new DateOnly(2026, 5, 10), debtorLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Selected party 'Apex Computers' belongs to account group 'Sundry Debtors' and is not eligible as a purchase supplier*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenSupplierIsCashOrBank_ThrowsDomainExceptionWithDescriptiveMessage()
    {
        // Arrange
        var cashGroup = AccountGroup.Create(_companyId, "Cash-in-hand", GroupNature.Asset, isPrimary: true);
        var cashLedgerId = Guid.NewGuid();
        var cashLedger = new Ledger(cashLedgerId, _companyId, cashGroup.Id, "Cash Account", billWiseEnabled: false);

        _ledgerRepo.GetByIdAsync(_companyId, cashLedgerId, Arg.Any<CancellationToken>()).Returns(cashLedger);
        _accountGroupRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup>
        {
            AccountGroup.Create(_companyId, "Sundry Creditors", GroupNature.Liability, isPrimary: true),
            cashGroup
        });

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-CASH-01", new DateOnly(2026, 5, 10), cashLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Selected party 'Cash Account' belongs to account group 'Cash-in-hand' and is not eligible as a purchase supplier*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenSupplierIsExpenseOrCapital_ThrowsDomainExceptionWithDescriptiveMessage()
    {
        // Arrange
        var capitalGroup = AccountGroup.Create(_companyId, "Capital Account", GroupNature.Capital, isPrimary: true);
        var capitalLedgerId = Guid.NewGuid();
        var capitalLedger = new Ledger(capitalLedgerId, _companyId, capitalGroup.Id, "Owner Equity", billWiseEnabled: false);

        _ledgerRepo.GetByIdAsync(_companyId, capitalLedgerId, Arg.Any<CancellationToken>()).Returns(capitalLedger);
        _accountGroupRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup>
        {
            AccountGroup.Create(_companyId, "Sundry Creditors", GroupNature.Liability, isPrimary: true),
            capitalGroup
        });

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-CAPITAL-01", new DateOnly(2026, 5, 10), capitalLedgerId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Selected party 'Owner Equity' belongs to account group 'Capital Account' and is not eligible as a purchase supplier*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenSupplierIsCrossCompany_ThrowsDomainException()
    {
        // Arrange
        var otherCompanyId = Guid.NewGuid();
        var otherCreditorGroup = AccountGroup.Create(otherCompanyId, "Sundry Creditors", GroupNature.Liability, isPrimary: true);
        var otherSupplierId = Guid.NewGuid();
        var otherSupplier = new Ledger(otherSupplierId, otherCompanyId, otherCreditorGroup.Id, "Other Company Supplier", billWiseEnabled: true);

        _ledgerRepo.GetByIdAsync(_companyId, otherSupplierId, Arg.Any<CancellationToken>()).Returns(otherSupplier);

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-CROSS-01", new DateOnly(2026, 5, 10), otherSupplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act & Assert
        var act = () => _service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Supplier ledger does not belong to the specified company*");
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenSupplierBelongsToCustomSubGroupUnderSundryCreditors_Succeeds()
    {
        // Arrange
        var rootCreditorGroup = AccountGroup.Create(_companyId, "Sundry Creditors", GroupNature.Liability, isPrimary: true);
        var subCreditorGroup = AccountGroup.Create(_companyId, "Raw Material Creditors", GroupNature.Liability, parentGroupId: rootCreditorGroup.Id);
        var subSupplierId = Guid.NewGuid();
        var subSupplier = new Ledger(subSupplierId, _companyId, subCreditorGroup.Id, "Steel Suppliers Ltd", billWiseEnabled: true);

        _ledgerRepo.GetByIdAsync(_companyId, subSupplierId, Arg.Any<CancellationToken>()).Returns(subSupplier);
        _accountGroupRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup>
        {
            rootCreditorGroup,
            subCreditorGroup,
            new AccountGroup(Guid.NewGuid(), _companyId, "Purchase Accounts", GroupNature.Expense, isPrimary: true)
        });

        var now = DateTime.UtcNow;
        var subVoucherDto = new VoucherDto(
            Guid.NewGuid(), _companyId, _financialYearId, "INV-SUB-01",
            VoucherType.Purchase, VoucherStatus.Posted, new DateOnly(2026, 5, 10),
            "INV-SUB-01", null, 500,
            new List<VoucherLineDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), _purchaseLedger.Id, "Purchase", 1, DebitCreditType.Debit, 500, null),
                new(Guid.NewGuid(), Guid.NewGuid(), subSupplierId, "Steel Suppliers Ltd", 2, DebitCreditType.Credit, 500, null)
            },
            now, "tester", now, "tester");

        _postingService.PostVoucherAsync(Arg.Any<CreateVoucherDto>(), Arg.Any<DbTransaction>(), Arg.Any<CancellationToken>())
            .Returns(subVoucherDto);

        var dto = new CreatePurchaseInvoiceDto(
            _companyId, _financialYearId, "INV-SUB-01", new DateOnly(2026, 5, 10), subSupplierId,
            Lines: new List<CreatePurchaseInvoiceItemLineDto> { new(_itemId, 1, _unitId, 500) });

        // Act
        var result = await _service.CreatePurchaseInvoiceAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.SupplierName.Should().Be("Steel Suppliers Ltd");
    }
}
