# BillsEasy — Source Provenance & Domain Evidence Matrix

---

## 1. Classification Categories

Every domain concept, field, workflow, and architectural choice in BillsEasy is classified under one of the following provenance categories:

1. **SOURCE-DERIVED**: Directly demonstrated and observed in studied tutorial material (Classes 1–4).
2. **DOMAIN INFERENCE**: Logical and structural deduction derived directly from observed source workflows.
3. **GENERAL INDUSTRY KNOWLEDGE**: Standard Indian statutory rules (GSTN, ICAI, Companies Act) and double-entry accounting mathematics.
4. **ARCHITECTURAL RECOMMENDATION**: Engineering best practice designed to guarantee performance, maintainability, and clean architecture.
5. **NOT YET VERIFIED**: Anticipated concept or future capability not yet demonstrated in studied classes.

---

## 2. Ingested Evidence & Provenance Inventory

| Domain Area | Concept / Field / Workflow | Provenance Classification | Source / Justification |
| :--- | :--- | :--- | :--- |
| **Company** | Company Name, Print Name, Short Name, Country, State | SOURCE-DERIVED | BUSY Tutorial Class 1 |
| **Company** | Financial Year Beginning, Books Beginning From | SOURCE-DERIVED | BUSY Tutorial Class 1 |
| **Company** | Base Currency Symbol (?), Currency Sub-unit (Paise) | SOURCE-DERIVED | BUSY Tutorial Class 1 |
| **Company** | GST Configuration (GSTIN, Type, State Code) | SOURCE-DERIVED | BUSY Tutorial Class 1 |
| **Company** | Company Security & User Authentication Toggle | SOURCE-DERIVED | BUSY Tutorial Class 1 |
| **Account Groups** | Primary Groups (Assets, Liabilities, Income, Expenses) | GENERAL INDUSTRY KNOWLEDGE | Double-entry accounting standard |
| **Account Groups** | Sub-groups (Sundry Debtors, Sundry Creditors, Bank, Cash, etc.) | SOURCE-DERIVED | BUSY Tutorial Class 2 & 3 |
| **Account Groups** | Group Creation, Modification, Deletion hierarchy | SOURCE-DERIVED | BUSY Tutorial Class 3 |
| **Ledger** | Ledger Name, Print Name, Group Classification | SOURCE-DERIVED | BUSY Tutorial Class 2 |
| **Ledger** | Opening Balance & Balance Type (Dr/Cr) | SOURCE-DERIVED | BUSY Tutorial Class 2 |
| **Ledger** | Address (Line 1/2, City, State, Pincode), Mobile, WhatsApp, Email | SOURCE-DERIVED | BUSY Tutorial Class 2 |
| **Ledger** | Credit Period (Days), Credit Limit (?) | SOURCE-DERIVED | BUSY Tutorial Class 2 |
| **Ledger** | Bill-by-Bill (Bill-Wise) Enablement Toggle | SOURCE-DERIVED | BUSY Tutorial Class 2 & 3 |
| **Ledger** | Bank Details (Account No, IFSC, Branch) | SOURCE-DERIVED | BUSY Tutorial Class 2 |
| **Ledger as Party** | Unifying Customer / Vendor / Account under Ledger | ARCHITECTURAL RECOMMENDATION | Eliminates dual source of truth & balance sync bugs |
| **Bill-Wise** | Bill/Reference Number, Due Date, Original Amount | SOURCE-DERIVED | BUSY Tutorial Class 3 |
| **Bill-Wise** | Multiple outstanding bills per party | SOURCE-DERIVED | BUSY Tutorial Class 3 |
| **Bill-Wise** | Cross-FY persistence of unsettled bill references | DOMAIN INFERENCE | Required for continuous business operation |
| **Voucher** | Payment Voucher (Cash payment & Bank payment) | SOURCE-DERIVED | BUSY Tutorial Class 4 |
| **Voucher** | Double-entry Debit/Credit accounting verification | SOURCE-DERIVED | BUSY Tutorial Class 4 |
| **Voucher** | Allocation of single payment across multiple pending bills | SOURCE-DERIVED | BUSY Tutorial Class 4 |
| **Voucher** | Voucher Modification & Deletion workflows | SOURCE-DERIVED | BUSY Tutorial Class 4 |
| **Voucher** | Atomic Double-Entry Core (Voucher & VoucherLine) | ARCHITECTURAL RECOMMENDATION | Clean Architecture domain core |
| **Reporting** | Party Dashboard, Pending Bills View, Payment History | SOURCE-DERIVED | BUSY Tutorial Class 4 |
| **Reporting** | Ledger Account Statement with Running Balance | SOURCE-DERIVED | BUSY Tutorial Class 4 |
| **Security** | User Attribution (CreatedBy, CreatedAt, ModifiedBy, ModifiedAt) | ARCHITECTURAL RECOMMENDATION | Essential for auditability |
| **Audit Trail** | Unalterable Edit Log capturing field-level changes | GENERAL INDUSTRY KNOWLEDGE | MCA Statutory Mandate for corporate entities |
| **GST Engine** | Multi-tier CGST/SGST/IGST tax calculation on sales/purchase | NOT YET VERIFIED | Scheduled for future tutorial classes |
| **Inventory** | Stock items, batch/expiry, compound units, godowns | NOT YET VERIFIED | Scheduled for future tutorial classes |
