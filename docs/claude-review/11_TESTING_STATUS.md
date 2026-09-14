# 11. Testing Status & Test Suite Coverage

## 1. Test Suite Summary
- **Total Tests:** **99**
- **Unit Tests (`BillsEasy.UnitTests`):** **80**
- **Integration Tests (`BillsEasy.IntegrationTests`):** **19**
- **Failed / Skipped:** **0** (100% Passing)
- **Build Status:** 0 Errors, 0 Warnings.

---

## 2. Coverage Matrix by Domain Area

| Domain Area | Unit Tests | Integration Tests | Tested Invariants & Edge Cases |
|---|---|---|---|
| **`Money` Value Object** | 8 | - | 2-decimal rounding, negative rejection, arithmetic (`+`, `-`, `*`), comparison (`<`, `>`). |
| **`AuditableEntity`** | 2 | - | UpdateAudit user and timestamp mutation. |
| **`Company` & `FinancialYear`** | 15 | 6 | Date bounds (`BooksBeginningFrom >= FinancialYearBeginning`), GSTIN regex, single active year atomic toggle, non-overlapping financial years, company deletion cascade to financial years. |
| **`AccountGroup` & Hierarchy** | 24 | 6 | 25+ standard group taxonomy seeding, sibling uniqueness within parent, root group uniqueness, cycle detection on parent update, system group protection, deletion restricted by child groups and ledgers. |
| **`Ledger` Master** | 31 | 5 | Unified party/account model, 15-char GSTIN validation, negative balance/credit period rejection, default system ledger protection (`Cash`, `P&L`), delete guards when transactions or bills exist, company isolation. |
| **SQLite Connection & WAL** | - | 2 | Connection open, WAL mode pragma, foreign keys enabled pragma, table creation and insert. |

---

## 3. Important Invariants Not Yet Tested (Deferred to Phase 1.5+)
1. `Voucher` double-entry balancing ($\sum \text{Debits} == \sum \text{Credits}$).
2. `VoucherLine` foreign key to `Ledgers` and `FinancialYears`.
3. Financial year lock enforcement during voucher posting.
4. Bill-wise reference allocation and outstanding balance tracking.
