# BillsEasy — Accounting Engine Invariants & Constraints Specification

**Mandate:** This document defines the non-negotiable rules and invariants enforced by the BillsEasy accounting core.  
**Rule for Future Implementations:** No developer, refactoring tool, or AI assistant may introduce shortcuts, cached balance overrides, or non-balanced transactions that violate these specifications.

---

## 1. Double-Entry & Mathematical Invariants

### Invariant 1.1: Atomic Voucher Balance
Every committed accounting voucher must satisfy:
\sum_{i=1}^{n} \text{DebitAmount}_i = \sum_{j=1}^{m} \text{CreditAmount}_j \quad \text{where } \text{Amount} > 0
* **Validation Point:** Enforced in domain entity factory/constructor and validated before persisting to SQLite.
* **Prohibited Behavior:** No voucher may contain only debits or only credits. No voucher line may have an amount $\le 0$ (negative amounts are forbidden; use inverse entry types).

### Invariant 1.2: Minimum Line Cardinality
A voucher must have at least 2 lines (at least 1 Debit line and at least 1 Credit line), except in compound multi-split vouchers where $ Debits balance $ Credits.

---

## 2. Balance Derivation Invariants

### Invariant 2.1: Balances Are Never Authoritatively Stored
* **Rule:** There is no column named CurrentBalance in the Ledgers table acting as the primary source of truth.
* **Computation:** A ledger balance is always derived as:
  \text{Balance}(\text{LedgerId}, \text{AsOfDate}) = \sum \text{Debits} - \sum \text{Credits} \quad \text{for } \text{VoucherDate} \le \text{AsOfDate}
* **Nature Interpretation:**
  - Asset & Expense Ledgers: Positive balance = Debit ($), Negative balance = Credit ($).
  - Liability, Equity & Income Ledgers: Positive balance = Credit ($), Negative balance = Debit ($).
* **Prohibited Behavior:** Writing update triggers or background jobs that directly overwrite ledger balances without voucher line entries.

---

## 3. Bill-Wise Accounting & Allocation Invariants

### Invariant 3.1: Bill Outstanding Derivation
For any BillReference:
\text{OutstandingAmount} = \text{OriginalAmount} - \sum \text{AllocatedAmounts}

### Invariant 3.2: Over-Allocation Prevention
\sum \text{AllocatedAmount} \le \text{OriginalAmount}
* **Rule:** A payment or receipt line cannot allocate an amount greater than the pending outstanding amount of the target bill reference unless explicitly classified as a separate Advance or On Account reference.
* **Multi-Bill Allocation:** When a single payment of ?50,000 is distributed across Bill 001 (?30,000) and Bill 002 (?20,000), two distinct BillAllocation records must be tied to the same VoucherLineId, and the sum of allocated amounts must exactly equal the VoucherLine.Amount.

### Invariant 3.3: Reference Integrity
A BillAllocation can only link a VoucherLine to a BillReference if both belong to the exact same LedgerId.

---

## 4. Opening Balance Invariants

### Invariant 4.1: Unified Ledger Opening Representation
Opening balances are created through an explicit initialization voucher (VoucherType.OpeningBalance or special balance initialization transaction) dated on or before BooksBeginningFrom.

### Invariant 4.2: Bill-Wise Breakdown for Opening Balances
If a party ledger has an opening balance of ?1,00,000 and the user specifies 3 prior unpaid bills:
- Bill A: ?40,000
- Bill B: ?35,000
- Bill C: ?25,000
The sum of OriginalAmount across the 3 BillReference records must strictly equal ?1,00,000.

---

## 5. Financial Year Boundaries & Continuity Invariants

### Invariant 5.1: Non-Destructive Year Transition
Moving from Financial Year 2024-25 to 2025-26 must **NOT** delete, truncate, or duplicate pending bills.
- BillReference entities persist across fiscal years.
- Outstanding bills remain open until settled by payments, regardless of which financial year the payment is recorded in.

### Invariant 5.2: Carry-Forward Integrity
- Real accounts (Assets, Liabilities, Capital) carry their closing balances forward as the opening balances of the subsequent year.
- Nominal accounts (Income, Expenses) close into the Profit & Loss / Retained Earnings summary at year-end.

---

## 6. Voucher Lifecycle: Modification, Deletion & Reversal

### Invariant 6.1: Immutable Historical Records Under Audit
When an accounting transaction is modified:
1. The transaction must validate that all new lines remain balanced ( = Credit$).
2. Existing BillAllocation links must be released/re-allocated within the same atomic database transaction.
3. An audit record capturing the previous state (OldValue), new state (NewValue), user ID, and timestamp must be recorded.

### Invariant 6.2: Deletion with Dependent Allocations
A voucher that created a BillReference (e.g. Sales Invoice or Opening Ref) cannot be deleted if subsequent payments/allocations have already settled against that bill, unless those payments are deleted/re-allocated first.

---

## 7. Database Transaction & Concurrency Invariants

### Invariant 7.1: Atomic Unit of Work
All voucher operations (Header insert/update, Line inserts/updates, Bill allocations, Audit logging) must execute within an explicit database transaction (BEGIN IMMEDIATE TRANSACTION in SQLite).
* If any assertion fails, the entire transaction rolls back cleanly with zero partial state.

### Invariant 7.2: Database Engine Pragmas for SQLite
- PRAGMA foreign_keys = ON; (Strict referential integrity)
- PRAGMA journal_mode = WAL; (Write-Ahead Logging for high concurrency and crash durability)
- PRAGMA synchronous = NORMAL;

---

## 8. User Attribution Invariants

### Invariant 8.1: Audit Metadata Completeness
Every domain entity inheriting from AuditableEntity must populate:
- CreatedAt (UTC Timestamp)
- CreatedBy (User ID / Operator Name)
- ModifiedAt (Nullable UTC Timestamp)
- ModifiedBy (Nullable User ID / Operator Name)
