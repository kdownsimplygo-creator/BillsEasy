using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
using BillsEasy.IntegrationTests.Workflows.Simulation;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BillsEasy.IntegrationTests.Workflows.RealDb;

/// <summary>
/// Second validation layer: executes against a dedicated real-database configuration file
/// (W:\BillsEasy\TestData\E2E\BillsEasy_RealDB_Simulation_Run.db) using the exact same Dependency Injection,
/// schema initialization, and service lifetimes as BillsEasy.Presentation.Wpf.App.xaml.cs.
/// </summary>
[CollectionDefinition("RealDatabaseTests", DisableParallelization = true)]
public class RealDatabaseTestsCollectionDefinition
{
}

[Collection("RealDatabaseTests")]
public class RealDbBusinessSimulationTests : IDisposable
{
    public static readonly string RealDbDirectory = InteractiveDatabaseGuard.ProtectedInteractiveDbDirectory;
    public static readonly string SimulationDbFilePath = InteractiveDatabaseGuard.AllowedSimulationDbFilePath;
    public static readonly string SimulationDbConnectionString = $"Data Source={SimulationDbFilePath}";

    // Backwards compatibility aliases redirecting to the isolated simulation database
    public static string RealDbFilePath => SimulationDbFilePath;
    public static string RealDbConnectionString => SimulationDbConnectionString;

    private readonly IServiceProvider _serviceProvider;
    private readonly Class01to08SimulationRunner _runner;
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public RealDbBusinessSimulationTests()
    {
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(SimulationDbFilePath);
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(SimulationDbConnectionString);

        // 1. Ensure test directory exists
        if (!Directory.Exists(RealDbDirectory))
        {
            Directory.CreateDirectory(RealDbDirectory);
        }

        // 2. Clean start from a fresh database file (strictly isolated from interactive E2E DB)
        ResetDatabaseFile();

        // 3. Mirror the EXACT same DI registrations from BillsEasy.Presentation.Wpf.App.xaml.cs
        var services = new ServiceCollection();

        _connectionFactory = new SqliteConnectionFactory(SimulationDbConnectionString);
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(_connectionFactory);
        services.AddSingleton<ISqliteDbConnectionFactory>(_connectionFactory);
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ICompanyRepository, CompanyRepository>();
        services.AddSingleton<IFinancialYearRepository, FinancialYearRepository>();
        services.AddSingleton<IAccountGroupRepository, AccountGroupRepository>();
        services.AddSingleton<ILedgerRepository, LedgerRepository>();
        services.AddSingleton<IVoucherRepository, VoucherRepository>();
        services.AddSingleton<IBillReferenceRepository, BillReferenceRepository>();
        services.AddSingleton<IBillAllocationRepository, BillAllocationRepository>();
        services.AddSingleton<IUnitRepository, UnitRepository>();
        services.AddSingleton<IItemGroupRepository, ItemGroupRepository>();
        services.AddSingleton<IItemRepository, ItemRepository>();
        services.AddSingleton<IPurchaseInvoiceRepository, PurchaseInvoiceRepository>();
        services.AddSingleton<IStockMovementRepository, StockMovementRepository>();
        services.AddSingleton<ICurrentUserService, StandaloneCurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<ICompanyContextService, CompanyContextService>();

        services.AddTransient<IAccountGroupService, AccountGroupService>();
        services.AddTransient<AccountGroupService>();
        services.AddTransient<ILedgerService, LedgerService>();
        services.AddTransient<LedgerService>();
        services.AddTransient<IUnitService, UnitService>();
        services.AddTransient<UnitService>();
        services.AddTransient<IItemGroupService, ItemGroupService>();
        services.AddTransient<ItemGroupService>();
        services.AddTransient<IItemService, ItemService>();
        services.AddTransient<ItemService>();
        services.AddTransient<IPostingService, PostingService>();
        services.AddTransient<PostingService>();
        services.AddTransient<IBillWiseService, BillWiseService>();
        services.AddTransient<BillWiseService>();
        services.AddTransient<IPurchaseService, PurchaseService>();
        services.AddTransient<PurchaseService>();
        services.AddTransient<CompanyService>();
        services.AddTransient<FinancialYearService>();

        _serviceProvider = services.BuildServiceProvider();

        _runner = new Class01to08SimulationRunner(_connectionFactory);
    }

    private static void ResetDatabaseFile()
    {
        InteractiveDatabaseGuard.CleanTestDatabaseFiles(SimulationDbFilePath);
    }

    [Fact]
    public async Task ExecuteRealDatabaseClasses1to8BusinessSimulation_PassesAllSubsystemsAndDirectSqlAudit()
    {
        // 1. Initialize schema synchronously through DI container (identical to App.xaml.cs OnStartup)
        var dbInit = _serviceProvider.GetRequiredService<DatabaseInitializer>();
        await dbInit.InitializeAsync();

        File.Exists(SimulationDbFilePath).Should().BeTrue("Simulation database file must be created on disk");

        // 2. Stage 01: Company & Financial Year Setup
        await _runner.RunStage01_CompanyAndFinancialYearAsync();

        // 3. Stage 02: Account Masters
        await _runner.RunStage02_AccountMastersAsync();

        // 4. Stage 03: Inventory Masters
        await _runner.RunStage03_InventoryMastersAsync();

        // 5. Stage 04: Opening Balances
        await _runner.RunStage04_OpeningBalancesAsync();

        // 6. Stage 05: Opening Stock & Bill-Wise References
        await _runner.RunStage05_OpeningStockAndBillReferencesAsync();

        // 7. Stage 06: Day 1–3 Purchases
        await _runner.RunStage06_Day1to3PurchasesAsync();

        // 8. Stage 07: Day 4–5 Payments & Bill Allocations
        await _runner.RunStage07_Day4to5PaymentsAsync();

        // 9. Stage 08: Day 6–7 Customer Receipts
        await _runner.RunStage08_Day6to7ReceiptsAsync();

        // 10. Stage 09: Day 8–9 Journal Office Expense & Bank Charges
        await _runner.RunStage09_Day8to9JournalAndPaymentAsync();

        // 11. Stage 10: Day 10 Purchase
        await _runner.RunStage10_Day10PurchaseAsync();

        // 12. Stage 11: Comprehensive Application-Layer Reconciliation
        await _runner.RunStage11_ComprehensiveReconciliationAsync();

        // 13. DIRECT SQL AUDIT on Real Database File
        await using var conn = new SqliteConnection(RealDbConnectionString);
        await conn.OpenAsync();

        // Verify Total Debits == Total Credits in SQLite database
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    SUM(CASE WHEN vl.Type = 1 THEN vl.Amount ELSE 0 END) AS TotalDebit,
                    SUM(CASE WHEN vl.Type = 2 THEN vl.Amount ELSE 0 END) AS TotalCredit
                FROM VoucherLines vl
                JOIN Vouchers v ON v.Id = vl.VoucherId
                WHERE v.Status = 2;";
            await using var reader = await cmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            var sumDebitPaise = reader.GetInt64(0);
            var sumCreditPaise = reader.GetInt64(1);
            sumDebitPaise.Should().Be(sumCreditPaise, "Total posted debits must equal total posted credits in SQL");
            sumDebitPaise.Should().Be(54000000, "Total posted voucher minor units must equal ₹5,40,000.00 (54,000,000 paise)");
        }

        // Verify Individual Ledgers in SQLite database
        var cashBalance = await _runner.Reconciler.CalculateLedgerBalanceAsync(_runner.Context.CompanyId, _runner.Context.CashLedgerId);
        cashBalance.Should().Be(107500m);

        var bankBalance = await _runner.Reconciler.CalculateLedgerBalanceAsync(_runner.Context.CompanyId, _runner.Context.BankLedgerId);
        bankBalance.Should().Be(657500m);

        var rajBalance = await _runner.Reconciler.CalculateLedgerBalanceAsync(_runner.Context.CompanyId, _runner.Context.RajLedgerId);
        rajBalance.Should().Be(35000m);

        var apexBalance = await _runner.Reconciler.CalculateLedgerBalanceAsync(_runner.Context.CompanyId, _runner.Context.ApexLedgerId);
        apexBalance.Should().Be(30000m);

        var yashikaBalance = await _runner.Reconciler.CalculateLedgerBalanceAsync(_runner.Context.CompanyId, _runner.Context.YashikaLedgerId);
        Math.Abs(yashikaBalance).Should().Be(187500m);

        var techWorldBalance = await _runner.Reconciler.CalculateLedgerBalanceAsync(_runner.Context.CompanyId, _runner.Context.TechWorldLedgerId);
        Math.Abs(techWorldBalance).Should().Be(81000m);

        var globalBalance = await _runner.Reconciler.CalculateLedgerBalanceAsync(_runner.Context.CompanyId, _runner.Context.GlobalSuppliesLedgerId);
        Math.Abs(globalBalance).Should().Be(161500m);

        // Verify Bill-Wise Outstanding in SQLite database
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT b.ReferenceNumber, b.OriginalAmount, COALESCE(SUM(a.AllocatedAmount), 0) AS Allocated,
                       b.OriginalAmount - COALESCE(SUM(a.AllocatedAmount), 0) AS Outstanding
                FROM BillReferences b
                LEFT JOIN BillAllocations a ON a.BillReferenceId = b.Id
                WHERE b.Status = 1
                GROUP BY b.Id
                ORDER BY b.ReferenceNumber;";

            var billResults = new Dictionary<string, (long Original, long Allocated, long Outstanding)>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var refNum = reader.GetString(0);
                var orig = reader.GetInt64(1);
                var alloc = reader.GetInt64(2);
                var outst = reader.GetInt64(3);
                billResults[refNum] = (orig, alloc, outst);
            }

            // Yashika
            billResults["OPEN-YASHIKA-001"].Outstanding.Should().Be(8000000); // ₹80,000
            billResults["PUR/YASH/001"].Allocated.Should().Be(3000000);       // ₹30,000 allocated
            billResults["PUR/YASH/001"].Outstanding.Should().Be(3800000);     // ₹38,000 outstanding
            billResults["PUR/YASH/002"].Outstanding.Should().Be(6950000);     // ₹69,500 outstanding

            // TechWorld
            billResults["OPEN-TECHWORLD-001"].Outstanding.Should().Be(4500000); // ₹45,000
            billResults["PUR/TECH/001"].Allocated.Should().Be(5000000);         // ₹50,000 allocated
            billResults["PUR/TECH/001"].Outstanding.Should().Be(3600000);       // ₹36,000 outstanding

            // Global
            billResults["PUR/GLOB/001"].Outstanding.Should().Be(16150000);      // ₹1,61,500 outstanding
        }

        // Verify Physical Stock Quantities in SQLite database
        var stockRepo = _serviceProvider.GetRequiredService<IStockMovementRepository>();
        (await stockRepo.GetCurrentQuantityAsync(_runner.Context.CompanyId, _runner.Context.SamsungMonitorItemId)).Should().Be(19m);
        (await stockRepo.GetCurrentQuantityAsync(_runner.Context.CompanyId, _runner.Context.DellMonitorItemId)).Should().Be(13m);
        (await stockRepo.GetCurrentQuantityAsync(_runner.Context.CompanyId, _runner.Context.LenovoLaptopItemId)).Should().Be(4m);
        (await stockRepo.GetCurrentQuantityAsync(_runner.Context.CompanyId, _runner.Context.HpLaptopItemId)).Should().Be(6m);
        (await stockRepo.GetCurrentQuantityAsync(_runner.Context.CompanyId, _runner.Context.TpLinkRouterItemId)).Should().Be(20m);
        (await stockRepo.GetCurrentQuantityAsync(_runner.Context.CompanyId, _runner.Context.DLinkSwitchItemId)).Should().Be(10m);

        // 14. Relational Integrity Queries
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM VoucherLines WHERE VoucherId NOT IN (SELECT Id FROM Vouchers);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);

            cmd.CommandText = "SELECT COUNT(*) FROM BillReferences WHERE SourceVoucherLineId IS NOT NULL AND SourceVoucherLineId NOT IN (SELECT Id FROM VoucherLines);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);

            cmd.CommandText = "SELECT COUNT(*) FROM BillAllocations WHERE VoucherLineId NOT IN (SELECT Id FROM VoucherLines) OR BillReferenceId NOT IN (SELECT Id FROM BillReferences);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);

            cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoiceItemLines WHERE PurchaseInvoiceId NOT IN (SELECT Id FROM PurchaseInvoices);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);

            cmd.CommandText = "SELECT COUNT(*) FROM PurchaseInvoiceSundryLines WHERE PurchaseInvoiceId NOT IN (SELECT Id FROM PurchaseInvoices);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);

            cmd.CommandText = "SELECT COUNT(*) FROM StockMovements WHERE SourceDocumentType = 'PurchaseInvoice' AND SourceDocumentId NOT IN (SELECT Id FROM PurchaseInvoices);";
            ((long)(await cmd.ExecuteScalarAsync())!).Should().Be(0);
        }

        // 15. Negative Scenarios (N1–N9) on real database
        await _runner.RunStage12_NegativeTestsAsync();

        // 16. Atomicity Rollback Tests (A–D) on real database
        await _runner.RunStage13_AtomicityRollbackTestsAsync();

        // 17. Isolation and Final Integrity Audit
        await _runner.RunStage14_IsolationAndDatabaseIntegrityAsync();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable d)
        {
            d.Dispose();
        }
    }
}
