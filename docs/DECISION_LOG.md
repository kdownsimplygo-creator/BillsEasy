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

---

### ADR 09: Minimal Database Schema Migration Strategy (SchemaVersion & Versioned Scripts)
- **Context**: The existing database schema is created via `CREATE TABLE IF NOT EXISTS` in `DatabaseInitializer.cs`. While sufficient for initial table provisioning, `CREATE TABLE IF NOT EXISTS` cannot apply `ALTER TABLE` statements (e.g. adding columns, modifying constraints, or index mutations) against deployed SQLite databases that already contain production master data.
- **Decision**: When Phase 1.5-B or subsequent phases require modifying existing tables:
  1. A dedicated metadata table `_SchemaVersion (Version INTEGER PRIMARY KEY, AppliedAt TEXT NOT NULL, Description TEXT NOT NULL)` will be introduced into `DatabaseInitializer`.
  2. Migrations will be structured as sequentially numbered, idempotent, transactional script steps (e.g., `001_Initial_Foundation.sql`, `002_Add_Vouchers.sql`, etc.) executed within an atomic SQLite transaction.
  3. No heavyweight ORM or third-party migration packages (such as EF Core Migrations or DbUp) will be added; the runner will remain a zero-dependency, parameterized C# runner using `Microsoft.Data.Sqlite` in `BillsEasy.Infrastructure`.
  4. Greenfield/test databases will apply all versioned steps from version 0 to current in order, ensuring test databases and deployed customer databases follow the identical evolutionary path.
- **Impact**: Provides safe, linear schema evolution without data loss or heavyweight dependencies, while deferring actual implementation until the first `ALTER TABLE` is required.

---

### ADR 10: Relational Foreign Key & Cascade Deletion Invariants (Single Source of Truth)
- **Context**: Independent review and Pre-Phase 1.5 Hardening identified the need for an authoritative, single source of truth governing all relational `ON DELETE` actions across masters and transactional tables in Phase 1.5-B.
- **Decision**: The following matrix is ratified as the permanent single source of truth for relational delete actions:

| Relationship | Parent Entity | Child Entity | Current FK Action | Planned Phase 1.5 FK Action | Rationale / Invariant |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Company → FinancialYear** | `Companies` | `FinancialYears` | `ON DELETE CASCADE` | `ON DELETE CASCADE` (master setup only) | Financial years are master temporal containers belonging to the company. |
| **2. Company → AccountGroup** | `Companies` | `AccountGroups` | `ON DELETE CASCADE` | `ON DELETE CASCADE` | Pre-transaction chart of accounts setup cascades on company purge. |
| **3. Company → Ledger** | `Companies` | `Ledgers` | `ON DELETE CASCADE` | `ON DELETE CASCADE` | Master setup accounts cascade on company deletion *only when clean*. |
| **4. Group → SubGroup** | `AccountGroups` | `AccountGroups` | `ON DELETE RESTRICT` | `ON DELETE RESTRICT` | Parent groups cannot be deleted if child groups exist. |
| **5. Group → Ledger** | `AccountGroups` | `Ledgers` | `ON DELETE RESTRICT` | `ON DELETE RESTRICT` | Groups cannot be deleted while referenced by ledgers. |
| **6. Company → Voucher** | `Companies` | `Vouchers` | *(Phase 1.5)* | **`ON DELETE RESTRICT`** | **CRITICAL:** A company that contains posted transactions can NEVER be silently deleted. |
| **7. FY → Voucher** | `FinancialYears` | `Vouchers` | *(Phase 1.5)* | **`ON DELETE RESTRICT`** | A financial year containing vouchers can never be deleted or cascade-purged. |
| **8. Voucher → VoucherLine** | `Vouchers` | `VoucherLines` | *(Phase 1.5)* | **`ON DELETE CASCADE`** | Voucher and its lines form a single atomic transaction aggregate. |
| **9. Ledger → VoucherLine** | `Ledgers` | `VoucherLines` | *(Phase 1.5)* | **`ON DELETE RESTRICT`** | A ledger referenced in any financial voucher can NEVER be deleted. |

- **Impact**: Guarantees zero historical accounting data loss, prevents cascade-purging companies with active ledger transactions, and strictly defines deletion boundaries before table creation in Phase 1.5-B. (Tracked testing debt for transaction guards documented in `docs/TESTING_DEBT.md`).

---

### ADR 11: Subledger Bill-Wise Accounting Invariants & Lifecycle (Phase 1.6A)
- **Context**: Phase 1.6 introduces commercial bill-by-bill tracking linked to double-entry general ledger vouchers.
- **Decision**:
  1. **Clean Architecture Separation of Responsibilities**:
     - `BillAllocation` domain entity enforces *intrinsic invariants only* (`Id`, `CompanyId`, `VoucherLineId`, `BillReferenceId` non-empty, `AllocatedAmount > 0`).
     - `BillWiseService` (Application Layer) is the *primary owner of business invariants*: existence, active status, complete allocation identity, accounting direction, and state-dependent outstanding limits.
  2. **Allocation Identity Invariant**:
     $$\text{BillAllocation.CompanyId} == \text{BillReference.CompanyId} == \text{VoucherLine.CompanyId}$$
     $$\text{BillReference.LedgerId} == \text{settlement VoucherLine.LedgerId}$$
     Enforced strictly in Application layer; database provides independent relational integrity constraints.
  3. **Single Source of Truth for Outstanding Balances**:
     Outstanding balances are computed dynamically as $\text{OriginalAmount} - \sum \text{AllocatedAmount}$ via indexed SQL aggregation. No mutable cached balance columns are stored.
  4. **Definitive Voucher Cancellation Lifecycle**:
     - Originating voucher: If active allocations exist, cancellation is rejected with `DomainException`. If zero allocations exist, associated bill is soft-cancelled (`Status = Cancelled`).
     - Settlement voucher: Cancellation/deletion deletes associated `BillAllocation` records, automatically restoring the source bill's dynamic outstanding balance with zero drift.
  5. **ADR 10 Deletion Rules Matrix Expansion**:
     - `Company → BillReference`: `ON DELETE RESTRICT`
     - `Ledger → BillReference`: `ON DELETE RESTRICT` (verified by `LedgerRepository.HasBillReferencesAsync`)
     - `VoucherLine → BillReference`: `ON DELETE RESTRICT`
     - `Company → BillAllocation`: `ON DELETE RESTRICT`
     - `VoucherLine → BillAllocation`: `ON DELETE CASCADE`
     - `BillReference → BillAllocation`: `ON DELETE RESTRICT`
- **Impact**: Clean, robust, drift-free bill-wise accounting fully integrated with the core double-entry engine.


