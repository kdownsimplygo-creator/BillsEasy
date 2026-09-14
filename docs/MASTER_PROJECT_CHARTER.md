# BillsEasy — Master Project Charter

**Product Name:** BillsEasy  
**Target Market:** Indian Small & Medium Enterprises (SMEs), Traders, Accountants, and Tax Practitioners  
**Target Platform:** Native Windows Desktop (.NET 8.0 LTS / WPF / C# / SQLite embedded)  
**Document Classification:** Foundational Governance & Architecture Mandate  
**Author:** Lead Engineering Architect  

---

## 1. Executive Summary & Vision

BillsEasy is a native Windows desktop accounting application built specifically for the Indian market. The product is engineered to deliver high-velocity, keyboard-first data entry, absolute mathematical and double-entry accounting integrity, full Indian statutory compliance (GST, TDS/TCS, MCA Audit Trail), and zero-configuration single-PC deployment with a seamless path toward multi-user LAN operation.

The project follows an **evidence-driven, incremental development philosophy**:
\text{LEARN} \longrightarrow \text{UNDERSTAND} \longrightarrow \text{VALIDATE} \longrightarrow \text{MODEL} \longrightarrow \text{TEST} \longrightarrow \text{BUILD} \longrightarrow \text{REVIEW} \longrightarrow \text{EXPAND}

We systematically ingest domain requirements through structured tutorial classes and statutory references while ensuring strict clean-room implementation.

---

## 2. Core Project Objectives

1. **High-Performance Native Windows Desktop UX:** Keyboard-centric operation with sub-50ms screen responses, instant autocomplete filtering, dense data visualization, and standardized keyboard navigation (Enter advances, Esc backs out, standard accounting hotkeys).
2. **Double-Entry & Mathematical Invariants:** Total Debits must equal Total Credits per voucher. No balance or outstanding amount is ever stored as a disconnected cached integer that can drift out of sync.
3. **Single Source of Truth Accounting:**
   - A Party (Customer/Vendor) is a Ledger categorized under an AccountGroup (e.g., Sundry Debtors / Sundry Creditors). No parallel \"Party\" entity competing for identity or balance.
   - Financial transactions are represented exclusively by Voucher and VoucherLine.
   - Opening balances are represented as initializing vouchers within the accounting engine, eliminating duplicate balance storage.
   - Bill-wise outstanding is dynamically derivable from original bills and allocation entries.
4. **Clean-Room & Intellectual Property Independence:** Independent design and clean-room implementation derived strictly from publicly available business tutorials and statutory guidelines without copying proprietary code, trade secrets, or binary assets.
5. **Zero-Configuration Desktop Deployment:** Phase 1 uses embedded SQLite with zero external database server configuration required. The architecture isolates persistence concerns to enable future PostgreSQL migration for LAN editions without rewriting domain logic.

---

## 3. Incremental Phasing Strategy

### Phase 1: Accounting Foundation (Current Scope)
- Application startup and local workspace discovery
- Company Management (Creation, Selection, Configuration, Books Beginning)
- Financial Year Context & Boundary Navigation
- Account Group Hierarchy (Primary & Sub-Groups)
- Ledger Masters (General, Debtors, Creditors, Bank, Cash, Tax, Expense)
- Opening Balance Engine (via integrated accounting vouchers)
- Bill-Wise Configuration & Reference Tracking
- Core Voucher Foundation (Voucher, VoucherLine, Double-Entry Validation)
- Payment & Receipt Vouchers with Bill-Wise Allocation
- Foundational Ledger Register & Outstanding Receivables/Payables Views

### Phase 2: Commercial Vouchers & Indian GST Engine (Future)
- Sales Invoices (B2B, B2C, Tax Invoice, Bill of Supply)
- Purchase Invoices & Input Tax Credit (ITC) categorization
- Indian GST Tax Calculation Engine (CGST, SGST, IGST, UTGST, RCM)
- Multi-rate tax invoicing & HSN/SAC statutory summaries
- Credit Notes & Debit Notes

### Phase 3: Inventory & Multi-Location Management (Future)
- Stock Groups, Categories, and Stock Items
- Base Units & Compound Unit Conversions (e.g., Box $\leftrightarrow$ Pcs)
- Godowns (Warehouses) & Multi-location tracking
- Batch & Expiry Management (Pharma/FMCG)
- Item-level discounts, schemes, and landed cost valuation (FIFO, Weighted Avg)

### Phase 4: Statutory MIS, Financial Reporting & Audit Trail (Future)
- Day Book, Cash/Bank Book, Group Summaries
- Trial Balance, Trading & Profit & Loss Account, Balance Sheet (Schedule III & T-Format)
- GST Return Formats (GSTR-1, GSTR-3B json/csv exports)
- MCA-compliant Audit Trail / Edit Log engine
- User Access Control, Multi-role security, and multi-user LAN deployment (PostgreSQL)

---

## 4. Architectural Non-Negotiables

* **No Premature Over-Engineering:** No microservices, no distributed messaging buses, no cloud lock-in, no redundant CQRS frameworks.
* **No Authoritative Cached Balances:** Balances are calculated from transactional lines or verified against transactional rollups.
* **Automated Test Guardrails:** Every accounting invariant must be verified with xUnit automated unit and integration tests before UI binding.
* **Preserve Cross-FY Continuity:** Bill references and outstanding tracking span financial years without destructive balance resets.
