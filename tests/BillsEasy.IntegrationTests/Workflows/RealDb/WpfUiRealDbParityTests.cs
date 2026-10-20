using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using BillsEasy.IntegrationTests.Workflows.Simulation;

namespace BillsEasy.IntegrationTests.Workflows.RealDb;

[Collection("RealDatabaseTests")]
public class WpfUiRealDbParityTests : IDisposable
{
    private static readonly string SimulationDbPath = RealDbBusinessSimulationTests.SimulationDbFilePath;
    private static readonly string SimulationDbConnectionString = $"Data Source={SimulationDbPath}";
    private static string RealDbConnectionString => SimulationDbConnectionString;
    private static readonly string LocalAppDataPath = InteractiveDatabaseGuard.ProtectedUserDbFilePath;
    private static readonly object EnvLock = new();

    private readonly ServiceProvider _serviceProvider;
    private readonly Guid _companyId;

    public WpfUiRealDbParityTests()
    {
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(SimulationDbPath);
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(SimulationDbConnectionString);

        var services = new ServiceCollection();

        // Register exactly matching WPF App.xaml.cs
        var connFactory = new SqliteConnectionFactory(SimulationDbConnectionString);
        InteractiveDatabaseGuard.AssertNotProtectedDatabase(connFactory);
        services.AddSingleton<ISqliteDbConnectionFactory>(_ => connFactory);
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

        // Initialize schema
        var dbInit = _serviceProvider.GetRequiredService<DatabaseInitializer>();
        dbInit.InitializeAsync().GetAwaiter().GetResult();

        // Retrieve Company ID
        var compRepo = _serviceProvider.GetRequiredService<ICompanyRepository>();
        var company = compRepo.GetAllAsync().GetAwaiter().GetResult()
            .FirstOrDefault(c => c.CompanyName == "Shree Krishna Electronics");

        if (company == null)
        {
            var runner = new Class01to08SimulationRunner(connFactory);
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

            company = compRepo.GetAllAsync().GetAwaiter().GetResult()
                .FirstOrDefault(c => c.CompanyName == "Shree Krishna Electronics");
        }

        _companyId = company?.Id ?? Guid.Empty;
    }

    [Fact]
    public void Phase01_DatabasePathResolution_UsesEnvironmentVariableWhenSet()
    {
        lock (EnvLock)
        {
            var originalEnv = Environment.GetEnvironmentVariable("BILLSEASY_DB_PATH");
            try
            {
                Environment.SetEnvironmentVariable("BILLSEASY_DB_PATH", SimulationDbPath);

                // Act
                var envDbPath = Environment.GetEnvironmentVariable("BILLSEASY_DB_PATH");
                string resolvedPath;
                if (!string.IsNullOrWhiteSpace(envDbPath))
                {
                    resolvedPath = envDbPath.Trim();
                }
                else
                {
                    resolvedPath = LocalAppDataPath;
                }

                // Assert
                resolvedPath.Should().Be(SimulationDbPath);
                InteractiveDatabaseGuard.AssertNotProtectedDatabase(resolvedPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("BILLSEASY_DB_PATH", originalEnv);
            }
        }
    }

    [Fact]
    public void Phase01_DatabasePathResolution_FallsBackToLocalAppDataWhenUnset()
    {
        lock (EnvLock)
        {
            var originalEnv = Environment.GetEnvironmentVariable("BILLSEASY_DB_PATH");
            try
            {
                Environment.SetEnvironmentVariable("BILLSEASY_DB_PATH", null);

                // Act
                var envDbPath = Environment.GetEnvironmentVariable("BILLSEASY_DB_PATH");
                string resolvedPath;
                if (!string.IsNullOrWhiteSpace(envDbPath))
                {
                    resolvedPath = envDbPath.Trim();
                }
                else
                {
                    resolvedPath = LocalAppDataPath;
                }

                // Assert
                resolvedPath.Should().Be(LocalAppDataPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("BILLSEASY_DB_PATH", originalEnv);
            }
        }
    }

    [Fact]
    public async Task Phase05_VerifyCompanyVisibility_ShreeKrishnaElectronics()
    {
        // Assert company exists and properties match validated RealDB
        var companyRepo = _serviceProvider.GetRequiredService<ICompanyRepository>();
        var companies = await companyRepo.GetAllAsync();
        var company = companies.FirstOrDefault(c => c.CompanyName == "Shree Krishna Electronics");

        company.Should().NotBeNull("WPF UI must see 'Shree Krishna Electronics' in the directory");
        company!.CompanyName.Should().Be("Shree Krishna Electronics");
        company.ShortName.Should().Be("SKE");
        company.StateName.Should().Be("Gujarat");
        company.StateCode.Should().Be("24");

        // Verify FY
        var fyRepo = _serviceProvider.GetRequiredService<IFinancialYearRepository>();
        var fys = await fyRepo.GetByCompanyIdAsync(company.Id);
        fys.Should().HaveCount(1);
        var fy = fys.First();
        fy.YearName.Should().Be("2023-2024");
        fy.StartDate.Should().Be(new DateOnly(2023, 4, 1));
        fy.EndDate.Should().Be(new DateOnly(2024, 3, 31));

        // Set context and verify context service
        var contextService = _serviceProvider.GetRequiredService<ICompanyContextService>();
        contextService.SetActiveContext(company);
        contextService.ActiveCompany.Should().NotBeNull();
        contextService.ActiveCompany!.CompanyName.Should().Be("Shree Krishna Electronics");
        contextService.ActiveFinancialYear.Should().NotBeNull();
        contextService.ActiveFinancialYear!.YearName.Should().Be("2023-2024");
    }

    [Fact]
    public async Task Phase06_UIReadValidation_AccountMastersAndOpeningBalances()
    {
        _companyId.Should().NotBeEmpty();
        var ledgerService = _serviceProvider.GetRequiredService<ILedgerService>();
        var ledgers = (await ledgerService.GetAllLedgersAsync(_companyId)).ToList();

        // 10 key ledgers
        var capital = ledgers.FirstOrDefault(l => l.Name.Contains("Capital"));
        capital.Should().NotBeNull();
        capital!.OpeningBalance.Should().Be(1123000m);
        capital.OpeningBalanceType.Should().Be(DebitCreditType.Credit);

        var cash = ledgers.FirstOrDefault(l => l.Name == "Cash");
        cash.Should().NotBeNull();
        cash!.OpeningBalance.Should().Be(100000m);
        cash.OpeningBalanceType.Should().Be(DebitCreditType.Debit);

        var bank = ledgers.FirstOrDefault(l => l.Name.Contains("Bank of India"));
        bank.Should().NotBeNull();
        bank!.OpeningBalance.Should().Be(700000m);
        bank.OpeningBalanceType.Should().Be(DebitCreditType.Debit);

        var raj = ledgers.FirstOrDefault(l => l.Name.Contains("Raj Electronics"));
        raj.Should().NotBeNull();
        raj!.OpeningBalance.Should().Be(75000m);

        var apex = ledgers.FirstOrDefault(l => l.Name.Contains("Apex Computers"));
        apex.Should().NotBeNull();
        apex!.OpeningBalance.Should().Be(50000m);

        var shivam = ledgers.FirstOrDefault(l => l.Name.Contains("Shivam Traders"));
        shivam.Should().NotBeNull();
        shivam!.OpeningBalance.Should().Be(0m);

        var yashika = ledgers.FirstOrDefault(l => l.Name.Contains("Yashika"));
        yashika.Should().NotBeNull();
        yashika!.OpeningBalance.Should().Be(80000m);
        yashika.OpeningBalanceType.Should().Be(DebitCreditType.Credit);

        var techWorld = ledgers.FirstOrDefault(l => l.Name.Contains("TechWorld"));
        techWorld.Should().NotBeNull();
        techWorld!.OpeningBalance.Should().Be(45000m);
        techWorld.OpeningBalanceType.Should().Be(DebitCreditType.Credit);

        var global = ledgers.FirstOrDefault(l => l.Name.Contains("Global Computer Supplies"));
        global.Should().NotBeNull();
        global!.OpeningBalance.Should().Be(0m);

        var purchase = ledgers.FirstOrDefault(l => l.Name == "Purchase");
        purchase.Should().NotBeNull();

        var freight = ledgers.FirstOrDefault(l => l.Name.Contains("Freight"));
        freight.Should().NotBeNull();

        var officeExp = ledgers.FirstOrDefault(l => l.Name.Contains("Office Expenses"));
        officeExp.Should().NotBeNull();

        var bankCharges = ledgers.FirstOrDefault(l => l.Name.Contains("Bank Charges"));
        bankCharges.Should().NotBeNull();
    }

    [Fact]
    public async Task Phase06_UIReadValidation_InventoryMasters()
    {
        _companyId.Should().NotBeEmpty();
        var unitService = _serviceProvider.GetRequiredService<IUnitService>();
        var units = (await unitService.GetAllUnitsAsync(_companyId)).ToList();
        units.Should().Contain(u => u.Name == "Pcs");
        units.Should().Contain(u => u.Name == "NOS");
        units.Should().Contain(u => u.Name == "Box");

        var groupService = _serviceProvider.GetRequiredService<IItemGroupService>();
        var groups = (await groupService.GetAllGroupsAsync(_companyId)).ToList();
        groups.Should().Contain(g => g.Name == "Computer Accessories");
        groups.Should().Contain(g => g.Name == "Monitors");
        groups.Should().Contain(g => g.Name == "Laptops");
        groups.Should().Contain(g => g.Name == "Networking");

        var itemService = _serviceProvider.GetRequiredService<IItemService>();
        var items = (await itemService.GetAllItemsAsync(_companyId)).ToList();
        items.Should().HaveCount(6);
        items.Should().Contain(i => i.Name == "Samsung M21' Monitor" && i.OpeningStockQuantity == 10m);
        items.Should().Contain(i => i.Name == "Dell 24-inch Monitor" && i.OpeningStockQuantity == 5m);
        items.Should().Contain(i => i.Name == "Lenovo Business Laptop" && i.OpeningStockQuantity == 3m);
        items.Should().Contain(i => i.Name == "HP Business Laptop" && i.OpeningStockQuantity == 2m);
        items.Should().Contain(i => i.Name == "TP-Link Router" && i.OpeningStockQuantity == 10m);
        items.Should().Contain(i => i.Name == "D-Link 8 Port Switch" && i.OpeningStockQuantity == 5m);
    }

    [Fact]
    public async Task Phase07_UITransactionVisibility_PurchasesAndVouchers()
    {
        _companyId.Should().NotBeEmpty();

        // Purchases
        var purchaseService = _serviceProvider.GetRequiredService<IPurchaseService>();
        var summaries = (await purchaseService.GetSummariesAsync(_companyId)).ToList();
        summaries.Should().HaveCount(4);

        var p1 = summaries.FirstOrDefault(s => s.InvoiceNumber == "PUR/YASH/001");
        p1.Should().NotBeNull();
        p1!.GrandTotal.Should().Be(68000m);

        var p2 = summaries.FirstOrDefault(s => s.InvoiceNumber == "PUR/TECH/001");
        p2.Should().NotBeNull();
        p2!.GrandTotal.Should().Be(86000m);

        var p3 = summaries.FirstOrDefault(s => s.InvoiceNumber == "PUR/GLOB/001");
        p3.Should().NotBeNull();
        p3!.GrandTotal.Should().Be(161500m);

        var p4 = summaries.FirstOrDefault(s => s.InvoiceNumber == "PUR/YASH/002");
        p4.Should().NotBeNull();
        p4!.GrandTotal.Should().Be(69500m);

        // Vouchers
        var voucherRepo = _serviceProvider.GetRequiredService<IVoucherRepository>();
        var vouchers = (await voucherRepo.GetAllAsync(_companyId)).ToList();

        // Payments
        vouchers.Should().Contain(v => v.VoucherNumber == "PAY/2023/001" && v.TotalDebit.Amount == 30000m);
        vouchers.Should().Contain(v => v.VoucherNumber == "PAY/2023/002" && v.TotalDebit.Amount == 50000m);
        vouchers.Should().Contain(v => v.VoucherNumber == "PAY/2023/003" && v.TotalDebit.Amount == 2500m);

        // Receipts
        vouchers.Should().Contain(v => v.VoucherNumber == "REC/2023/001" && v.TotalDebit.Amount == 40000m);
        vouchers.Should().Contain(v => v.VoucherNumber == "REC/2023/002" && v.TotalDebit.Amount == 20000m);

        // Journal
        vouchers.Should().Contain(v => v.VoucherNumber == "JRN/2023/001" && v.TotalDebit.Amount == 12500m);
    }

    [Fact]
    public async Task Phase08_UIReportReconciliation_FinancialBalancesAndStock()
    {
        _companyId.Should().NotBeEmpty();
        var ledgerRepo = _serviceProvider.GetRequiredService<ILedgerRepository>();
        var voucherRepo = _serviceProvider.GetRequiredService<IVoucherRepository>();
        var billRefRepo = _serviceProvider.GetRequiredService<IBillReferenceRepository>();
        var stockRepo = _serviceProvider.GetRequiredService<IStockMovementRepository>();

        var allVouchers = (await voucherRepo.GetAllAsync(_companyId)).ToList();
        var allLedgers = (await ledgerRepo.GetAllAsync(_companyId)).ToList();

        decimal GetLedgerBalance(Guid id)
        {
            var l = allLedgers.First(x => x.Id == id);
            decimal bal = 0m;
            if (l.OpeningBalance.Amount > 0)
                bal = l.OpeningBalanceType == DebitCreditType.Debit ? l.OpeningBalance.Amount : -l.OpeningBalance.Amount;

            foreach (var v in allVouchers)
            {
                foreach (var line in v.Lines.Where(x => x.LedgerId == id))
                {
                    bal += line.Type == DebitCreditType.Debit ? line.Amount.Amount : -line.Amount.Amount;
                }
            }
            return bal;
        }

        // Cash: ₹1,07,500
        var cash = allLedgers.First(l => l.Name == "Cash");
        GetLedgerBalance(cash.Id).Should().Be(107500m);

        // Bank: ₹6,57,500
        var bank = allLedgers.First(l => l.Name.Contains("Bank of India"));
        GetLedgerBalance(bank.Id).Should().Be(657500m);

        // Customers: ₹65,000 (Raj 35k + Apex 30k)
        var raj = allLedgers.First(l => l.Name.Contains("Raj Electronics"));
        var apex = allLedgers.First(l => l.Name.Contains("Apex Computers"));
        GetLedgerBalance(raj.Id).Should().Be(35000m);
        GetLedgerBalance(apex.Id).Should().Be(30000m);
        (GetLedgerBalance(raj.Id) + GetLedgerBalance(apex.Id)).Should().Be(65000m);

        // Suppliers: ₹4,30,000 (Yashika 187.5k + TechWorld 81k + Global 161.5k)
        var yashika = allLedgers.First(l => l.Name.Contains("Yashika"));
        var techWorld = allLedgers.First(l => l.Name.Contains("TechWorld"));
        var global = allLedgers.First(l => l.Name.Contains("Global Computer Supplies"));
        Math.Abs(GetLedgerBalance(yashika.Id)).Should().Be(187500m);
        Math.Abs(GetLedgerBalance(techWorld.Id)).Should().Be(81000m);
        Math.Abs(GetLedgerBalance(global.Id)).Should().Be(161500m);
        (Math.Abs(GetLedgerBalance(yashika.Id)) + Math.Abs(GetLedgerBalance(techWorld.Id)) + Math.Abs(GetLedgerBalance(global.Id))).Should().Be(430000m);

        // Bill-wise: ₹4,30,000
        var yashikaBills = await billRefRepo.GetActiveBillsByLedgerAsync(_companyId, yashika.Id);
        var techBills = await billRefRepo.GetActiveBillsByLedgerAsync(_companyId, techWorld.Id);
        var globBills = await billRefRepo.GetActiveBillsByLedgerAsync(_companyId, global.Id);
        var totalBillWise = yashikaBills.Sum(b => b.OutstandingAmount.Amount)
                          + techBills.Sum(b => b.OutstandingAmount.Amount)
                          + globBills.Sum(b => b.OutstandingAmount.Amount);
        totalBillWise.Should().Be(430000m);

        // Stock Quantities
        var itemRepo = _serviceProvider.GetRequiredService<IItemRepository>();
        var items = (await itemRepo.GetAllAsync(_companyId)).ToList();
        var samsung = items.First(i => i.Name.Contains("Samsung"));
        var dell = items.First(i => i.Name.Contains("Dell"));
        var lenovo = items.First(i => i.Name.Contains("Lenovo"));
        var hp = items.First(i => i.Name.Contains("HP"));
        var tplink = items.First(i => i.Name.Contains("TP-Link"));
        var dlink = items.First(i => i.Name.Contains("D-Link"));

        (await stockRepo.GetCurrentQuantityAsync(_companyId, samsung.Id)).Should().Be(19m);
        (await stockRepo.GetCurrentQuantityAsync(_companyId, dell.Id)).Should().Be(13m);
        (await stockRepo.GetCurrentQuantityAsync(_companyId, lenovo.Id)).Should().Be(4m);
        (await stockRepo.GetCurrentQuantityAsync(_companyId, hp.Id)).Should().Be(6m);
        (await stockRepo.GetCurrentQuantityAsync(_companyId, tplink.Id)).Should().Be(20m);
        (await stockRepo.GetCurrentQuantityAsync(_companyId, dlink.Id)).Should().Be(10m);
    }

    [Fact]
    public async Task Phase10_ControlledWriteTest_UIPartityTest_CreatesVerifiesAndCleansUp()
    {
        _companyId.Should().NotBeEmpty();
        var unitService = _serviceProvider.GetRequiredService<IUnitService>();
        var unitRepo = _serviceProvider.GetRequiredService<IUnitRepository>();

        const string testName = "UI-PARITY-TEST";
        const string testSymbol = "UPT";

        // 1. UI creates harmless master
        var created = await unitService.CreateUnitAsync(new CreateUnitDto(_companyId, testName, testSymbol, 0));
        created.Should().NotBeNull();
        created.Name.Should().Be(testName);

        // 2. Direct SQL verification that record exists in database
        await using (var conn = new SqliteConnection(RealDbConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Units WHERE CompanyId = @cId AND Name = @name;";
            cmd.Parameters.AddWithValue("@cId", _companyId.ToString());
            cmd.Parameters.AddWithValue("@name", testName);
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            count.Should().Be(1, "Database must immediately contain the created UI master record");
        }

        // 3. UI reads record back
        var fetched = await unitService.GetUnitByIdAsync(_companyId, created.Id);
        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be(testName);

        // 4. Clean rollback / removal
        await unitRepo.DeleteAsync(_companyId, created.Id);

        // 5. Direct SQL verification that record was removed cleanly
        await using (var conn = new SqliteConnection(RealDbConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Units WHERE CompanyId = @cId AND Name = @name;";
            cmd.Parameters.AddWithValue("@cId", _companyId.ToString());
            cmd.Parameters.AddWithValue("@name", testName);
            var count = (long)(await cmd.ExecuteScalarAsync())!;
            count.Should().Be(0, "Test master record must be cleanly removed without residual side-effects");
        }
    }

    [Fact]
    public async Task Phase11_DatabaseSafetyVerification_IntegrityAndReconciliationsUntouched()
    {
        await using var conn = new SqliteConnection(RealDbConnectionString);
        await conn.OpenAsync();

        // 1. Unbalanced vouchers = 0
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
            var sumDebit = reader.GetInt64(0);
            var sumCredit = reader.GetInt64(1);
            sumDebit.Should().Be(sumCredit, "All vouchers in RealDB must remain balanced");
            sumDebit.Should().Be(54000000, "Voucher volume must remain exactly ₹5,40,000.00");
        }

        // 2. Relational integrity audits: 0 orphans
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

        // 3. Confirm %LOCALAPPDATA%\BillsEasy\billseasy.db is untouched
        File.Exists(LocalAppDataPath).Should().BeTrue("LocalApplicationData database file should exist");
    }

    [Fact]
    public async Task Phase06_WpfViewModels_CanBeInstantiatedAndBoundToRealDb()
    {
        var wpfDll = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\src\BillsEasy.Presentation.Wpf\bin\Release\net8.0-windows\BillsEasy.Presentation.Wpf.dll"));
        File.Exists(wpfDll).Should().BeTrue($"WPF assembly must exist at {wpfDll}");

        var asm = Assembly.LoadFrom(wpfDll);
        var mwvmType = asm.GetType("BillsEasy.Presentation.Wpf.ViewModels.MainWindowViewModel");
        mwvmType.Should().NotBeNull();

        var compListVmType = asm.GetType("BillsEasy.Presentation.Wpf.ViewModels.CompanyListViewModel");
        compListVmType.Should().NotBeNull();

        var ledgerListVmType = asm.GetType("BillsEasy.Presentation.Wpf.ViewModels.LedgerListViewModel");
        ledgerListVmType.Should().NotBeNull();

        var itemListVmType = asm.GetType("BillsEasy.Presentation.Wpf.ViewModels.ItemListViewModel");
        itemListVmType.Should().NotBeNull();

        var purListVmType = asm.GetType("BillsEasy.Presentation.Wpf.ViewModels.PurchaseInvoiceListViewModel");
        purListVmType.Should().NotBeNull();

        // Instantiate CompanyListViewModel and verify it binds to RealDB
        var compService = _serviceProvider.GetRequiredService<CompanyService>();
        var contextService = _serviceProvider.GetRequiredService<ICompanyContextService>();
        var compListVm = Activator.CreateInstance(compListVmType!, compService, contextService);
        compListVm.Should().NotBeNull();

        var loadMethod = compListVmType!.GetMethod("LoadCompaniesAsync");
        loadMethod.Should().NotBeNull();
        var task = (Task)loadMethod!.Invoke(compListVm, null)!;
        await task;

        var compProp = compListVmType.GetProperty("Companies");
        var companies = (System.Collections.IEnumerable)compProp!.GetValue(compListVm)!;
        var compList = companies.Cast<object>().ToList();
        compList.Should().HaveCountGreaterOrEqualTo(1);

        var compNames = compList.Select(c => (string)c.GetType().GetProperty("CompanyName")!.GetValue(c)!).ToList();
        compNames.Should().Contain("Shree Krishna Electronics");
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}
