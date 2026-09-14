# Indian Accounting Software Domain Analysis & Reference Models

**Project:** BillsEasy  
**Purpose:** Domain knowledge baseline distilled from established Indian accounting ecosystems (BUSY, TallyPrime, Marg ERP).  
**Compliance & Ethics Note:** All information is sourced exclusively from public product documentation, statutory guidelines, and official help portals. No proprietary code, trade secrets, or binary assets are used or referenced.

---

## 1. Domain Taxonomy & Conceptual Foundations

Indian accounting software differs fundamentally from generic Western bookkeeping software (like QuickBooks or Xero) due to statutory nuances, hyper-keyboard navigation habits of accountants, multi-tier tax classifications (GST, TCS, TDS), and strict audit trail requirements (MCA guidelines).

`
                        +----------------------------------------------+
                        ¦              Company / Entity                ¦
                        +----------------------------------------------+
                                               ¦
               +---------------------------------------------------------------+
               ?                                                               ?
  +-------------------------+                                     +-------------------------+
  ¦   Financial Accounting  ¦                                     ¦   Inventory Management  ¦
  +-------------------------¦                                     +-------------------------¦
  ¦ • Primary/Sub Groups    ¦                                     ¦ • Stock Groups          ¦
  ¦ • Ledgers (Debtors/etc.)¦                                     ¦ • Stock Items           ¦
  ¦ • Voucher Types         ¦                                     ¦ • Units (Simple/Compound)¦
  ¦ • Cost Centers          ¦                                     ¦ • Godowns / Locations   ¦
  ¦ • Tax Masters (GST/TDS) ¦                                     ¦ • Batches & Serial Nos  ¦
  +-------------------------+                                     +-------------------------+
               ¦                                                               ¦
               +---------------------------------------------------------------+
                                               ?
                                  +-------------------------+
                                  ¦   Voucher Entry Engine  ¦
                                  +-------------------------¦
                                  ¦ • Sales / Purchase      ¦
                                  ¦ • Receipt / Payment     ¦
                                  ¦ • Contra / Journal      ¦
                                  ¦ • Credit / Debit Notes  ¦
                                  +-------------------------+
                                               ?
                                  +-------------------------+
                                  ¦    Statutory & MIS      ¦
                                  +-------------------------¦
                                  ¦ • GSTR-1, GSTR-3B       ¦
                                  ¦ • Day Book, Trial Bal   ¦
                                  ¦ • P&L, Balance Sheet    ¦
                                  ¦ • Receivables/Payables  ¦
                                  +-------------------------+
`

---

## 2. Core Master Structures

### 2.1 Ledger Hierarchy (Chart of Accounts)
- **Primary Groups:**
  - Assets (Current Assets, Fixed Assets, Investments, Misc. Expenses)
  - Liabilities (Current Liabilities, Loans/Borrowings, Capital Account)
  - Income (Direct Income / Sales, Indirect Income)
  - Expenses (Direct Expenses / Cost of Goods Sold, Indirect Expenses)
- **Standard Pre-defined Subgroups:**
  - Sundry Debtors (Customers / Accounts Receivable)
  - Sundry Creditors (Suppliers / Accounts Payable)
  - Cash-in-hand
  - Bank Accounts / Bank Overdraft
  - Duties & Taxes (CGST, SGST, IGST, Cess, TDS, TCS)
  - Stock-in-hand
- **Party Ledger Attributes:**
  - Trade Name & Legal Name
  - Registration Type (Regular, Composition, Unregistered, Overseas/SEZ)
  - GSTIN (15-character statutory alphanumeric code)
  - State & State Code (Crucial for Place of Supply computation)
  - PAN, Aadhaar, Credit Limit, Credit Days, Opening Balance (Dr/Cr)

### 2.2 Inventory Masters
- **Stock Item Properties:**
  - Item Name & Alias/Barcode/SKU
  - Stock Group & Category
  - Unit of Measurement (Base Unit e.g., PCS, KGS, BOX)
  - Alternate Unit & Conversion Factor (e.g., 1 BOX = 10 PCS)
  - HSN/SAC Code (4, 6, or 8 digits)
  - Taxability Type (Taxable, Nil Rated, Exempted, Non-GST)
  - GST Rate Schedule (Integrated Tax % + State Tax % + Central Tax % + Cess %)
  - Valuation Method (FIFO, Moving Average, Standard Cost)
  - Batch / Lot Tracking (Manufacturing Date, Expiry Date, Batch No)

---

## 3. Voucher Concepts & Rules

| Voucher Type | Typical Use Case | Core Accounting Impact | Statutory / GST Impact |
| :--- | :--- | :--- | :--- |
| **Sales Voucher (Tax Invoice)** | B2B & B2C taxable sales of goods/services | Dr Customer / Cash<br>Cr Sales Account<br>Cr Output CGST/SGST/IGST | Generates B2B/B2C data for GSTR-1 & E-Invoice |
| **Purchase Voucher** | Receiving taxable goods/services from vendor | Dr Purchase Account<br>Dr Input CGST/SGST/IGST<br>Cr Vendor / Bank | Captures Input Tax Credit (ITC) for GSTR-3B / 2B matching |
| **Receipt Voucher** | Receiving money from customer/debtor | Dr Bank / Cash<br>Cr Customer Ledger | Can handle Advance Receipts (with GST if applicable) |
| **Payment Voucher** | Paying supplier, expenses, or taxes | Dr Vendor / Expense / Tax<br>Cr Bank / Cash | Updates vendor balance, bank reconciliation |
| **Contra Voucher** | Internal money transfer (Cash to Bank, Bank to Bank) | Dr Receiving Bank/Cash<br>Cr Source Bank/Cash | No external tax implication |
| **Journal Voucher** | Non-cash adjustments, depreciation, opening entries | Dr Adjusting Account<br>Cr Target Account | Rectification & year-end closing entries |
| **Credit Note** | Sales return, rate difference, volume discount | Dr Sales Return / Rebate<br>Dr Output Tax<br>Cr Customer | Decreases tax liability in GSTR-1 |
| **Debit Note** | Purchase return, rate escalation, supplier discount | Dr Supplier<br>Cr Purchase Return<br>Cr Input Tax | Reverses ITC in GSTR-3B |

---

## 4. Indian GST Workflow & Tax Computation Engine

### 4.1 Place of Supply (POS) Logic
- **Intra-State Supply:** Company State Code == Place of Supply State Code
  \text{Total GST} = \text{CGST} (\text{Rate}/2) + \text{SGST} (\text{Rate}/2)
- **Inter-State Supply:** Company State Code $\neq$ Place of Supply State Code
  \text{Total GST} = \text{IGST} (\text{Rate})
- **Special Cases:** SEZ Units, Deemed Exports, High Sea Sales, Reverse Charge Mechanism (RCM).

### 4.2 Rounding & Calculations
- Item-level vs Invoice-level rounding (standard Indian practice allows rounding off invoice total to nearest ?1 via a Round Off ledger).
- HSN summary aggregation table mandatory on tax invoices.

---

## 5. Reporting & User Expectations

1. **Books of Account:**
   - Day Book (Chronological record of all daily transactions)
   - Cash Book & Bank Book
   - General Ledger / Party Ledger with running balance (Dr/Cr)
2. **Financial Statements:**
   - Trial Balance (Group-wise / Ledger-wise)
   - Profit & Loss Statement (Trading & P&L)
   - Balance Sheet (Schedule III / Traditional T-Format)
3. **MIS & Operational Reports:**
   - Outstanding Bills (Receivables / Payables Aging Analysis: 0-30, 31-60, 61-90, 90+ days)
   - Stock Summary / Inventory Movement / Low Stock Reorder Alerts
   - GST Return Summaries: GSTR-1 (Outward), GSTR-3B (Summary computation)

---

## 6. Ergonomics & UX Paradigms in Indian Desktop Accounting

- **Keyboard-First Workflow:** Accounting operators expect full keyboard navigation without lifting their hands to use the mouse.
  - Enter advances to next field; Esc moves back or closes modal.
  - Standard hotkeys: Alt+C (Create Master on the fly from dropdown), Alt+A (Add Row), Alt+D (Delete Row).
  - Search-as-you-type fuzzy filtering for Ledgers and Stock Items.
