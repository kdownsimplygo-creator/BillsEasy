using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
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

namespace BillsEasy.IntegrationTests.Services;

public class PurchaseAtomicTransactionTests : IDisposable
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
    private readonly PostingService _postingService;
    private readonly BillWiseService _billWiseService;
    private readonly ICurrentUserService _userService;
    private readonly IDateTimeProvider _dateProvider;

    private Guid _companyId;
    private Guid _financialYearId;
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _purchaseLedgerId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();
    private readonly Guid _itemGroupId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    private class FaultyStockMovementRepository : IStockMovementRepository
    {
        public Task AddRangeAsync(IEnumerable<StockMovement> movements, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated hardware failure during stock movement save");
        }

        public Task<IReadOnlyList<StockMovement>> GetByItemIdAsync(Guid companyId, Guid itemId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StockMovement>>(Array.Empty<StockMovement>());

        public Task<decimal> GetCurrentQuantityAsync(Guid companyId, Guid itemId, string materialCentre = "Main Store", CancellationToken cancellationToken = default)
            => Task.FromResult(0m);
    }

    public PurchaseAtomicTransactionTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_TxTest_{Guid.NewGuid():N}.db");
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

        _userService = new StandaloneCurrentUserService();
        _dateProvider = new DateTimeProvider();

        _postingService = new PostingService(
            _voucherRepository,
            _companyRepository,
            _financialYearRepository,
            _ledgerRepository,
            _userService,
            _dateProvider,
            _billReferenceRepository,
            _billAllocationRepository);

        _billWiseService = new BillWiseService(
            _billReferenceRepository,
            _billAllocationRepository,
            _voucherRepository,
            _companyRepository,
            _ledgerRepository,
            _userService,
            _dateProvider);
    }

    private async Task SeedPrerequisitesAsync()
    {
        await _initializer.InitializeAsync();

        var company = Company.Create(
            companyName: "Shree Krishna Traders",
            printName: "Shree Krishna Traders",
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
        await _accountGroupRepository.AddAsync(creditorGroup);
        await _accountGroupRepository.AddAsync(purchaseGroup);

        var supplier = new Ledger(_supplierId, _companyId, creditorGroup.Id, "Yashika Pvt Ltd", billWiseEnabled: true);
        var purchaseLedger = new Ledger(_purchaseLedgerId, _companyId, purchaseGroup.Id, "Purchase");
        await _ledgerRepository.AddAsync(supplier);
        await _ledgerRepository.AddAsync(purchaseLedger);

        var unit = new Unit(_unitId, _companyId, "Pcs", "Pieces", 0);
        await _unitRepository.AddAsync(unit);

        var group = new ItemGroup(_itemGroupId, _companyId, "Electronics");
        await _itemGroupRepository.AddAsync(group);

        var item = new Item(_itemId, _companyId, _itemGroupId, _unitId, "Samsung 19 Inch Monitor");
        await _itemRepository.AddAsync(item);
    }

    [Fact]
    public async Task CreatePurchaseInvoice_WhenStockMovementFails_RollsBackAllTablesCompletely()
    {
        await SeedPrerequisitesAsync();

        var faultyStockRepo = new FaultyStockMovementRepository();

        var service = new PurchaseService(
            _connectionFactory,
            _purchaseInvoiceRepository,
            faultyStockRepo,
            _voucherRepository,
            _postingService,
            _billWiseService,
            _companyRepository,
            _financialYearRepository,
            _ledgerRepository,
            _accountGroupRepository,
            _itemRepository,
            _unitRepository,
            _userService,
            _dateProvider);

        var dto = new CreatePurchaseInvoiceDto(
            CompanyId: _companyId,
            FinancialYearId: _financialYearId,
            InvoiceNumber: "TX-ROLLBACK-01",
            InvoiceDate: new DateOnly(2026, 5, 10),
            SupplierId: _supplierId,
            Series: "Main",
            MaterialCentre: "Main Store",
            Lines: new List<CreatePurchaseInvoiceItemLineDto>
            {
                new(_itemId, 5m, _unitId, 5500m)
            });

        // Act
        var act = () => service.CreatePurchaseInvoiceAsync(dto);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated hardware failure during stock movement save");

        // Assert: Verify zero rows in all target tables
        await using var conn = (Microsoft.Data.Sqlite.SqliteConnection)await _connectionFactory.CreateConnectionAsync();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoices;";
        var invoiceCount = (long)(await cmd.ExecuteScalarAsync())!;
        invoiceCount.Should().Be(0, "PurchaseInvoices must be rolled back");

        cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoiceItemLines;";
        var itemLineCount = (long)(await cmd.ExecuteScalarAsync())!;
        itemLineCount.Should().Be(0, "PurchaseInvoiceItemLines must be rolled back");

        cmd.CommandText = "SELECT COUNT(*) FROM Vouchers;";
        var voucherCount = (long)(await cmd.ExecuteScalarAsync())!;
        voucherCount.Should().Be(0, "Vouchers must be rolled back");

        cmd.CommandText = "SELECT COUNT(*) FROM VoucherLines;";
        var voucherLineCount = (long)(await cmd.ExecuteScalarAsync())!;
        voucherLineCount.Should().Be(0, "VoucherLines must be rolled back");

        cmd.CommandText = "SELECT COUNT(*) FROM BillReferences;";
        var billRefCount = (long)(await cmd.ExecuteScalarAsync())!;
        billRefCount.Should().Be(0, "BillReferences must be rolled back");

        cmd.CommandText = "SELECT COUNT(*) FROM StockMovements;";
        var stockCount = (long)(await cmd.ExecuteScalarAsync())!;
        stockCount.Should().Be(0, "StockMovements must be rolled back");
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }
}
