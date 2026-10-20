using System;
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
using Xunit;

namespace BillsEasy.IntegrationTests.Infrastructure.Persistence;

public class PurchaseInvoiceRepositoryTests : IDisposable
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
    private readonly PurchaseInvoiceRepository _purchaseInvoiceRepository;

    private Guid _companyId = Guid.NewGuid();
    private Guid _financialYearId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _purchaseLedgerId = Guid.NewGuid();
    private readonly Guid _freightLedgerId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Guid _voucherId = Guid.NewGuid();

    public PurchaseInvoiceRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_PIRepoTest_{Guid.NewGuid():N}.db");
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
        _purchaseInvoiceRepository = new PurchaseInvoiceRepository(_connectionFactory);
    }

    private async Task SeedPrerequisitesAsync()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Test Company",
            printName: "Test Company",
            shortName: null,
            stateName: "Delhi",
            stateCode: "07",
            financialYearBeginning: new DateOnly(2026, 4, 1),
            booksBeginningFrom: new DateOnly(2026, 4, 1));
        await _companyRepository.AddAsync(company);

        _companyId = company.Id;
        _financialYearId = company.FinancialYears.First().Id;

        var creditorGroup = new AccountGroup(Guid.NewGuid(), _companyId, "Sundry Creditors", GroupNature.Liability, isPrimary: true);
        var purchaseGroup = new AccountGroup(Guid.NewGuid(), _companyId, "Purchase Accounts", GroupNature.Expense, isPrimary: true);
        var expenseGroup = new AccountGroup(Guid.NewGuid(), _companyId, "Indirect Expenses", GroupNature.Expense, isPrimary: true);
        await _accountGroupRepository.AddAsync(creditorGroup);
        await _accountGroupRepository.AddAsync(purchaseGroup);
        await _accountGroupRepository.AddAsync(expenseGroup);

        var supplier = new Ledger(_supplierId, _companyId, creditorGroup.Id, "Yashika Pvt Ltd", billWiseEnabled: true);
        var purchaseLedger = new Ledger(_purchaseLedgerId, _companyId, purchaseGroup.Id, "Purchase");
        var freightLedger = new Ledger(_freightLedgerId, _companyId, expenseGroup.Id, "Freight & Forwarding Charges");
        await _ledgerRepository.AddAsync(supplier);
        await _ledgerRepository.AddAsync(purchaseLedger);
        await _ledgerRepository.AddAsync(freightLedger);

        var unit = new Unit(_unitId, _companyId, "Pcs", "Pieces", 0);
        await _unitRepository.AddAsync(unit);

        var group = new ItemGroup(_itemGroupId, _companyId, "Electronics");
        await _itemGroupRepository.AddAsync(group);

        var item = new Item(_itemId, _companyId, _itemGroupId, _unitId, "Samsung 19 Inch Monitor");
        await _itemRepository.AddAsync(item);

        var voucher = new Voucher(
            _voucherId, _companyId, _financialYearId, "INV-101",
            VoucherType.Purchase, new DateOnly(2026, 5, 10));
        voucher.AddLine(_purchaseLedgerId, DebitCreditType.Debit, Money.FromDecimal(27500m));
        voucher.AddLine(_freightLedgerId, DebitCreditType.Debit, Money.FromDecimal(500m));
        voucher.AddLine(_supplierId, DebitCreditType.Credit, Money.FromDecimal(28000m));
        await _voucherRepository.AddAsync(voucher);
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_RetrievesCompletePurchaseInvoice()
    {
        await SeedPrerequisitesAsync();

        var invoice = PurchaseInvoice.Create(
            companyId: _companyId,
            financialYearId: _financialYearId,
            invoiceNumber: "INV-101",
            invoiceDate: new DateOnly(2026, 5, 10),
            supplierId: _supplierId,
            series: "Main",
            materialCentre: "Main Store",
            narration: "Test purchase",
            transport: new TransportDetails("Blue Dart", "GR-12345", new DateOnly(2026, 5, 10), "DL-01-AB-1234", "Delhi"));

        invoice.AddItemLine(_itemId, 5m, _unitId, Money.FromDecimal(5500m));
        invoice.AddSundryLine(_freightLedgerId, Money.FromDecimal(500m));

        await _purchaseInvoiceRepository.AddAsync(invoice);

        var retrieved = await _purchaseInvoiceRepository.GetByIdAsync(_companyId, invoice.Id);

        retrieved.Should().NotBeNull();
        retrieved!.InvoiceNumber.Should().Be("INV-101");
        retrieved.Series.Should().Be("Main");
        retrieved.InvoiceDate.Should().Be(new DateOnly(2026, 5, 10));
        retrieved.SupplierId.Should().Be(_supplierId);
        retrieved.MaterialCentre.Should().Be("Main Store");
        retrieved.ItemSubtotal.Amount.Should().Be(27500m);
        retrieved.SundrySubtotal.Amount.Should().Be(500m);
        retrieved.GrandTotal.Amount.Should().Be(28000m);
        retrieved.Status.Should().Be(PurchaseInvoiceStatus.Active);

        retrieved.Transport.Should().NotBeNull();
        retrieved.Transport!.TransportName.Should().Be("Blue Dart");
        retrieved.Transport.GrRrNumber.Should().Be("GR-12345");
        retrieved.Transport.VehicleNumber.Should().Be("DL-01-AB-1234");

        retrieved.Lines.Should().HaveCount(1);
        var itemLine = retrieved.Lines.First();
        itemLine.ItemId.Should().Be(_itemId);
        itemLine.Quantity.Should().Be(5m);
        itemLine.Price.Amount.Should().Be(5500m);
        itemLine.Amount.Amount.Should().Be(27500m);
        itemLine.UnitId.Should().Be(_unitId);

        retrieved.Sundries.Should().HaveCount(1);
        var sundryLine = retrieved.Sundries.First();
        sundryLine.LedgerId.Should().Be(_freightLedgerId);
        sundryLine.Amount.Amount.Should().Be(500m);
    }

    [Fact]
    public async Task ExistsByInvoiceNumberAsync_ReturnsTrueWhenExists_FalseWhenNot()
    {
        await SeedPrerequisitesAsync();

        var invoice = PurchaseInvoice.Create(
            companyId: _companyId,
            financialYearId: _financialYearId,
            invoiceNumber: "INV-EXISTS",
            invoiceDate: new DateOnly(2026, 5, 10),
            supplierId: _supplierId,
            series: "Main",
            materialCentre: "Main Store");

        invoice.AddItemLine(_itemId, 1m, _unitId, Money.FromDecimal(1000m));
        await _purchaseInvoiceRepository.AddAsync(invoice);

        var exists = await _purchaseInvoiceRepository.ExistsByInvoiceNumberAsync(_companyId, _financialYearId, "INV-EXISTS");
        var notExists = await _purchaseInvoiceRepository.ExistsByInvoiceNumberAsync(_companyId, _financialYearId, "NON-EXISTENT");

        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task GetByInvoiceNumberAsync_RetrievesMatchingInvoice()
    {
        await SeedPrerequisitesAsync();

        var invoice = PurchaseInvoice.Create(
            companyId: _companyId,
            financialYearId: _financialYearId,
            invoiceNumber: "INV-BY-NUM",
            invoiceDate: new DateOnly(2026, 5, 10),
            supplierId: _supplierId,
            series: "Main",
            materialCentre: "Main Store");

        invoice.AddItemLine(_itemId, 2m, _unitId, Money.FromDecimal(3000m));
        await _purchaseInvoiceRepository.AddAsync(invoice);

        var retrieved = await _purchaseInvoiceRepository.GetByInvoiceNumberAsync(_companyId, _financialYearId, "INV-BY-NUM");

        retrieved.Should().NotBeNull();
        retrieved!.InvoiceNumber.Should().Be("INV-BY-NUM");
        retrieved.GrandTotal.Amount.Should().Be(6000m);
        retrieved.Lines.Should().HaveCount(1);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
