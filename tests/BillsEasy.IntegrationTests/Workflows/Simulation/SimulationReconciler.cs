using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;

namespace BillsEasy.IntegrationTests.Workflows.Simulation;

public class SimulationReconciler
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IBillReferenceRepository _billReferenceRepository;
    private readonly IStockMovementRepository _stockMovementRepository;

    public SimulationReconciler(
        IVoucherRepository voucherRepository,
        ILedgerRepository ledgerRepository,
        IBillReferenceRepository billReferenceRepository,
        IStockMovementRepository stockMovementRepository)
    {
        _voucherRepository = voucherRepository;
        _ledgerRepository = ledgerRepository;
        _billReferenceRepository = billReferenceRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    public async Task<decimal> CalculateLedgerBalanceAsync(Guid companyId, Guid ledgerId)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(companyId, ledgerId);
        ledger.Should().NotBeNull();

        var vouchers = await _voucherRepository.GetAllAsync(companyId, ledgerId: ledgerId);

        decimal balance = 0m;
        // Apply opening balance
        if (ledger!.OpeningBalance.Amount > 0)
        {
            balance = ledger.OpeningBalanceType == DebitCreditType.Debit
                ? ledger.OpeningBalance.Amount
                : -ledger.OpeningBalance.Amount;
        }

        // Apply transaction lines
        foreach (var v in vouchers)
        {
            foreach (var l in v.Lines.Where(x => x.LedgerId == ledgerId))
            {
                if (l.Type == DebitCreditType.Debit)
                    balance += l.Amount.Amount;
                else
                    balance -= l.Amount.Amount;
            }
        }

        return balance;
    }

    public async Task VerifyAccountingEqualityAsync(Guid companyId)
    {
        var vouchers = await _voucherRepository.GetAllAsync(companyId);

        decimal totalDebits = 0m;
        decimal totalCredits = 0m;

        foreach (var v in vouchers)
        {
            v.IsBalanced.Should().BeTrue($"Voucher {v.VoucherNumber} must be balanced");
            totalDebits += v.TotalDebit.Amount;
            totalCredits += v.TotalCredit.Amount;
        }

        totalDebits.Should().Be(totalCredits, "Total debits must equal total credits across all vouchers");
        (totalDebits - totalCredits).Should().Be(0m);
    }

    public async Task VerifyCashReconciliationAsync(Guid companyId, Guid cashLedgerId)
    {
        var balance = await CalculateLedgerBalanceAsync(companyId, cashLedgerId);
        // Expected Cash: Opening 1,00,000 + Apex 20,000 - Office Expense 12,500 = 1,07,500 Dr
        balance.Should().Be(107500m, "Cash balance must reconcile to ₹1,07,500");
    }

    public async Task VerifyBankReconciliationAsync(Guid companyId, Guid bankLedgerId)
    {
        var balance = await CalculateLedgerBalanceAsync(companyId, bankLedgerId);
        // Expected Bank: Opening 7,00,000 - Yashika 30,000 - TechWorld 50,000 + Raj 40,000 - Bank Charges 2,500 = 6,57,500 Dr
        balance.Should().Be(657500m, "Bank balance must reconcile to ₹6,57,500");
    }

    public async Task VerifyCustomerReconciliationAsync(Guid companyId, Guid rajId, Guid apexId)
    {
        var rajBal = await CalculateLedgerBalanceAsync(companyId, rajId);
        rajBal.Should().Be(35000m, "Raj Electronics receivable must reconcile to ₹35,000 (75,000 - 40,000)");

        var apexBal = await CalculateLedgerBalanceAsync(companyId, apexId);
        apexBal.Should().Be(30000m, "Apex Computers receivable must reconcile to ₹30,000 (50,000 - 20,000)");

        (rajBal + apexBal).Should().Be(65000m, "Total debtor receivable must equal ₹65,000");
    }

    public async Task VerifySupplierAndBillWiseReconciliationAsync(
        Guid companyId,
        Guid yashikaId,
        Guid techWorldId,
        Guid globalId)
    {
        // 1. Yashika: Op 80,000 + Pur1 68,000 - Pay 30,000 + Pur2 69,500 = 1,87,500 Cr (represented as -187500 in balance)
        var yashikaBal = await CalculateLedgerBalanceAsync(companyId, yashikaId);
        Math.Abs(yashikaBal).Should().Be(187500m, "Yashika ledger payable must reconcile to ₹1,87,500");

        var yashikaBills = await _billReferenceRepository.GetActiveBillsByLedgerAsync(companyId, yashikaId);
        var yashikaBillSum = yashikaBills.Sum(b => b.OutstandingAmount.Amount);
        yashikaBillSum.Should().Be(187500m, "Yashika bill-wise outstanding must equal ₹1,87,500");

        // Specific Yashika bill breakdowns
        var yashOp = yashikaBills.FirstOrDefault(b => b.Bill.ReferenceNumber == "OPEN-YASHIKA-001");
        yashOp.Should().NotBeNull();
        yashOp!.OutstandingAmount.Amount.Should().Be(80000m);

        var yashP1 = yashikaBills.FirstOrDefault(b => b.Bill.ReferenceNumber == "PUR/YASH/001");
        yashP1.Should().NotBeNull();
        yashP1!.TotalAllocated.Amount.Should().Be(30000m);
        yashP1.OutstandingAmount.Amount.Should().Be(38000m);

        var yashP2 = yashikaBills.FirstOrDefault(b => b.Bill.ReferenceNumber == "PUR/YASH/002");
        yashP2.Should().NotBeNull();
        yashP2!.TotalAllocated.Amount.Should().Be(0m);
        yashP2.OutstandingAmount.Amount.Should().Be(69500m);

        // 2. TechWorld: Op 45,000 + Pur 86,000 - Pay 50,000 = 81,000 Cr
        var techBal = await CalculateLedgerBalanceAsync(companyId, techWorldId);
        Math.Abs(techBal).Should().Be(81000m, "TechWorld ledger payable must reconcile to ₹81,000");

        var techBills = await _billReferenceRepository.GetActiveBillsByLedgerAsync(companyId, techWorldId);
        var techBillSum = techBills.Sum(b => b.OutstandingAmount.Amount);
        techBillSum.Should().Be(81000m, "TechWorld bill-wise outstanding must equal ₹81,000");

        var techOp = techBills.FirstOrDefault(b => b.Bill.ReferenceNumber == "OPEN-TECHWORLD-001");
        techOp.Should().NotBeNull();
        techOp!.OutstandingAmount.Amount.Should().Be(45000m);

        var techP1 = techBills.FirstOrDefault(b => b.Bill.ReferenceNumber == "PUR/TECH/001");
        techP1.Should().NotBeNull();
        techP1!.TotalAllocated.Amount.Should().Be(50000m);
        techP1.OutstandingAmount.Amount.Should().Be(36000m);

        // 3. Global Computer Supplies: Pur 1,61,500 = 1,61,500 Cr
        var globalBal = await CalculateLedgerBalanceAsync(companyId, globalId);
        Math.Abs(globalBal).Should().Be(161500m, "Global Computer Supplies ledger payable must reconcile to ₹1,61,500");

        var globalBills = await _billReferenceRepository.GetActiveBillsByLedgerAsync(companyId, globalId);
        var globalBillSum = globalBills.Sum(b => b.OutstandingAmount.Amount);
        globalBillSum.Should().Be(161500m, "Global Computer Supplies bill-wise outstanding must equal ₹1,61,500");

        var globalP1 = globalBills.FirstOrDefault(b => b.Bill.ReferenceNumber == "PUR/GLOB/001");
        globalP1.Should().NotBeNull();
        globalP1!.OutstandingAmount.Amount.Should().Be(161500m);

        // Total Supplier Creditors
        var totalSupplierCreditors = Math.Abs(yashikaBal) + Math.Abs(techBal) + Math.Abs(globalBal);
        totalSupplierCreditors.Should().Be(430000m, "Total supplier ledger liability must equal ₹4,30,000");

        var totalBillWiseOutstanding = yashikaBillSum + techBillSum + globalBillSum;
        totalBillWiseOutstanding.Should().Be(430000m, "Total supplier bill-wise outstanding must equal ₹4,30,000");
    }

    public async Task VerifyPurchasesAndFreightReconciliationAsync(
        Guid companyId,
        Guid purchaseLedgerId,
        Guid freightLedgerId)
    {
        var purchaseBal = await CalculateLedgerBalanceAsync(companyId, purchaseLedgerId);
        // Expected Purchases: 67,500 + 85,000 + 1,60,000 + 69,100 = 3,81,600
        purchaseBal.Should().Be(381600m, "Purchase ledger debit balance must equal ₹3,81,600");

        var freightBal = await CalculateLedgerBalanceAsync(companyId, freightLedgerId);
        // Expected Freight: 500 + 1,000 + 1,500 + 400 = 3,400
        freightBal.Should().Be(3400m, "Freight & Forwarding Charges debit balance must equal ₹3,400");

        (purchaseBal + freightBal).Should().Be(385000m, "Total purchase + freight liability incurred must equal ₹3,85,000");
    }

    public async Task VerifyPhysicalInventoryReconciliationAsync(
        Guid companyId,
        SimulationContext ctx)
    {
        var samsungQty = await _stockMovementRepository.GetCurrentQuantityAsync(companyId, ctx.SamsungMonitorItemId);
        samsungQty.Should().Be(19m, "Samsung M21' Monitor stock quantity must equal 19 Pcs (10 + 5 + 4)");

        var dellQty = await _stockMovementRepository.GetCurrentQuantityAsync(companyId, ctx.DellMonitorItemId);
        dellQty.Should().Be(13m, "Dell 24-inch Monitor stock quantity must equal 13 Pcs (5 + 8)");

        var lenovoQty = await _stockMovementRepository.GetCurrentQuantityAsync(companyId, ctx.LenovoLaptopItemId);
        lenovoQty.Should().Be(4m, "Lenovo Business Laptop stock quantity must equal 4 NOS (3 + 1)");

        var hpQty = await _stockMovementRepository.GetCurrentQuantityAsync(companyId, ctx.HpLaptopItemId);
        hpQty.Should().Be(6m, "HP Business Laptop stock quantity must equal 6 NOS (2 + 3 + 1)");

        var tpLinkQty = await _stockMovementRepository.GetCurrentQuantityAsync(companyId, ctx.TpLinkRouterItemId);
        tpLinkQty.Should().Be(20m, "TP-Link Router stock quantity must equal 20 Pcs (10 + 10)");

        var dLinkQty = await _stockMovementRepository.GetCurrentQuantityAsync(companyId, ctx.DLinkSwitchItemId);
        dLinkQty.Should().Be(10m, "D-Link 8 Port Switch stock quantity must equal 10 Pcs (5 + 5)");
    }
}
