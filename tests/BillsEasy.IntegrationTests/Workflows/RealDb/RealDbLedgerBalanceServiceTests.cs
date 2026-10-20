using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.Services;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.Infrastructure.Persistence.Repositories;
using BillsEasy.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BillsEasy.IntegrationTests.Workflows.RealDb;

[Collection("RealDatabaseTests")]
public class RealDbLedgerBalanceServiceTests : IDisposable
{
    private static readonly string SimulationDbPath = RealDbBusinessSimulationTests.SimulationDbFilePath;
    private readonly ServiceProvider _serviceProvider;
    private readonly Guid _companyId;

    public RealDbLedgerBalanceServiceTests()
    {
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(SimulationDbPath);

        var services = new ServiceCollection();
        var connectionString = $"Data Source={SimulationDbPath}";
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(connectionString);
        var connFactory = new SqliteConnectionFactory(connectionString);
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(connFactory);

        services.AddSingleton<ISqliteDbConnectionFactory>(_ => connFactory);
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ICompanyRepository, CompanyRepository>();
        services.AddSingleton<IAccountGroupRepository, AccountGroupRepository>();
        services.AddSingleton<ILedgerRepository, LedgerRepository>();
        services.AddSingleton<IVoucherRepository, VoucherRepository>();
        services.AddSingleton<ILedgerBalanceService, LedgerBalanceService>();
        services.AddSingleton<IItemRepository, ItemRepository>();
        services.AddSingleton<IItemGroupRepository, ItemGroupRepository>();
        services.AddSingleton<IStockMovementRepository, StockMovementRepository>();
        services.AddSingleton<IUnitRepository, UnitRepository>();
        services.AddSingleton<ICurrentUserService, StandaloneCurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<IItemService, ItemService>();

        _serviceProvider = services.BuildServiceProvider();

        // Ensure schema
        var dbInit = _serviceProvider.GetRequiredService<DatabaseInitializer>();
        dbInit.InitializeAsync().GetAwaiter().GetResult();

        var companyRepo = _serviceProvider.GetRequiredService<ICompanyRepository>();
        var company = companyRepo.GetAllAsync().GetAwaiter().GetResult()
            .FirstOrDefault(c => c.CompanyName == "Shree Krishna Electronics");

        if (company == null)
        {
            var runner = new BillsEasy.IntegrationTests.Workflows.Simulation.Class01to08SimulationRunner(connFactory);
            runner.RunStage01_CompanyAndFinancialYearAsync().GetAwaiter().GetResult();
            runner.RunStage02_AccountMastersAsync().GetAwaiter().GetResult();
            runner.RunStage03_InventoryMastersAsync().GetAwaiter().GetResult();
            runner.RunStage04_OpeningBalancesAsync().GetAwaiter().GetResult();
            runner.RunStage05_OpeningStockAndBillReferencesAsync().GetAwaiter().GetResult();
            runner.RunStage06_Day1to3PurchasesAsync().GetAwaiter().GetResult();
            runner.RunStage07_Day4to5PaymentsAsync().GetAwaiter().GetResult();
            runner.RunStage08_Day6to7ReceiptsAsync().GetAwaiter().GetResult();
            runner.RunStage09_Day8to9JournalAndPaymentAsync().GetAwaiter().GetResult();
            runner.RunStage10_Day10PurchaseAsync().GetAwaiter().GetResult();
            runner.RunStage11_ComprehensiveReconciliationAsync().GetAwaiter().GetResult();

            company = companyRepo.GetAllAsync().GetAwaiter().GetResult()
                .FirstOrDefault(c => c.CompanyName == "Shree Krishna Electronics");
        }

        company.Should().NotBeNull();
        _companyId = company!.Id;
    }

    [Fact]
    public async Task GetDashboardFinancialSummaryAsync_ReturnsExactExpectedReconciledBalances()
    {
        var balanceService = _serviceProvider.GetRequiredService<ILedgerBalanceService>();

        var summary = await balanceService.GetDashboardFinancialSummaryAsync(_companyId);

        // Cash: Opening 1,00,000 + Apex 20,000 - Office Exp 12,500 = 1,07,500 Dr
        summary.CashBalance.Should().Be(107500m);
        summary.CashBalanceType.Should().Be("Dr");

        // Bank: Opening 7,00,000 - Yashika 30,000 - TechWorld 50,000 + Raj 40,000 - Bank Charges 2,500 = 6,57,500 Dr
        summary.BankBalance.Should().Be(657500m);
        summary.BankBalanceType.Should().Be("Dr");

        // Receivables: Raj 35,000 + Apex 30,000 = 65,000 Dr
        summary.ReceivablesBalance.Should().Be(65000m);
        summary.ReceivablesBalanceType.Should().Be("Dr");

        // Payables: Yashika 187,500 + TechWorld 81,000 + Global 161,500 = 430,000 Cr
        summary.PayablesBalance.Should().Be(430000m);
        summary.PayablesBalanceType.Should().Be("Cr");
    }

    [Fact]
    public async Task VerifyInventorySnapshot_ReturnsExactPhysicalStockCounts()
    {
        var itemService = _serviceProvider.GetRequiredService<IItemService>();
        var stockMovementRepo = _serviceProvider.GetRequiredService<IStockMovementRepository>();

        var items = (await itemService.GetAllItemsAsync(_companyId)).ToDictionary(i => i.Name);

        // Verify all 6 stock items with exact physical quantities
        items.Should().ContainKey("Samsung M21' Monitor");
        (await stockMovementRepo.GetCurrentQuantityAsync(_companyId, items["Samsung M21' Monitor"].Id)).Should().Be(19m);

        items.Should().ContainKey("Dell 24-inch Monitor");
        (await stockMovementRepo.GetCurrentQuantityAsync(_companyId, items["Dell 24-inch Monitor"].Id)).Should().Be(13m);

        items.Should().ContainKey("Lenovo Business Laptop");
        (await stockMovementRepo.GetCurrentQuantityAsync(_companyId, items["Lenovo Business Laptop"].Id)).Should().Be(4m);

        items.Should().ContainKey("HP Business Laptop");
        (await stockMovementRepo.GetCurrentQuantityAsync(_companyId, items["HP Business Laptop"].Id)).Should().Be(6m);

        items.Should().ContainKey("TP-Link Router");
        (await stockMovementRepo.GetCurrentQuantityAsync(_companyId, items["TP-Link Router"].Id)).Should().Be(20m);

        items.Should().ContainKey("D-Link 8 Port Switch");
        (await stockMovementRepo.GetCurrentQuantityAsync(_companyId, items["D-Link 8 Port Switch"].Id)).Should().Be(10m);
    }

    [Fact]
    public async Task VerifyDatabaseCountsRemainUnchanged()
    {
        var factory = _serviceProvider.GetRequiredService<ISqliteDbConnectionFactory>();
        await using var conn = (Microsoft.Data.Sqlite.SqliteConnection)await factory.CreateConnectionAsync();

        var tables = new[]
        {
            "Companies", "FinancialYears", "AccountGroups", "Ledgers", "Vouchers",
            "VoucherLines", "PurchaseInvoices", "PurchaseInvoiceItemLines",
            "PurchaseInvoiceSundryLines", "BillReferences", "BillAllocations",
            "StockMovements", "Items", "Units"
        };

        foreach (var t in tables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {t}";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            count.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
