# BillsEasy — UI & Master Deferred Item Backlog (UI_TODO)
**Status**: Backlog / Future Phase Tracking  
**Phase**: 1.4.3 UX Foundation Reference  
**Scope**: Tracks enterprise and Indian accounting master fields identified in reference software (e.g. BUSY / Tally) that are deferred to maintain strict Phase 1.4 Clean Architecture boundaries.

---

## 1. Company Master Deferred Fields

The following fields are present in traditional Indian accounting ERP reference setups (BUSY 21/18) but are deferred from the current `Company` domain entity until statutory, multi-currency, and invoicing phases:

| Field Name | Category | Reason for Deferral | Target Phase |
| :--- | :--- | :--- | :--- |
| `CurrencySymbol` (₹, Rs., $) | Multi-Currency / Formatting | Current system standardizes on Indian Rupee (`INR` / `₹`) with integer minor units (paise). | Phase 2.x (Multi-Currency) |
| `CurrencySubString` (Paise, Cents) | Print / Number-to-Words | Number-to-words engine deferred until Voucher Printing & Invoicing. | Phase 1.6 (Voucher Printing) |
| `CurrencyFont` | Legacy Print Styling | Legacy DOS/Windows font selector for custom currency glyphs; modern UTF-8 handles `₹` natively. | Not Required / Legacy |
| `BillByBillDefaultCreditDays` | Credit Management | Bill-by-bill outstanding tracking deferred until Phase 1.5 Voucher Engine / Bill Allocation. | Phase 1.5 / 1.6 |
| `DefaultTaxType` / `GSTDefault` | Statutory Invoicing | Master GST defaults will attach when Voucher Invoicing and Tax Engine are introduced. | Phase 1.5+ |
| `InventoryIntegrated` (Yes/No) | Module Boundaries | Phase 1 is pure financial accounting; Inventory modules are deferred to Phase 3. | Phase 3 (Inventory) |
| `RoundOffConfiguration` | Invoicing Rounding | Round-off ledger auto-postings will be defined in Voucher Configuration. | Phase 1.5 |

---

## 2. Ledger Master Deferred Fields

| Field Name | Category | Reason for Deferral | Target Phase |
| :--- | :--- | :--- | :--- |
| `CostCenterApplicable` (Yes/No) | Cost Accounting | Cost center allocation engine is scheduled post-core accounting. | Phase 2.x |
| `BankDetails` (Account No, IFSC, Branch) | Banking / Reconciliation | Auto-bank reconciliation and electronic payment export. | Phase 2.x |
| `TDS/TCS Applicability` | Direct Taxation | Withholding tax engine deferred until statutory taxation milestone. | Phase 2.x |
| `ContactPersons` (Multi-contact list) | CRM / Party Master | Multiple contact persons per party ledger. | Phase 2.x |
| `CreditDaysGrace` | Credit Management | Overdue interest and grace periods. | Phase 1.6 |

---

## 3. Account Group Master Deferred Fields

| Field Name | Category | Reason for Deferral | Target Phase |
| :--- | :--- | :--- | :--- |
| `GroupClassificationTag` (Manufacturing, Trading, Admin) | Financial Statement Formatting | Detailed Schedule III balance sheet and P&L classification grouping. | Phase 1.7 (Reports) |
| `BudgetLimit` | Budgeting & Variance | Group-level annual/quarterly budget tracking. | Phase 2.x |

---

## 4. UI / Interaction Deferred Enhancements

| Feature | Description | Target Phase |
| :--- | :--- | :--- |
| `Custom Theme Swapping` | Dark mode / Slate mode / Classic Tally blue mode. | Post-Phase 1.5 |
| `In-Grid Quick Ledger Creation` | Pressing `F3` inside a voucher line to pop up the Ledger creation dialog on the fly and immediately return with newly created ledger selected. | Phase 1.5 (Voucher Entry) |
| `Grid Column Reordering / Customizer` | User-defined column visibility and widths saved to local SQLite/JSON user preferences. | Phase 1.6 |
