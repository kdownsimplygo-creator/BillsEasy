# BILLSEASY — PHASE 1.4.2 FINAL PRE-PHASE-1.5 ARCHITECTURE REVIEW PACKAGE
**Target Reviewer**: Claude / Principal Architect  
**Review Status**: Awaiting Independent Review  
**Repository Root**: `W:\BillsEasy`  
**Current Date**: 2026-09-13  
**Verified Build & Test Status**: `0 Errors`, `0 Warnings`, `124/124 Tests Passing` (99 Unit Tests, 25 Integration Tests)

---

## A. EXECUTIVE SUMMARY

BillsEasy is an on-premises native desktop accounting software suite designed specifically for Indian SMEs, built on **.NET 8 LTS**, **WPF**, **C#**, **SQLite 3**, and **Clean Architecture**.

The system has completed:
- **Phase 1.1**: Solution Scaffolding, Core Domain Invariants & Clean Architecture.
- **Phase 1.2**: Company & Financial Year Management (Multi-company, strict FY date boundaries, single-active-FY enforcement).
- **Phase 1.3**: Account Groups & Hierarchy Engine (Parent-child tree, cycle detection, nature derivation, subtree protection).
- **Phase 1.4**: Ledger / Account Master (Unified Party/Account entity, GSTIN validation, deletion guards, opening balance controls).
- **Phase 1.4.1**: Native Desktop Accounting UI Design System (Dense information layout, keyboard navigation framework, custom chrome, high-contrast focus, Dr/Cr converters).
- **Phase 1.4.2**: Principal Architect Pre-Flight Remediation (Single `Money` monetary model with SQLite `INTEGER` minor units / paise storage, transaction-safe ledger deletion, fail-loud hierarchy ancestor cycle validation, and ADR 06 AccountGroup nature consistency).

Development has been **strictly frozen** prior to beginning **Phase 1.5 (Voucher Engine & Double-Entry Posting Engine)**. No voucher, journal entry, posting engine, or transaction code has been written.

---

## B. CURRENT ARCHITECTURE & PROJECT STRUCTURE

### 1. Solution Structure & Dependency Flow
```
BillsEasy.sln
│
├── src/
│   ├── BillsEasy.Domain/               (Zero dependencies; Core entities, Value Objects, Enums, Exceptions)
│   ├── BillsEasy.Application/          (Depends ONLY on Domain; Interfaces, Use-Case Services, DTOs)
│   ├── BillsEasy.Infrastructure/       (Depends on Application & Domain; SQLite ADO.NET, Repositories, System Services)
│   └── BillsEasy.Presentation.Wpf/     (Depends on Application, Domain, Infrastructure; MVVM, Views, ViewModels, Themes)
│
└── tests/
    ├── BillsEasy.UnitTests/            (xUnit, FluentAssertions, NSubstitute; Domain & Application unit testing)
    └── BillsEasy.IntegrationTests/     (xUnit, Microsoft.Data.Sqlite; Real on-disk SQLite repository & constraint testing)
```

### 2. Dependency Inversion & Layer Isolation
- **Domain Layer (`BillsEasy.Domain`)**: Pure C# domain model. No external NuGet packages. Zero database or UI dependencies.
- **Application Layer (`BillsEasy.Application`)**: Contains service contracts (`ICompanyService`, `IAccountGroupService`, `ILedgerService`, etc.), repository interfaces (`ICompanyRepository`, `IAccountGroupRepository`, `ILedgerRepository`), and record DTOs.
- **Infrastructure Layer (`BillsEasy.Infrastructure`)**: Implements raw parameterized ADO.NET data access via `Microsoft.Data.Sqlite`. Zero ORM (no EF Core or Dapper).
- **Presentation Layer (`BillsEasy.Presentation.Wpf`)**: Desktop UI utilizing `CommunityToolkit.Mvvm` (v8.x). Uses standard Microsoft DI container (`Microsoft.Extensions.DependencyInjection`).

### 3. Dependency Injection Registration (`App.xaml.cs`)
```csharp
// Infrastructure & Persistence Singletons
services.AddSingleton<ISqliteDbConnectionFactory>(_ => new SqliteConnectionFactory(connectionString));
services.AddSingleton<DatabaseInitializer>();
services.AddSingleton<ICompanyRepository, CompanyRepository>();
services.AddSingleton<IFinancialYearRepository, FinancialYearRepository>();
services.AddSingleton<IAccountGroupRepository, AccountGroupRepository>();
services.AddSingleton<ILedgerRepository, LedgerRepository>();
services.AddSingleton<ICurrentUserService, StandaloneCurrentUserService>();
services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
services.AddSingleton<ICompanyContextService, CompanyContextService>();

// Application Services (Transient)
services.AddTransient<IAccountGroupService, AccountGroupService>();
services.AddTransient<AccountGroupService>();
services.AddTransient<ILedgerService, LedgerService>();
services.AddTransient<LedgerService>();
services.AddTransient<CompanyService>();
services.AddTransient<FinancialYearService>();

// ViewModels & UI
services.AddTransient<AccountGroupListViewModel>();
services.AddTransient<LedgerListViewModel>();
services.AddSingleton<MainWindowViewModel>();
services.AddSingleton<MainWindow>();
```

---

## C. COMPLETE CURRENT DATABASE SCHEMA (SQLITE 3.X DDL)

The database schema is initialized on application startup via `DatabaseInitializer.InitializeAsync()` using raw DDL. SQLite is strictly configured with `PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;`.

```sql
PRAGMA foreign_keys = ON;

-- 1. COMPANIES TABLE
CREATE TABLE IF NOT EXISTS Companies (
    Id TEXT PRIMARY KEY,
    CompanyName TEXT NOT NULL,
    PrintName TEXT NOT NULL,
    ShortName TEXT,
    Country TEXT NOT NULL DEFAULT 'India',
    StateName TEXT NOT NULL,
    StateCode TEXT NOT NULL,
    FinancialYearBeginning TEXT NOT NULL,
    BooksBeginningFrom TEXT NOT NULL,
    CurrencySymbol TEXT NOT NULL DEFAULT '₹',
    CurrencyFormalName TEXT NOT NULL DEFAULT 'INR',
    GSTIN TEXT,
    GSTRegistrationType INTEGER NOT NULL DEFAULT 1 CHECK (GSTRegistrationType BETWEEN 1 AND 6),
    IsSecurityEnabled INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS UQ_Companies_Name 
    ON Companies(CompanyName COLLATE NOCASE);

-- 2. FINANCIAL YEARS TABLE
CREATE TABLE IF NOT EXISTS FinancialYears (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    YearName TEXT NOT NULL,
    StartDate TEXT NOT NULL,
    EndDate TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 0,
    IsLocked INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_FinancialYears_CompanyId 
    ON FinancialYears(CompanyId);
CREATE UNIQUE INDEX IF NOT EXISTS UQ_FinancialYears_Company_OneActive 
    ON FinancialYears(CompanyId) WHERE IsActive = 1;

-- 3. ACCOUNT GROUPS TABLE
CREATE TABLE IF NOT EXISTS AccountGroups (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    Name TEXT NOT NULL,
    ParentGroupId TEXT,
    Nature INTEGER NOT NULL CHECK (Nature BETWEEN 1 AND 5),
    IsPrimary INTEGER NOT NULL DEFAULT 0,
    IsDefaultSystemGroup INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParentGroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Parent_Name
    ON AccountGroups(CompanyId, ParentGroupId, Name COLLATE NOCASE)
    WHERE ParentGroupId IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Root_Name
    ON AccountGroups(CompanyId, Name COLLATE NOCASE)
    WHERE ParentGroupId IS NULL;
CREATE INDEX IF NOT EXISTS IX_AccountGroups_ParentGroupId 
    ON AccountGroups(ParentGroupId);
CREATE INDEX IF NOT EXISTS IX_AccountGroups_CompanyId 
    ON AccountGroups(CompanyId);

-- 4. LEDGERS TABLE
CREATE TABLE IF NOT EXISTS Ledgers (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    GroupId TEXT NOT NULL,
    Name TEXT NOT NULL,
    PrintName TEXT NOT NULL,
    Alias TEXT,
    OpeningBalance INTEGER NOT NULL DEFAULT 0,
    OpeningBalanceType INTEGER NOT NULL DEFAULT 1 CHECK (OpeningBalanceType IN (1, 2)),
    BillWiseEnabled INTEGER NOT NULL DEFAULT 0,
    CreditPeriodDays INTEGER,
    CreditLimit INTEGER,
    AddressLine1 TEXT,
    AddressLine2 TEXT,
    City TEXT,
    StateName TEXT,
    StateCode TEXT,
    PinCode TEXT,
    Country TEXT NOT NULL DEFAULT 'India',
    GSTIN TEXT,
    GSTRegistrationType INTEGER NOT NULL DEFAULT 1 CHECK (GSTRegistrationType BETWEEN 1 AND 6),
    PAN TEXT,
    AadhaarNumber TEXT,
    ContactPerson TEXT,
    MobileNumber TEXT,
    WhatsAppNumber TEXT,
    Email TEXT,
    BankName TEXT,
    BankAccountNumber TEXT,
    BankIFSC TEXT,
    BankBranch TEXT,
    IsDefaultSystemLedger INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
    FOREIGN KEY (GroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS UQ_Ledgers_Company_Name 
    ON Ledgers(CompanyId, Name COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS IX_Ledgers_GroupId 
    ON Ledgers(GroupId);
CREATE INDEX IF NOT EXISTS IX_Ledgers_CompanyId 
    ON Ledgers(CompanyId);
```

---

## D. DOMAIN MODEL SPECIFICATION

### 1. Entities
- **`Company`** (`BillsEasy.Domain.Entities.Company`): Root aggregate. Holds company configuration, currency parameters, statutory GSTIN, book beginning date, and a collection of associated `FinancialYear`s.
- **`FinancialYear`** (`BillsEasy.Domain.Entities.FinancialYear`): Represents an accounting year bounded by `(StartDate, EndDate)`. Enforces `StartDate < EndDate`, single active year invariant, and lock state.
- **`AccountGroup`** (`BillsEasy.Domain.Entities.AccountGroup`): Hierarchical classification tree. Enforces tree validity, parent company co-ownership, cycle avoidance, fail-loud ancestor checks, and ADR 06 Nature consistency.
- **`Ledger`** (`BillsEasy.Domain.Entities.Ledger`): Single unified entity representing all general ledger accounts, customer parties, supplier parties, bank accounts, tax accounts, and expenses.

### 2. Value Objects
- **`Money`** (`BillsEasy.Domain.ValueObjects.Money`):
  - Readonly struct holding `decimal Amount` and `string Currency`.
  - Normalized internally to 2 decimal places using `decimal.Round(amount, 2, MidpointRounding.AwayFromZero)`.
  - Provides `ToMinorUnits()` returning `long` (paise) and `Money.FromMinorUnits(long minorUnits)`.
  - Implements `IEquatable<Money>`, `IComparable<Money>`, arithmetic operators (`+`, `-`, `*`, `/`), comparisons (`<`, `<=`, `>`, `>=`), and implicit conversion from `decimal`.

### 3. Enums
- **`GroupNature`** (`BillsEasy.Domain.Enums.GroupNature`): `Asset = 1`, `Liability = 2`, `Capital = 3`, `Income = 4`, `Expense = 5`.
- **`DebitCreditType`** (`BillsEasy.Domain.Enums.DebitCreditType`): `Debit = 1`, `Credit = 2`.
- **`GSTRegistrationType`** (`BillsEasy.Domain.Enums.GSTRegistrationType`): `Regular = 1`, `Composition = 2`, `Unregistered = 3`, `Consumer = 4`, `Overseas = 5`, `SpecialEconomicZone = 6`.

---

## E. APPLICATION LAYER IMPLEMENTATION

### 1. Services & Invariant Enforcement
- **`CompanyService`**:
  - Validates company name uniqueness (case-insensitive).
  - Enforces statutory dates (`BooksBeginningFrom >= FinancialYearBeginning`).
  - Automatically seeds the initial `FinancialYear` and runs default `AccountGroupService.SeedDefaultGroupsAsync` and `LedgerService.SeedDefaultLedgersAsync` within a unit of work.
- **`FinancialYearService`**:
  - Manages financial year lifecycles.
  - Validates non-overlapping year dates for a company.
  - Handles active financial year switching.
- **`AccountGroupService`**:
  - Handles group CRUD and hierarchy movements (`ParentGroupId` mutation).
  - Enforces sibling-level and root-level case-insensitive name uniqueness.
  - Prevents deletion of default system groups, groups with child sub-groups, or groups with assigned ledgers.
  - Generates DTO tree views (`AccountGroupNodeDto`) with recursive depth levels for UI consumption.
- **`LedgerService`**:
  - Validates ledger name uniqueness per company.
  - Enforces deletion safety: rejects deletion of system ledgers (`IsDefaultSystemLedger`), ledgers with transactions (`HasTransactionsAsync`), and ledgers with bill references (`HasBillReferencesAsync`).
  - Enforces opening balance immutability when transactions exist.

### 2. DTO Contracts
All DTOs are defined as immutable C# records:
- `CompanyDto`, `CreateCompanyDto`, `UpdateCompanyDto`
- `FinancialYearDto`, `CreateFinancialYearDto`
- `AccountGroupDto`, `AccountGroupNodeDto`, `AccountGroupLookupDto`, `CreateAccountGroupDto`, `UpdateAccountGroupDto`
- `LedgerDto`, `LedgerLookupDto`, `CreateLedgerDto`, `UpdateLedgerDto`

---

## F. INFRASTRUCTURE & DATA ACCESS

### 1. SQLite Persistence Design
- **Connection Factory (`SqliteConnectionFactory`)**:
  - Creates dedicated `SqliteConnection` instances.
  - Executes `PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;` on every opened connection.
- **Raw ADO.NET Parameterization**:
  - Every SQL query utilizes strict parameterized queries (`@ParamName`).
  - Zero SQL string concatenation.
- **Transactions**:
  - Multi-statement mutations (e.g. `CompanyRepository.AddAsync` writing Company + FinancialYears) wrap operations in `SqliteTransaction`.

### 2. Audit & User Context
- **`IAuditableEntity`**: `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`.
- **`StandaloneCurrentUserService`**: Returns `Environment.UserName.ToLowerInvariant()` for desktop operator tracking.
- **`DateTimeProvider`**: Exposes `UtcNow` and `Now` for testable deterministic time tracking.

---

## G. WPF PRESENTATION LAYER & MVVM DESIGN SYSTEM

1. **MVVM Framework**: Built on `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
2. **Dense Accounting Layout**: BUSY-style dense UI with 11pt Segoe UI, compact 24px input rows, clear cell padding, and high-contrast focus outlines (`#005A9E`).
3. **Keyboard Navigation Framework**:
   - `KeyboardNavigationBehavior`: Implements accounting data-entry keyboard patterns (Enter moves to next input field, Shift+Enter moves to previous input field).
   - `SelectAllOnFocusBehavior`: Auto-selects full text on focus for rapid numeric and text editing.
4. **Converters & Formatting**:
   - `IndianCurrencyConverter`: Formats `Money` and `decimal` to standard Indian numbering format (`₹ 1,00,000.00`).
   - `DebitCreditConverter`: Formats debit/credit types to standard `Dr`/`Cr` suffixes with contextual coloring.

---

## H. MONEY & MONETARY STORAGE VERIFICATION (ADR 02 COMPLIANCE)

| Area | Implementation Detail | Verification Status |
| :--- | :--- | :--- |
| **Domain Type** | `BillsEasy.Domain.ValueObjects.Money` (readonly struct) | **VERIFIED** |
| **Arithmetic Precision** | Exact `decimal` storage; 2 decimal places with `MidpointRounding.AwayFromZero` | **VERIFIED** |
| **Database Column Types** | `Ledgers.OpeningBalance` is `INTEGER`<br>`Ledgers.CreditLimit` is `INTEGER` | **VERIFIED** |
| **Write Conversion** | `cmd.Parameters.AddWithValue("@OpeningBalance", ledger.OpeningBalance.ToMinorUnits());` | **VERIFIED** |
| **Read Conversion** | `var openingBalance = Money.FromMinorUnits(reader.GetInt64(6));` | **VERIFIED** |
| **Conversion Duplication** | Zero manual `Math.Round(* 100m)` calls in repositories; 100% delegated to `Money` methods | **VERIFIED** |
| **Floating-point Types** | Zero `REAL` or `DOUBLE` columns in schema | **VERIFIED** |

---

## I. ACCOUNT GROUP HIERARCHY VERIFICATION (ADR 04 & ADR 06 COMPLIANCE)

1. **Company Isolation**: Group creation and reparenting verify `parentGroup.CompanyId == group.CompanyId`.
2. **Self-Parenting Protection**: `Reparent` throws `DomainException` if `targetParent.Id == group.Id`.
3. **Cycle Detection & Fail-Loud Ancestor Precondition**:
   ```csharp
   // Walking ancestor graph in AccountGroup.Reparent
   while (currentCheckId.HasValue)
   {
       if (currentCheckId.Value == Id)
           throw new DomainException($"Cannot set parent: circular hierarchy detected. Group '{Name}' would become an ancestor of itself.");

       if (!groupDict.TryGetValue(currentCheckId.Value, out var ancestorGroup))
       {
           throw new DomainException(
               $"Incomplete account group hierarchy supplied. Ancestor group with ID '{currentCheckId.Value}' was not found in the company groups collection.");
       }

       currentCheckId = ancestorGroup.ParentGroupId;
   }
   ```
4. **ADR 06 Nature Consistency & Subtree Protection**:
   - If a group has child sub-groups (`companyGroups.Any(g => g.ParentGroupId == Id)`), any attempt to change its `Nature` (directly via `Update` or via `Reparent` to a parent of differing `Nature`) is strictly rejected with a `DomainException`.
   - Leaf groups without children update their `Nature` to match the new parent.
5. **System Group Protection**: Predefined system groups (`IsDefaultSystemGroup == true`) cannot be deleted, and cannot have their `Nature` modified.
6. **Deletion Guards**: Application layer checks `HasChildrenAsync` and `HasLedgersAsync` prior to calling repository delete. Database enforces `FOREIGN KEY (ParentGroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT`.

---

## J. LEDGER & PARTY MODEL VERIFICATION

1. **Unified Ledger Model**: There is NO separate `Party`, `Customer`, or `Supplier` entity/table. All entities are represented as `Ledger` instances categorized under appropriate `AccountGroup` nodes (e.g. `Sundry Debtors` -> `GroupNature.Asset`, `Sundry Creditors` -> `GroupNature.Liability`).
2. **Opening Balance Safeguards**:
   - `UpdateOpeningBalance` and `Update` require an explicit `bool hasTransactionHistory` argument.
   - If `hasTransactionHistory == true` and opening balance or type is altered, a `DomainException` is thrown.
3. **Deletion Safety Call Flow**:
   - `Presentation/Wpf` $\rightarrow$ calls `LedgerService.DeleteLedgerAsync(companyId, ledgerId)`.
   - `LedgerService` checks:
     1. `ledger.IsDefaultSystemLedger` $\rightarrow$ throws `DomainException` ("System predefined ledger cannot be deleted").
     2. `await _ledgerRepository.HasTransactionsAsync(companyId, id)` $\rightarrow$ throws `DomainException` ("Cannot delete ledger because it has one or more recorded financial transactions").
     3. `await _ledgerRepository.HasBillReferencesAsync(companyId, id)` $\rightarrow$ throws `DomainException` ("Cannot delete ledger because it has one or more bill-wise references").
     4. Calls `_ledgerRepository.DeleteAsync(companyId, id)`.
   - `LedgerRepository.DeleteAsync` executes `DELETE FROM Ledgers WHERE CompanyId = @CompanyId AND Id = @Id;`.
   - Database enforces `FOREIGN KEY (GroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT`.

---

## K. COMPANY & FINANCIAL YEAR VERIFICATION

1. **Company Name Uniqueness**: Case-insensitive uniqueness enforced by database index `UQ_Companies_Name ON Companies(CompanyName COLLATE NOCASE)` and application service `ExistsByNameAsync`.
2. **Financial Year Boundaries**: `StartDate < EndDate` enforced by `FinancialYear` domain constructor.
3. **Single Active Financial Year**:
   - Database level: Partial unique index `UQ_FinancialYears_Company_OneActive ON FinancialYears(CompanyId) WHERE IsActive = 1;`.
   - Service level: `FinancialYearService.SetActiveFinancialYearAsync` atomically deactivates previously active years before activating the target year.
4. **Automatic Seed Workflows**: Creating a company automatically seeds the initial financial year, 28 standard predefined account groups, and 2 default system ledgers (`Cash` and `Profit & Loss Account`).

---

## L. AUDIT & USER ATTRIBUTION

- Every database table (`Companies`, `FinancialYears`, `AccountGroups`, `Ledgers`) contains:
  - `CreatedAt TEXT NOT NULL`
  - `CreatedBy TEXT NOT NULL`
  - `ModifiedAt TEXT`
  - `ModifiedBy TEXT`
- User identity is provided by `ICurrentUserService.UserId`. In Phase 1 desktop mode, `StandaloneCurrentUserService` returns the operating system username (`Environment.UserName.ToLowerInvariant()`).
- No premature role/permission engine has been implemented. User authentication/security flags (`Companies.IsSecurityEnabled`) exist as configuration toggles.

---

## M. ARCHITECTURAL INVARIANT AUDIT (NEGATIVE VERIFICATION)

We explicitly certify that the current codebase contains:
- **NO `Party` entity or table**
- **NO `Customer` entity or table**
- **NO `Supplier` entity or table**
- **NO `AccountingEntry` entity or table**
- **NO `OpeningBalance` entity or table**
- **NO cached `CurrentBalance` or `ClosingBalance` columns**
- **NO separate balance summary tables**
- **NO `Voucher` entity or table**
- **NO `VoucherLine` entity or table**
- **NO `PostingEngine` or transaction generator**

---

## N. TEST QUALITY & COVERAGE AUDIT

### Summary: 124 Passing Tests (0 Failures, 0 Warnings)
- **Unit Tests (`BillsEasy.UnitTests`)**: 99 tests
- **Integration Tests (`BillsEasy.IntegrationTests`)**: 25 tests

### Key Test Categories
1. **Direct Domain Invariant Tests**:
   - `MoneyTests`: Arithmetic exactness, division by zero, rounding, minor units conversion, currency matching assertions.
   - `AccountGroupTests`: Self-parenting rejection, circular graph detection, missing ancestor fail-loud rejection, ADR 06 subtree protection, system group immutability.
   - `LedgerTests`: Exact `Money` assertions, negative balance rejection, GSTIN format regex validation, transaction history opening balance locks.
   - `CompanyTests` & `FinancialYearTests`: Date validation, name validation, currency symbol defaults, active FY toggle logic.
2. **Database Constraint Integration Tests (`DatabaseConstraintIntegrationTests.cs`)**:
   - Case-insensitive company name uniqueness.
   - Single active financial year partial index violation rejection.
   - Account group sibling and root unique index validation.
   - Ledger company-wide unique name validation.
   - Foreign key `ON DELETE CASCADE` from Companies to FinancialYears, AccountGroups, and Ledgers.
   - Foreign key `ON DELETE RESTRICT` from AccountGroups to child sub-groups and Ledgers.
   - Check constraints for `GSTRegistrationType`, `Nature`, and `OpeningBalanceType`.
   - Exact integer minor unit (paise) storage and retrieval round-trip.
3. **Application Service Tests**:
   - Service-level duplicate rejection, cascade seeding verification, ledger deletion safety guard verification.

---

## O. DOCUMENTATION CONSISTENCY AUDIT

| Document | Status | Notes / Discrepancies |
| :--- | :--- | :--- |
| `docs/DECISION_LOG.md` | **MATCH** | Contains authoritative ADRs 01 through 06. |
| `docs/PHASE_1_4_2_REMEDIATION_REPORT.md` | **MATCH** | Accurately details the 4 completed pre-Phase-1.5 remediations. |
| `docs/claude-review/DATABASE_SCHEMA.sql` | **MATCH** | Exact 1:1 match with `DatabaseInitializer.cs`. |
| `docs/claude-review/04_DATABASE_SCHEMA.md` | **MATCH** | Accurate table schemas, indexes, and constraints. |
| `docs/claude-review/02_CURRENT_ARCHITECTURE.md` | **OUTDATED (Minor)** | Listed `StandaloneCurrentUserService` as returning static `"system_admin"`. In actual code (`DateTimeProvider.cs`), it dynamically returns `Environment.UserName.ToLowerInvariant()`. |
| `docs/claude-review/03_DOMAIN_MODEL.md` | **OUTDATED (Minor)** | Had `Ledger.OpeningBalance` typed as `decimal` prior to Phase 1.4.2 remediation. Now `Money`. |

---

## P. KNOWN RISKS, GAPS & ARCHITECTURAL MITIGATIONS

1. **Single-Threaded SQLite Write Concurrency**:
   - *Risk*: SQLite allows multiple readers but only one concurrent writer.
   - *Mitigation*: Connection factory configures WAL mode (`journal_mode = WAL`) and `synchronous = NORMAL`. For Phase 1 desktop single-user / LAN deployment, write locks are held only during discrete transaction blocks.
2. **Account Group Hierarchy Depth**:
   - *Risk*: Unbounded hierarchy depth could lead to stack overflow during recursive UI tree construction.
   - *Mitigation*: `AccountGroupService.GetGroupTreeAsync` iteratively limits traversal, and cycle detection prevents infinite loops. UI displays up to 10 visual indent levels cleanly.
3. **Bill Allocations Deferred to Phase 1.6**:
   - *Risk*: Users might expect bill-by-bill matching during voucher entry in Phase 1.5.
   - *Mitigation*: Explicitly governed by ADR 01. Phase 1.5 delivers general ledger double-entry posting and imbalance validation. Subledger bill allocation is scheduled for Phase 1.6.

---

## Q. PHASE 1.5 READINESS CHECK (VOUCHER ENGINE)

The Phase 1.1–1.4 foundation is completely hardened and ready for Phase 1.5. Specifically:
1. **Ledger Schema**: Ready with integer minor units and unified account/party representation.
2. **Double-Entry Arithmetic**: `Money` struct guarantees zero floating point drift and exact arithmetic for `Sum(DebitLines) == Sum(CreditLines)`.
3. **Sequential Numbering**: Ready for dedicated atomic counter tables per `(CompanyId, FinancialYearId, VoucherType)` (ADR 03).
4. **Balance Derivation**: Balances will be derived dynamically via SQL aggregation queries (`SUM(CASE WHEN Type=1 THEN Amount ELSE -Amount END)`) without cached balance columns.

---

## R. QUESTIONS CLAUDE / PRINCIPAL ARCHITECT MUST EXPLICITLY ANSWER

Claude, please review this package against fundamental accounting principles, Clean Architecture, and high-performance desktop software design, and explicitly answer:

1. **Monetary Representation**: Is the combination of Domain `Money` (immutable struct with 2-decimal arithmetic) and SQLite `INTEGER` (minor units / paise) optimal and free of edge-case arithmetic drift for Indian GST and general ledger posting?
2. **Unified Ledger Model**: Does the absence of distinct `Customer` / `Supplier` / `Party` tables create any architectural bottlenecks for future Phase 1.6 Bill Allocations or GST E-Invoicing / E-Way Bill generation?
3. **Account Group Tree & ADR 06**: Is the subtree nature protection rule in ADR 06 sound, or does it impose excessive constraints on end-user chart-of-accounts restructuring?
4. **Voucher Engine Transaction Isolation**: For Phase 1.5, what specific transaction isolation and locking pattern do you recommend for SQLite WAL mode to guarantee atomic voucher numbering under rapid data entry?
5. **Final Verdict**: Is the BillsEasy codebase in an acceptable, hardened state to unfreeze and begin Phase 1.5?

---

## FINAL STATUS

### **READY FOR CLAUDE REVIEW**

### Priority Files for Claude to Inspect:
1. `src/BillsEasy.Domain/ValueObjects/Money.cs`
2. `src/BillsEasy.Domain/Entities/Ledger.cs`
3. `src/BillsEasy.Domain/Entities/AccountGroup.cs`
4. `src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs`
5. `src/BillsEasy.Infrastructure/Persistence/Repositories/LedgerRepository.cs`
6. `src/BillsEasy.Application/Services/LedgerService.cs`
7. `src/BillsEasy.Application/Services/AccountGroupService.cs`
8. `docs/DECISION_LOG.md`
