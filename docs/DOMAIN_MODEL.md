# BillsEasy — Domain Model Specification

**Provenance Note:** Domain concepts documented here are synthesized from studied tutorial classes (Classes 1–4) and standard Indian accounting principles.

---

## 1. Core Domain Aggregate Roots & Entities

`
 +-----------------------------------------------------------------------------+
 ¦                             COMPANY (Aggregate Root)                        ¦
 ¦  • Id (Guid)                    • CompanyName (string)                      ¦
 ¦  • PrintName (string)           • ShortName (string)                        ¦
 ¦  • Country (string)             • State (string) & StateCode (string)       ¦
 ¦  • FinancialYearStart (Date)    • BooksBeginningFrom (Date)                 ¦
 ¦  • BaseCurrency (Currency)      • GSTConfig (GSTIN, RegType, TaxPeriodicity)¦
 +-----------------------------------------------------------------------------+
                                        ¦ 1:N
                                        ?
 +-----------------------------------------------------------------------------+
 ¦                         ACCOUNT GROUP (Entity / Tree)                       ¦
 ¦  • Id (Guid)                    • Name (string)                             ¦
 ¦  • ParentGroupId (Guid?)        • Nature (Asset | Liability | Income | Exp) ¦
 ¦  • IsPrimary (bool)             • IsDefaultSystemGroup (bool)               ¦
 +-----------------------------------------------------------------------------+
                                        ¦ 1:N
                                        ?
 +-----------------------------------------------------------------------------+
 ¦                           LEDGER (Aggregate Root)                           ¦
 ¦  • Id (Guid)                    • Name (string)                             ¦
 ¦  • PrintName (string)           • GroupId (Guid)                            ¦
 ¦  • OpeningBalance (Money)       • OpeningBalanceType (Debit | Credit)       ¦
 ¦  • IsBillWiseEnabled (bool)     • CreditPeriodDays (int)                    ¦
 ¦  • CreditLimit (decimal?)       • ContactDetails (Address, Phone, Email)    ¦
 ¦  • StatutoryDetails (GSTIN, PAN, StateCode)                                 ¦
 ¦  • BankDetails (AccountNo, IFSC, BankName, Branch)                          ¦
 +-----------------------------------------------------------------------------+
                    ¦ 1:N                                      ¦ 1:N
                    ? (When BillWiseEnabled)                   ? (Line Items)
 +----------------------------------------+ +----------------------------------+
 ¦            BILL REFERENCE              ¦ ¦           VOUCHER LINE           ¦
 ¦  • Id (Guid)                           ¦ ¦  • Id (Guid)                     ¦
 ¦  • LedgerId (Guid)                     ¦ ¦  • VoucherId (Guid)              ¦
 ¦  • ReferenceNo (string)                ¦ ¦  • LedgerId (Guid)               ¦
 ¦  • ReferenceType (NewRef|AgstRef|...)  ¦ ¦  • LineType (Debit | Credit)     ¦
 ¦  • BillDate (Date)                     ¦ ¦  • Amount (Money)                ¦
 ¦  • DueDate (Date?)                     ¦ ¦  • Narration (string?)           ¦
 ¦  • OriginalAmount (Money)              ¦ +----------------------------------+
 ¦  • Status (Pending | Settled)          ¦                    ¦
 +----------------------------------------+                    ¦ 1:N (Allocations)
                    ¦ 1:N (Allocated Against)                  ?
                    ¦       +--------------------------------------------------+
                    +-------?              BILL ALLOCATION                     ¦
                            ¦  • Id (Guid)                                     ¦
                            ¦  • VoucherLineId (Guid)                          ¦
                            ¦  • BillReferenceId (Guid)                        ¦
                            ¦  • AllocationType (AgainstRef | Advance | OnAcc) ¦
                            ¦  • AllocatedAmount (Money)                       ¦
                            +--------------------------------------------------+
`

---

## 2. Core Entities & Value Object Dictionary

### 2.1 Company
- **Role:** Root tenant/organizational boundary.
- **Invariants:** Every voucher, ledger, and group strictly belongs to one Company. A company defines its FinancialYearStart (typically April 1 in India) and BooksBeginningFrom (date accounting records start).

### 2.2 AccountGroup
- **Role:** Hierarchical classification for the Chart of Accounts.
- **Top-Level Root Classifications:**
  1. Assets (Current Assets, Fixed Assets, Investments, Cash, Bank)
  2. Liabilities (Current Liabilities, Loans, Capital Account)
  3. Income (Direct Income / Sales, Indirect Income)
  4. Expenses (Direct Expenses, Indirect Expenses)
- **Standard Predefined Sub-Groups:**
  - Sundry Debtors (Customers / Accounts Receivable)
  - Sundry Creditors (Suppliers / Accounts Payable)
  - Bank Accounts, Bank Overdraft (OD), Cash-in-hand, Duties & Taxes.

### 2.3 Ledger (The Authoritative Entity for Accounts & Parties)
- **Fundamental Rule:** **There is NO separate \"Party\" entity.**
- A Customer or Vendor is simply a Ledger categorized under Sundry Debtors or Sundry Creditors.
- Party-specific attributes (GSTIN, PAN, Address, Credit Period, Credit Limit, Bill-Wise Flag) are properties of the Ledger.
- **Source of Truth:** All balances (Opening, Current, Closing) and statement histories are grounded in the Ledger and its associated transactional VoucherLine records.

### 2.4 Voucher & VoucherLine
- **Voucher Aggregate:** Represents an atomic financial transaction.
  - Types: Payment, Receipt, Journal, Contra, Sales, Purchase, DebitNote, CreditNote, OpeningBalance.
  - Properties: VoucherNumber, VoucherDate, VoucherType, Narration, CreatedBy, CreatedAt.
- **VoucherLine Entity:** Represents a single debit or credit entry belonging to a voucher.
  - Properties: LedgerId, Amount (positive decimal), EntryType (Debit or Credit), SequenceNumber, LineNarration.
- **Invariant:** $\sum \text{Debit Amounts} == \sum \text{Credit Amounts}$ for every voucher.

### 2.5 BillReference & BillAllocation (Bill-Wise Accounting)
- **BillReference:** Represents an outstanding bill commitment created either via:
  - An opening balance bill entry (Opening Ref)
  - A Sales / Purchase voucher (New Ref)
  - An advance payment / receipt (Advance Ref)
- **BillAllocation:** Represents the application of a payment/receipt VoucherLine against a specific BillReference.
- **Derivation of Outstanding:**
  \text{Bill Outstanding} = \text{OriginalAmount} - \sum_{\text{All Valid Allocations}} \text{AllocatedAmount}

---

## 3. Opening Balance Representation

* **Architectural Decision:** Opening balances are **NOT** stored in a disconnected balance cache table.
* **Mechanism:** When a company or ledger is initialized with an opening balance, the system records an explicit **Opening Balance Voucher** (VoucherType.OpeningBalance):
  - Debit opening balances credit the system Opening Stock / Difference in Opening Balances ledger.
  - Credit opening balances debit the system Difference in Opening Balances ledger.
  - If bill-wise breakdown is provided for an opening balance (e.g. Ramesh has 3 pending unpaid bills from prior years), corresponding BillReference records are created with reference type OpeningRef.
* **Benefit:** Ledger balances, trial balance differences, and bill-wise outstandings flow naturally through the unified double-entry query pipeline.

---

## 4. Derived Values Matrix

| Conceptual Value | Is Stored Authoritatively? | How It Is Derived |
| :--- | :--- | :--- |
| **Ledger Current Balance** | **NO (Derived)** | $\sum (\text{Debit Lines}) - \sum (\text{Credit Lines})$ from all committed non-reversed vouchers up to target date. |
| **Ledger Closing Balance** | **NO (Derived)** | Opening Balance + Net Period Movements for the selected date range. |
| **Bill Remaining Balance** | **NO (Derived)** | $\text{BillReference.OriginalAmount} - \sum \text{BillAllocation.AllocatedAmount}$. |
| **Party Total Outstanding** | **NO (Derived)** | Sum of all unsettled BillReference outstandings for the party ledger. |
| **Difference in Opening Balances** | **NO (Derived)** | Net balance of the system Difference in Opening Balances equity ledger. |
