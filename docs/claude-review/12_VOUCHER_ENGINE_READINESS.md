# 12. Voucher Engine Readiness & Hard Constraints

## 1. Readiness Assessment: READY FOR PHASE 1.5
The current foundation (Phases 1.1–1.4) provides all necessary prerequisites for the Voucher Engine:
- `Company` and `FinancialYear` boundaries are established.
- `AccountGroup` hierarchy is complete.
- `Ledger` is the single authoritative entity representing both nominal accounts and trading parties.
- SQLite foreign key rules and WAL concurrency are verified.

---

## 2. Hard Architectural Constraints for Phase 1.5 (Must Lock Before Coding)

```
                       +-------------------+
                       |      COMPANY      |
                       +-------------------+
                                 |
                                 | 1..N
                                 v
                       +-------------------+
                       |  FINANCIAL YEAR   |
                       +-------------------+
                                 |
                                 | 1..N
                                 v
+-------------------+  1..N   +-------------------+
|      LEDGER       |<--------|      VOUCHER      |
|  (Account/Party)  |         | (Header / Number) |
+-------------------+         +-------------------+
         ^                             |
         |                             | 1..N
         | 1                           v
         +--------------------+-------------------+
                              |   VOUCHER LINE    |
                              | (Dr/Cr Line Entry)|
                              +-------------------+
```

### Hard Constraints:
1. **Double-Entry Balance Invariant:**
   Every posted voucher MUST satisfy:
   $$\sum_{\text{lines}} \text{Debit Amount} == \sum_{\text{lines}} \text{Credit Amount} > 0$$
2. **Immutability & Integrity:**
   Voucher lines MUST NOT be updated in isolation. The `Voucher` is the aggregate root; lines are modified or replaced exclusively through the aggregate.
3. **No Cached Balance Columns:**
   Do **NOT** add `CurrentBalance`, `ClosingBalance`, or `Balance` columns to `Ledgers` or `Companies`.
4. **Voucher Numbering:**
   Voucher numbering must be scoped to `(CompanyId, FinancialYearId, VoucherType)`.
5. **Financial Year Lock Guard:**
   Voucher creation, modification, or cancellation in a locked financial year (`IsLocked == true`) MUST be rejected.
6. **Date Boundaries:**
   `VoucherDate` must fall within `[FinancialYear.StartDate, FinancialYear.EndDate]`.
