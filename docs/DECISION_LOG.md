# BillsEasy Architectural Decision Records (ADRs)

## Status: Active & Authoritative

---

### ADR 01: Bill / BillAllocation Phasing Scope
- **Context**: The `Ledgers` schema contains `BillWiseEnabled`, `CreditPeriodDays`, and `CreditLimit`. However, `Voucher` and `VoucherLine` (Phase 1.5) require a focused double-entry posting engine.
- **Decision**: `BillReferences` and `BillAllocations` are scheduled for **Phase 1.6** after Phase 1.5 (Voucher Engine, Double-Entry Postings, Voucher Imbalance Verification) is completed and verified. `Ledger.BillWiseEnabled` is retained in the schema as master setup data.
- **Impact**: Clean separation between core general ledger posting verification and subsequent subledger bill-by-bill matching.

---

### ADR 02: Minor Units (Paise) Integer Storage for Monetary Columns
- **Context**: SQLite `REAL` is an IEEE-754 floating point number susceptible to precision loss and float drift (e.g. accumulating ₹0.10 sums). Double-entry balance invariants require absolute exactness (Σdebit == Σcredit).
- **Decision**: All monetary columns (`Ledgers.OpeningBalance`, `Ledgers.CreditLimit`, and future `VoucherLines.Amount`) are stored as `INTEGER` representing minor units (paise: ₹100.50 -> 10050) in SQLite, which seamlessly maps to `BIGINT` in PostgreSQL. The Domain layer continues exposing `decimal` via `Money` value objects, with exact integer conversions at the persistence boundary.
- **Impact**: Zero float drift; exact double-entry arithmetic.

---

### ADR 03: Atomic Voucher Numbering Generation
- **Context**: Voucher numbering must be sequential, continuous, and strictly scoped to `(CompanyId, FinancialYearId, VoucherType)`.
- **Decision**: Voucher numbering generation will execute within the same database transaction as the voucher creation, using a dedicated sequence/counter table updated atomically in Phase 1.5. "Read MAX and add 1" patterns outside transactions are prohibited.
- **Impact**: Guarantees zero sequence gaps and full concurrency safety for multi-user / LAN editions.

---

### ADR 04: Distinction Between `AccountGroup.IsPrimary` and `IsDefaultSystemGroup`
- **Context**: Need clear delineation of responsibilities between `IsPrimary` and `IsDefaultSystemGroup`.
- **Decision**:
  - `IsPrimary`: Governs hierarchy topology. A group is primary if and only if `ParentGroupId IS NULL`. The 5 root nature groups (Assets, Liabilities, Capital, Income, Expenses) are primary roots.
  - `IsDefaultSystemGroup`: Governs deletion and nature protection. Predefined standard accounting taxonomy groups cannot be deleted and cannot have their `Nature` mutated.
- **Impact**: Custom primary root groups can be created if needed, while system taxonomy remains immutable.

---

### ADR 05: AadhaarNumber Field Handling & Privacy Compliance
- **Context**: `Ledgers` contains an optional `AadhaarNumber` field for statutory Indian identity verification of proprietorship/individual parties.
- **Decision**: `AadhaarNumber` is retained as an optional statutory party field, but must be masked in UI displays and reports by default (showing only the last 4 digits, e.g. `XXXXXXXX1234`), and restricted from bulk plaintext data exports.
- **Impact**: Compliance with Indian data privacy standards while preserving statutory record-keeping for sole proprietors.

---

### ADR 06: Account Group Nature Consistency & Subtree Protection
- **Provenance**: ARCHITECTURAL RECOMMENDATION (Distinct from legacy source-derived BUSY/Tally behavior).
- **Context**: In financial accounting, all accounts and sub-groups within a branch must strictly share the parent group's fundamental `Nature` (Asset, Liability, Capital, Income, Expense) to guarantee correct Balance Sheet and Profit & Loss classification. Mutating a parent group's nature without managing its subtree risks silently corrupting financial statements.
- **Decision**:
  1. A child `AccountGroup` strictly derives its `Nature` from its immediate parent.
  2. Reparenting updates the group's `Nature` from the new parent.
  3. Attempting to change an `AccountGroup`'s `Nature` (either directly or via reparenting) when the group has child descendants is strictly rejected with a `DomainException`. Descendants must be moved or restructured first.
  4. For leaf groups with no descendants, changing root nature is permitted (provided the group is not a protected system group).
  5. Predefined system groups (`IsDefaultSystemGroup == true`) can never have their nature changed.
- **Impact**: Guarantees 100% hierarchy tree nature consistency without silent recursive subtree corruption.

---

### ADR 07: GSTRegistrationType Schema CHECK Constraint Synchronization
- **Context**: The `GSTRegistrationType` enum in `BillsEasy.Domain.Enums` defines 4 statutory classifications (`Regular=1, Composition=2, Unregistered=3, OverseasOrSEZ=4`). However, `Companies` and `Ledgers` table definitions in SQLite had `CHECK (GSTRegistrationType BETWEEN 1 AND 6)` originating from an early design draft that had contemplated splitting Overseas and SEZ and adding Consumer. Values 5 and 6 had no domain enum members and would silently produce undefined enum values on hydration.
- **Decision**: Narrowed the database `CHECK` constraints on both `Companies` and `Ledgers` tables to `CHECK (GSTRegistrationType BETWEEN 1 AND 4)` to exactly align with the authoritative Domain enum. Indian statutory cross-border/zero-rated trade is consolidated in `OverseasOrSEZ = 4`, and B2C consumers are classified under `Unregistered = 3`.
- **Impact**: Zero database-to-domain drift; invalid integer values (0, 5, 6, 7+) are strictly rejected at the database engine level.

---

### ADR 08: Phase 1.5 Voucher Engine Design Constraints (IsLocked & Context Snapshotting)
- **Context**: Architectural review identified two critical requirements to enforce during Phase 1.5 Voucher Engine implementation:
  1. `FinancialYear.IsLocked` / `SetLocked` exists today but must prevent unauthorized voucher creation or mutation once a financial year is closed/locked.
  2. Ambient context (`ICompanyContextService.ActiveCompany` and `ActiveFinancialYear`) contains mutable state with no thread locking; re-reading context mid-operation could result in split-tenant data corruption.
- **Decision**:
  1. **Domain-Level `IsLocked` Verification**: In Phase 1.5, when a Voucher is posted or modified, the entity's domain logic (either in the constructor or a `Post()` method) must explicitly check the target `FinancialYear.IsLocked` and throw a `DomainException` if true. This invariant belongs strictly in the Domain layer, not solely in an Application service.
  2. **Immutable Context Snapshotting**: Any Application service orchestrating voucher creation or posting must snapshot `CompanyId` and `FinancialYearId` as plain, immutable local variables at the very beginning of the operation, rather than repeatedly querying `ICompanyContextService` across asynchronous steps.
  3. **FK Strategy for Historical Accounting Preservation**: In the Phase 1.5 schema, `Voucher.FinancialYearId` will enforce `REFERENCES FinancialYears(Id) ON DELETE RESTRICT`. Combined with the `CompanyService.DeleteCompanyAsync` transaction guard, this ensures no company with recorded transactions can ever be deleted or cascade-purged.
- **Impact**: Guarantees tamper-proof financial year locking, concurrency safety across async service boundaries, and absolute preservation of historical accounting records.
