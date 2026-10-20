using System;
using System.IO;
using System.Threading.Tasks;
using BillsEasy.Infrastructure.Persistence;
using BillsEasy.IntegrationTests.Workflows.Simulation;
using FluentAssertions;
using Xunit;

namespace BillsEasy.IntegrationTests.Workflows;

public class Class01to08BusinessSimulationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly Class01to08SimulationRunner _runner;

    public Class01to08BusinessSimulationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"BillsEasy_E2E_Class1to8_{Guid.NewGuid():N}.db");
        _connectionFactory = new SqliteConnectionFactory($"Data Source={_testDbPath}");
        _runner = new Class01to08SimulationRunner(_connectionFactory);
    }

    [Fact]
    public async Task ExecuteComplete14StageBusinessSimulation_PassesAllStagesAndReconciliations()
    {
        await _runner.InitializeDatabaseAsync();

        // Stage 01 — Company & Financial Year Setup
        await _runner.RunStage01_CompanyAndFinancialYearAsync();

        // Stage 02 — Account Masters
        await _runner.RunStage02_AccountMastersAsync();

        // Stage 03 — Inventory Masters
        await _runner.RunStage03_InventoryMastersAsync();

        // Stage 04 — Opening Balances
        await _runner.RunStage04_OpeningBalancesAsync();

        // Stage 05 — Opening Stock & Opening Bill References
        await _runner.RunStage05_OpeningStockAndBillReferencesAsync();

        // Stage 06 — Day 1–3 Purchases
        await _runner.RunStage06_Day1to3PurchasesAsync();

        // Stage 07 — Day 4–5 Payments & Bill Allocations
        await _runner.RunStage07_Day4to5PaymentsAsync();

        // Stage 08 — Day 6–7 Customer Receipts
        await _runner.RunStage08_Day6to7ReceiptsAsync();

        // Stage 09 — Day 8–9 Journal Office Expense & Bank Charges
        await _runner.RunStage09_Day8to9JournalAndPaymentAsync();

        // Stage 10 — Day 10 Purchase
        await _runner.RunStage10_Day10PurchaseAsync();

        // Stage 11 — Comprehensive Reconciliation
        await _runner.RunStage11_ComprehensiveReconciliationAsync();
    }

    [Fact]
    public async Task ExecuteNegativeTestSuite_RejectsAllInvalidOperationsCleanly()
    {
        await _runner.InitializeDatabaseAsync();
        await _runner.RunStage01_CompanyAndFinancialYearAsync();
        await _runner.RunStage02_AccountMastersAsync();
        await _runner.RunStage03_InventoryMastersAsync();
        await _runner.RunStage04_OpeningBalancesAsync();
        await _runner.RunStage05_OpeningStockAndBillReferencesAsync();
        await _runner.RunStage06_Day1to3PurchasesAsync();
        await _runner.RunStage07_Day4to5PaymentsAsync();
        await _runner.RunStage08_Day6to7ReceiptsAsync();
        await _runner.RunStage09_Day8to9JournalAndPaymentAsync();
        await _runner.RunStage10_Day10PurchaseAsync();

        // Stage 12 — Negative Tests (N1 to N9)
        await _runner.RunStage12_NegativeTestsAsync();
    }

    [Fact]
    public async Task ExecuteAtomicityRollbackTests_EnsureCompleteRollback()
    {
        await _runner.InitializeDatabaseAsync();
        await _runner.RunStage01_CompanyAndFinancialYearAsync();
        await _runner.RunStage02_AccountMastersAsync();
        await _runner.RunStage03_InventoryMastersAsync();
        await _runner.RunStage04_OpeningBalancesAsync();
        await _runner.RunStage05_OpeningStockAndBillReferencesAsync();

        // Stage 13 — Atomicity Rollback Tests (Failure Points A, B, C, D)
        await _runner.RunStage13_AtomicityRollbackTestsAsync();
    }

    [Fact]
    public async Task ExecuteIsolationAndDatabaseIntegrityAudit_Passes()
    {
        await _runner.InitializeDatabaseAsync();
        await _runner.RunStage01_CompanyAndFinancialYearAsync();
        await _runner.RunStage02_AccountMastersAsync();
        await _runner.RunStage03_InventoryMastersAsync();
        await _runner.RunStage04_OpeningBalancesAsync();
        await _runner.RunStage05_OpeningStockAndBillReferencesAsync();
        await _runner.RunStage06_Day1to3PurchasesAsync();
        await _runner.RunStage07_Day4to5PaymentsAsync();
        await _runner.RunStage08_Day6to7ReceiptsAsync();
        await _runner.RunStage09_Day8to9JournalAndPaymentAsync();
        await _runner.RunStage10_Day10PurchaseAsync();

        // Stage 14 — Company Isolation, FY Isolation, Database Integrity Audit
        await _runner.RunStage14_IsolationAndDatabaseIntegrityAsync();
    }

    public void Dispose()
    {
        BillsEasy.IntegrationTests.Workflows.RealDb.InteractiveDatabaseGuard.CleanTestDatabaseFiles(_testDbPath);
    }
}
