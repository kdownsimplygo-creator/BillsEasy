# 15. Review Questions for Claude

When performing the independent architectural review of BillsEasy Phases 1.1–1.4, Claude should specifically evaluate and answer the following questions:

## 1. Accounting Domain & Entity Modeling
1. Does the unified `Ledger` aggregate (treating Customers as ledgers under Sundry Debtors and Suppliers as ledgers under Sundry Creditors) completely eliminate the need for a separate `Party` table without creating unmanageable field sparseness for nominal accounts?
2. Is the storage of `OpeningBalance` and `OpeningBalanceType` on the `Ledger` aggregate sound as initial setup master data, provided it is never mutated during normal voucher transactions?
3. In cross-financial-year transitions (e.g. FY 2026-27 to FY 2027-28), should the opening balances for the new FY be generated as synthetic opening vouchers or dynamically calculated from historical voucher lines?

## 2. Relational Schema & Invariants
4. Are the SQLite indexes and composite unique constraints (`UQ_AccountGroups_Company_Parent_Name`, `UQ_AccountGroups_Company_Root_Name`, `UQ_Ledgers_Company_Name`) optimal and complete?
5. Does the foreign key constraint `ON DELETE RESTRICT` on `AccountGroups(ParentGroupId)` and `Ledgers(GroupId)` adequately protect against accidental cascade deletion of accounting hierarchy?
6. Should `Money` values in SQLite be stored as `REAL` (floating point) or as `INTEGER` (in minor units / paise) to completely prevent binary floating point drift in high-volume transaction aggregations?

## 3. Voucher Engine Readiness (Phase 1.5 Gate)
7. Are there any hidden aggregate boundary flaws or missing master fields in `Company`, `FinancialYear`, `AccountGroup`, or `Ledger` that would force breaking schema changes once `Voucher` and `VoucherLine` are implemented?
8. Is the single active financial year toggle (`SetActiveAsync`) and financial year lock mechanism (`IsLocked`) sufficient to govern transaction posting boundaries?

## 4. Overall Architectural Recommendation
9. Should BillsEasy proceed to Phase 1.5:
   - **[A] GO** — Proceed directly to Phase 1.5 Voucher Engine.
   - **[B] GO WITH REQUIRED CHANGES** — Minor adjustments required before starting Phase 1.5.
   - **[C] NO-GO** — Fundamental redesign required.
