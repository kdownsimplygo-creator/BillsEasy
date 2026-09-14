# Phase 1.3 Implementation Report: Account Groups & Hierarchy

## 1. Executive Summary
Phase 1.3 of **BillsEasy** has been implemented and fully verified. This phase establishes the Master Data classification backbone for the Chart of Accounts and future Ledgers. It introduces the hierarchical `AccountGroup` aggregate, standard predefined Indian accounting taxonomy seeding, domain cycle detection, delete protection guards, SQLite persistence with cascade/restrict foreign keys, and a native keyboard-first WPF management experience.

In accordance with the Master Project Charter and accounting engine constraints:
- **Master Data Only**: No transaction records, balances, or ledger postings were created in this phase.
- **No Separate Party Entity**: Maintained the invariant that customers/suppliers will be represented as Ledgers under `Sundry Debtors` and `Sundry Creditors`.
- **No Cached Balances**: Balances are NOT stored in AccountGroups; they will be computed from future transactions/vouchers.
- **Zero Invariant Violations**: 100% of automated tests pass (72 tests total: 59 Unit, 13 Integration, 0 build warnings, 0 build errors).

---

## 2. Files Created and Modified

### Domain Layer (`BillsEasy.Domain`)
- [`src/BillsEasy.Domain/Enums/GroupNature.cs`](file:///w:/BillsEasy/src/BillsEasy.Domain/Enums/GroupNature.cs) — Enum for group classification (`Asset = 1`, `Liability = 2`, `Capital = 3`, `Income = 4`, `Expense = 5`).
- [`src/BillsEasy.Domain/Entities/AccountGroup.cs`](file:///w:/BillsEasy/src/BillsEasy.Domain/Entities/AccountGroup.cs) — Hierarchical master data aggregate entity with invariant validation, parent assignment, and system protection.

### Application Layer (`BillsEasy.Application`)
- [`src/BillsEasy.Application/DTOs/AccountGroupDtos.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/DTOs/AccountGroupDtos.cs) — DTOs for group management, tree representation (`AccountGroupNodeDto`), and parent lookups.
- [`src/BillsEasy.Application/Common/Interfaces/IAccountGroupRepository.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Common/Interfaces/IAccountGroupRepository.cs) — Persistence interface for account groups supporting sibling-scoped uniqueness.
- [`src/BillsEasy.Application/Common/Interfaces/IAccountGroupService.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Common/Interfaces/IAccountGroupService.cs) — Application service interface.
- [`src/BillsEasy.Application/Services/AccountGroupService.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Services/AccountGroupService.cs) — Application service enforcing cycle detection, delete protection, and standard group seeding.
- [`src/BillsEasy.Application/Services/CompanyService.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Services/CompanyService.cs) — Integrated default account group seeding upon company creation.

### Infrastructure Layer (`BillsEasy.Infrastructure`)
- [`src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs`](file:///w:/BillsEasy/src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs) — Added SQLite DDL for `AccountGroups` table with partial unique indexes (`UQ_AccountGroups_Company_Parent_Name` and `UQ_AccountGroups_Company_Root_Name`).
- [`src/BillsEasy.Infrastructure/Persistence/Repositories/AccountGroupRepository.cs`](file:///w:/BillsEasy/src/BillsEasy.Infrastructure/Persistence/Repositories/AccountGroupRepository.cs) — Parameterized SQLite repository implementation.

### Presentation Layer (`BillsEasy.Presentation.Wpf`)
- [`src/BillsEasy.Presentation.Wpf/ViewModels/AccountGroupEditViewModel.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/ViewModels/AccountGroupEditViewModel.cs) — Add/Modify group dialog ViewModel with dynamic nature synchronization and descendant filtering.
- [`src/BillsEasy.Presentation.Wpf/ViewModels/AccountGroupListViewModel.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/ViewModels/AccountGroupListViewModel.cs) — List/Tree view ViewModel with live search filter and delete protection dialogs.
- [`src/BillsEasy.Presentation.Wpf/ViewModels/MainWindowViewModel.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/ViewModels/MainWindowViewModel.cs) — Added `ManageAccountGroupsCommand` (`Alt+G`).
- [`src/BillsEasy.Presentation.Wpf/Views/Masters/AccountGroupEditDialog.xaml`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/Views/Masters/AccountGroupEditDialog.xaml) & `.cs` — Modal dialog for creating/editing account groups.
- [`src/BillsEasy.Presentation.Wpf/Views/Masters/AccountGroupManagementDialog.xaml`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/Views/Masters/AccountGroupManagementDialog.xaml) & `.cs` — Master dialog with dual List View and Hierarchy Tree View.
- [`src/BillsEasy.Presentation.Wpf/MainWindow.xaml`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/MainWindow.xaml) — Enabled `Masters -> Account Groups (Alt+G)` menu.
- [`src/BillsEasy.Presentation.Wpf/App.xaml.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/App.xaml.cs) — Registered AccountGroup dependencies in the IoC container.

### Automated Test Suite
- [`tests/BillsEasy.UnitTests/Domain/Entities/AccountGroupTests.cs`](file:///w:/BillsEasy/tests/BillsEasy.UnitTests/Domain/Entities/AccountGroupTests.cs) — Domain invariant tests.
- [`tests/BillsEasy.UnitTests/Application/Services/AccountGroupServiceTests.cs`](file:///w:/BillsEasy/tests/BillsEasy.UnitTests/Application/Services/AccountGroupServiceTests.cs) — Use case tests (sibling uniqueness, cross-parent allowance, circular hierarchy rejection, delete protection, seeding).
- [`tests/BillsEasy.IntegrationTests/Infrastructure/Persistence/AccountGroupRepositoryTests.cs`](file:///w:/BillsEasy/tests/BillsEasy.IntegrationTests/Infrastructure/Persistence/AccountGroupRepositoryTests.cs) — SQLite integration tests (sibling uniqueness constraint, cross-parent persistence, cascade deletion, company isolation).

---

## 3. Account Group Hierarchy & Taxonomy

The standard predefined Indian Chart of Accounts seeded per company comprises:

```text
├── Capital Account [Capital] (Primary)
│   └── Reserves & Surplus [Capital]
│
├── Liabilities [Liability] (Primary)
│   ├── Current Liabilities [Liability]
│   │   ├── Sundry Creditors [Liability]
│   │   ├── Duties & Taxes [Liability]
│   │   ├── Outstanding Expenses [Liability]
│   │   └── Provisions [Liability]
│   └── Loans (Liability) [Liability]
│       ├── Bank OD Accounts [Liability]
│       ├── Secured Loans [Liability]
│       └── Unsecured Loans [Liability]
│
├── Assets [Asset] (Primary)
│   ├── Current Assets [Asset]
│   │   ├── Sundry Debtors [Asset]
│   │   ├── Cash-in-hand [Asset]
│   │   ├── Bank Accounts [Asset]
│   │   ├── Stock-in-hand [Asset]
│   │   └── Loans & Advances (Asset) [Asset]
│   ├── Fixed Assets [Asset]
│   └── Investments [Asset]
│
├── Income [Income] (Primary)
│   ├── Direct Income [Income]
│   │   └── Sales Accounts [Income]
│   └── Indirect Income [Income]
│
└── Expenses [Expense] (Primary)
    ├── Direct Expenses [Expense]
    │   └── Purchase Accounts [Expense]
    └── Indirect Expenses [Expense]
```

---

## 4. Database Schema

```sql
CREATE TABLE IF NOT EXISTS AccountGroups (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    Name TEXT NOT NULL,
    ParentGroupId TEXT,
    Nature INTEGER NOT NULL,
    IsPrimary INTEGER NOT NULL DEFAULT 0,
    IsDefaultSystemGroup INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParentGroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT
);

-- Sibling-Scoped Uniqueness Constraints (Case-Insensitive)
CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Parent_Name
    ON AccountGroups(CompanyId, ParentGroupId, Name COLLATE NOCASE)
    WHERE ParentGroupId IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Root_Name
    ON AccountGroups(CompanyId, Name COLLATE NOCASE)
    WHERE ParentGroupId IS NULL;

CREATE INDEX IF NOT EXISTS IX_AccountGroups_ParentGroupId ON AccountGroups(ParentGroupId);
CREATE INDEX IF NOT EXISTS IX_AccountGroups_CompanyId ON AccountGroups(CompanyId);
```

---

## 5. Domain Invariants & Delete Protection

1. **Company Scoping**: Every Account Group belongs strictly to a single company (`CompanyId != Guid.Empty`).
2. **Sibling-Scoped Uniqueness**: Group names are unique among siblings (groups sharing the same parent or root). Groups under different parents may share a name.
3. **Hierarchy Integrity**:
   - A group cannot be its own parent (`ParentGroupId != Id`).
   - Circular hierarchy is prevented via recursive ancestor chain traversal (`A -> B -> C -> A` rejected).
   - Parent group must exist in the same company.
4. **Delete Protection**:
   - Protected/system groups (`IsDefaultSystemGroup = true`) cannot be deleted.
   - Groups with child sub-groups cannot be deleted (`HasChildrenAsync`).
   - Groups with assigned Ledgers cannot be deleted (`HasLedgersAsync` - future dependency checked).
5. **Nature Inheritance**: Sub-groups automatically inherit the fundamental accounting nature of their parent group.

---

## 6. Phase 1.3 Final Consistency Review

### What Was Inspected
1. **Uniqueness Policy**: Audited `UQ_AccountGroups_Company_Name` vs. the domain requirement ("Duplicate sibling group names must be rejected").
2. **Persistence Constraints**: Evaluated SQLite NULL indexing semantics and case-insensitivity.
3. **Invariants Alignment**: Verified company scoping, parent company matching, self-parent prevention, cycle detection, delete guards, and audit attribution across Domain, Application, Infrastructure, and UI layers.
4. **Source Provenance**: Validated seeded groups against Classes 2–3 training topics.
5. **Architectural Guardrails**: Confirmed zero violations (no Party entity, no separate OpeningBalance entity, no cached group balances, no AccountingEntry entity, clean MVVM without code-behind business logic).

### Corrections & Alignment Made
- **Uniqueness Rule Alignment**: Replaced whole-company unique index with sibling-scoped partial unique indexes (`UQ_AccountGroups_Company_Parent_Name` and `UQ_AccountGroups_Company_Root_Name` with `COLLATE NOCASE`).
- **Application Service & Repository**: Updated `ExistsByNameAsync` to check `(CompanyId, Name, ParentGroupId, ExcludeId)`.
- **Test Suite Expansion**: Added unit and integration tests confirming sibling name collisions are rejected while identical names under different parent groups are permitted.

### Final Verification Results
- **Build**: 0 Errors, 0 Warnings
- **Test Count**: **72 Total Tests** (59 Unit Tests, 13 Integration Tests)
- **Pass Rate**: **100% (72/72 Passing)**

---

## 7. Deferred Items & Future Dependencies
1. **Ledger Master (Stage 1.4)**: `AccountGroup` exposes its `Id` for future `Ledgers.GroupId`. Ledger-based deletion protection currently checks table existence dynamically.
2. **Accounting Balances**: Group balances are not stored in master data and will be aggregated dynamically from `VoucherLine` entries in Phase 1.6/1.8.

---

## 8. Stop Condition
Phase 1.3 implementation and consistency review are complete. Awaiting review and explicit authorization for Phase 1.4.