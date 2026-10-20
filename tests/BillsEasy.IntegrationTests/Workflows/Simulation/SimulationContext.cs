using System;
using System.Collections.Generic;
using BillsEasy.Application.DTOs;

namespace BillsEasy.IntegrationTests.Workflows.Simulation;

/// <summary>
/// State carrier holding entities, IDs, and contextual metadata across all 14 stages of the E2E simulation.
/// </summary>
public class SimulationContext
{
    // Company & FY
    public Guid CompanyId { get; set; }
    public Guid FinancialYearId { get; set; }
    public CompanyDto? Company { get; set; }

    // Account Groups
    public Guid CapitalGroupId { get; set; }
    public Guid BankGroupId { get; set; }
    public Guid DebtorGroupId { get; set; }
    public Guid CreditorGroupId { get; set; }
    public Guid IndirectExpGroupId { get; set; }
    public Guid DirectExpGroupId { get; set; }
    public Guid PurchaseAccountsGroupId { get; set; }

    // Ledgers
    public Guid CapitalLedgerId { get; set; }
    public Guid BankLedgerId { get; set; }
    public Guid CashLedgerId { get; set; }
    public Guid RajLedgerId { get; set; }
    public Guid ApexLedgerId { get; set; }
    public Guid ShivamLedgerId { get; set; }
    public Guid YashikaLedgerId { get; set; }
    public Guid TechWorldLedgerId { get; set; }
    public Guid GlobalSuppliesLedgerId { get; set; }
    public Guid OfficeExpLedgerId { get; set; }
    public Guid FreightLedgerId { get; set; }
    public Guid BankChargesLedgerId { get; set; }
    public Guid PurchaseLedgerId { get; set; }

    // Units
    public Guid PcsUnitId { get; set; }
    public Guid NosUnitId { get; set; }
    public Guid BoxUnitId { get; set; }

    // Item Groups
    public Guid AccessoriesGroupId { get; set; }
    public Guid MonitorsGroupId { get; set; }
    public Guid LaptopsGroupId { get; set; }
    public Guid NetworkingGroupId { get; set; }

    // Items
    public Guid SamsungMonitorItemId { get; set; }
    public Guid DellMonitorItemId { get; set; }
    public Guid LenovoLaptopItemId { get; set; }
    public Guid HpLaptopItemId { get; set; }
    public Guid TpLinkRouterItemId { get; set; }
    public Guid DLinkSwitchItemId { get; set; }

    // Invoices & Vouchers tracked chronologically
    public List<PurchaseInvoiceDto> PurchaseInvoices { get; } = new();
    public List<VoucherDto> Vouchers { get; } = new();
    public List<BillReferenceDto> BillReferences { get; } = new();
    public List<BillAllocationDto> BillAllocations { get; } = new();
}
