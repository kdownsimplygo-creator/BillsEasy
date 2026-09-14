# BILLSEASY — PRE-PHASE-1.5 CLAUDE FORENSIC REVIEW SOURCE PACKAGE
**Target Reviewer**: Claude / Principal Architect
**Review Scope**: Complete Implementation through Phase 1.4.2 (Scaffolding, Company/FY, Account Groups, Ledgers, UI Design System, Pre-Flight Hardening)
**Repository Root**: `W:\BillsEasy`
**Date**: 2026-09-13
**Build Status**: 0 Warnings, 0 Errors
**Test Status**: 124/124 Tests Passing (99 Unit Tests, 25 Integration Tests)
**Phase 1.5 Status**: FROZEN — Zero Voucher, VoucherLine, or PostingEngine code implemented.

---

## EXECUTIVE SUMMARY & NAVIGATION INDEX
This review package provides the complete, authoritative forensic evidence of the BillsEasy codebase as of the completion of Phase 1.4.2.
To avoid file truncation while providing complete source code, detailed unedited source files are packaged in dedicated companions:
- [`CLAUDE_REVIEW_DOMAIN.md`](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_DOMAIN.md) — Complete Domain Layer Source (13 files)
- [`CLAUDE_REVIEW_APPLICATION.md`](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_APPLICATION.md) — Complete Application Layer Source (18 files)
- [`CLAUDE_REVIEW_INFRASTRUCTURE.md`](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_INFRASTRUCTURE.md) — Complete Infrastructure Layer Source (9 files)
- [`CLAUDE_REVIEW_SCHEMA.md`](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_SCHEMA.md) — Authoritative DDL & Schema Comparisons
- [`CLAUDE_REVIEW_TESTS.md`](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_TESTS.md) — Complete Test Suite Source (18 test files)

---

## SECTION A — CURRENT PROJECT TREE
```
.gitignore
BillsEasy.sln
run.bat
docs\ACCOUNTING_ENGINE_CONSTRAINTS.md
docs\ARCHITECTURE.md
docs\AUDIT_TRAIL.md
docs\CLAUDE_REVIEW_APPLICATION.md
docs\CLAUDE_REVIEW_DOMAIN.md
docs\CLAUDE_REVIEW_INFRASTRUCTURE.md
docs\CLAUDE_REVIEW_SCHEMA.md
docs\CLAUDE_REVIEW_TESTS.md
docs\DATABASE_DESIGN.md
docs\DECISION_LOG.md
docs\DEVELOPMENT_ENVIRONMENT.md
docs\DOMAIN_MODEL.md
docs\MASTER_PROJECT_CHARTER.md
docs\MODULE_BOUNDARIES.md
docs\PHASE_1_1_IMPLEMENTATION_REPORT.md
docs\PHASE_1_2_ARCHITECTURE_GATE.md
docs\PHASE_1_2_IMPLEMENTATION_REPORT.md
docs\PHASE_1_3_IMPLEMENTATION_REPORT.md
docs\PHASE_1_4_1_STEP_2_UI_DESIGN_SYSTEM_REPORT.md
docs\PHASE_1_4_1_UI_ASSESSMENT.md
docs\PHASE_1_4_2_FINAL_ARCHITECTURE_REVIEW_PACKAGE.md
docs\PHASE_1_4_2_REMEDIATION_REPORT.md
docs\PHASE_1_4_IMPLEMENTATION_REPORT.md
docs\PRE_PHASE_1_5_CLAUDE_SOURCE_PACKAGE.md
docs\REFERENCE_SOFTWARE.md
docs\REFERENCE_SOURCES.md
docs\SECURITY_MODEL.md
docs\SOURCE_PROVENANCE.md
docs\TECHNOLOGY_NOTES.md
docs\UI_ARCHITECTURE.md
docs\claude-review\01_EXECUTIVE_SUMMARY.md
docs\claude-review\02_CURRENT_ARCHITECTURE.md
docs\claude-review\03_DOMAIN_MODEL.md
docs\claude-review\04_DATABASE_SCHEMA.md
docs\claude-review\05_COMPANY_FINANCIAL_YEAR.md
docs\claude-review\06_ACCOUNT_GROUPS.md
docs\claude-review\07_LEDGER_MODEL.md
docs\claude-review\08_AUDIT_USER_MODEL.md
docs\claude-review\09_SQLITE_DESKTOP_ARCHITECTURE.md
docs\claude-review\10_WPF_MVVM_ARCHITECTURE.md
docs\claude-review\11_TESTING_STATUS.md
docs\claude-review\12_VOUCHER_ENGINE_READINESS.md
docs\claude-review\13_RISKS_AND_GAPS.md
docs\claude-review\14_ARCHITECTURAL_DECISIONS.md
docs\claude-review\15_REVIEW_QUESTIONS_FOR_CLAUDE.md
docs\claude-review\ALL_REVIEW_DOCS_COMBINED.md
docs\claude-review\BillsEasy_Claude_Review_Package.zip
docs\claude-review\CLAUDE_REVIEW_PROMPT.md
docs\claude-review\CURRENT_PROJECT_TREE.txt
docs\claude-review\DATABASE_SCHEMA.sql
src\BillsEasy.Application\BillsEasy.Application.csproj
src\BillsEasy.Application\Common\IndianStates.cs
src\BillsEasy.Application\Common\Interfaces\IAccountGroupRepository.cs
src\BillsEasy.Application\Common\Interfaces\IAccountGroupService.cs
src\BillsEasy.Application\Common\Interfaces\ICompanyContextService.cs
src\BillsEasy.Application\Common\Interfaces\ICompanyRepositories.cs
src\BillsEasy.Application\Common\Interfaces\ICurrentUserService.cs
src\BillsEasy.Application\Common\Interfaces\IDateTimeProvider.cs
src\BillsEasy.Application\Common\Interfaces\ILedgerRepository.cs
src\BillsEasy.Application\Common\Interfaces\ILedgerService.cs
src\BillsEasy.Application\Common\Interfaces\ISqliteDbConnectionFactory.cs
src\BillsEasy.Application\DTOs\AccountGroupDtos.cs
src\BillsEasy.Application\DTOs\CompanyDtos.cs
src\BillsEasy.Application\DTOs\LedgerDtos.cs
src\BillsEasy.Application\Services\AccountGroupService.cs
src\BillsEasy.Application\Services\CompanyService.cs
src\BillsEasy.Application\Services\FinancialYearService.cs
src\BillsEasy.Application\Services\LedgerService.cs
src\BillsEasy.Domain\BillsEasy.Domain.csproj
src\BillsEasy.Domain\Common\AuditableEntity.cs
src\BillsEasy.Domain\Common\DomainException.cs
src\BillsEasy.Domain\Common\Entity.cs
src\BillsEasy.Domain\Common\IAuditableEntity.cs
src\BillsEasy.Domain\Entities\AccountGroup.cs
src\BillsEasy.Domain\Entities\Company.cs
src\BillsEasy.Domain\Entities\FinancialYear.cs
src\BillsEasy.Domain\Entities\Ledger.cs
src\BillsEasy.Domain\Enums\DebitCreditType.cs
src\BillsEasy.Domain\Enums\GroupNature.cs
src\BillsEasy.Domain\Enums\GSTRegistrationType.cs
src\BillsEasy.Domain\ValueObjects\Money.cs
src\BillsEasy.Infrastructure\BillsEasy.Infrastructure.csproj
src\BillsEasy.Infrastructure\Persistence\DatabaseInitializer.cs
src\BillsEasy.Infrastructure\Persistence\SqliteConnectionFactory.cs
src\BillsEasy.Infrastructure\Persistence\Repositories\AccountGroupRepository.cs
src\BillsEasy.Infrastructure\Persistence\Repositories\CompanyRepository.cs
src\BillsEasy.Infrastructure\Persistence\Repositories\FinancialYearRepository.cs
src\BillsEasy.Infrastructure\Persistence\Repositories\LedgerRepository.cs
src\BillsEasy.Infrastructure\Services\CompanyContextService.cs
src\BillsEasy.Infrastructure\Services\DateTimeProvider.cs
src\BillsEasy.Presentation.Wpf\App.xaml
src\BillsEasy.Presentation.Wpf\App.xaml.cs
src\BillsEasy.Presentation.Wpf\AssemblyInfo.cs
src\BillsEasy.Presentation.Wpf\BillsEasy.Presentation.Wpf.csproj
src\BillsEasy.Presentation.Wpf\MainWindow.xaml
src\BillsEasy.Presentation.Wpf\MainWindow.xaml.cs
src\BillsEasy.Presentation.Wpf\Behaviors\KeyboardNavigationBehavior.cs
src\BillsEasy.Presentation.Wpf\Behaviors\SelectAllOnFocusBehavior.cs
src\BillsEasy.Presentation.Wpf\Controls\AccountingDialogFooter.xaml
src\BillsEasy.Presentation.Wpf\Controls\AccountingDialogFooter.xaml.cs
src\BillsEasy.Presentation.Wpf\Controls\AccountingDialogHeader.xaml
src\BillsEasy.Presentation.Wpf\Controls\AccountingDialogHeader.xaml.cs
src\BillsEasy.Presentation.Wpf\Converters\AccountingFormatConverters.cs
src\BillsEasy.Presentation.Wpf\Themes\ButtonStyles.xaml
src\BillsEasy.Presentation.Wpf\Themes\Colors.xaml
src\BillsEasy.Presentation.Wpf\Themes\DataGridStyles.xaml
src\BillsEasy.Presentation.Wpf\Themes\DialogStyles.xaml
src\BillsEasy.Presentation.Wpf\Themes\InputStyles.xaml
src\BillsEasy.Presentation.Wpf\Themes\MenuStyles.xaml
src\BillsEasy.Presentation.Wpf\Themes\StatusBarStyles.xaml
src\BillsEasy.Presentation.Wpf\Themes\TabControlStyles.xaml
src\BillsEasy.Presentation.Wpf\Themes\Typography.xaml
src\BillsEasy.Presentation.Wpf\ViewModels\AccountGroupEditViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\AccountGroupListViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\CompanyEditViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\CompanyListViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\FinancialYearManagementViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\LedgerEditViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\LedgerListViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\MainWindowViewModel.cs
src\BillsEasy.Presentation.Wpf\ViewModels\ViewModelBase.cs
src\BillsEasy.Presentation.Wpf\Views\Company\CompanyEditDialog.xaml
src\BillsEasy.Presentation.Wpf\Views\Company\CompanyEditDialog.xaml.cs
src\BillsEasy.Presentation.Wpf\Views\Company\CompanyListDialog.xaml
src\BillsEasy.Presentation.Wpf\Views\Company\CompanyListDialog.xaml.cs
src\BillsEasy.Presentation.Wpf\Views\Company\FinancialYearManagementDialog.xaml
src\BillsEasy.Presentation.Wpf\Views\Company\FinancialYearManagementDialog.xaml.cs
src\BillsEasy.Presentation.Wpf\Views\Masters\AccountGroupEditDialog.xaml
src\BillsEasy.Presentation.Wpf\Views\Masters\AccountGroupEditDialog.xaml.cs
src\BillsEasy.Presentation.Wpf\Views\Masters\AccountGroupManagementDialog.xaml
src\BillsEasy.Presentation.Wpf\Views\Masters\AccountGroupManagementDialog.xaml.cs
src\BillsEasy.Presentation.Wpf\Views\Masters\LedgerEditDialog.xaml
src\BillsEasy.Presentation.Wpf\Views\Masters\LedgerEditDialog.xaml.cs
src\BillsEasy.Presentation.Wpf\Views\Masters\LedgerManagementDialog.xaml
src\BillsEasy.Presentation.Wpf\Views\Masters\LedgerManagementDialog.xaml.cs
tests\BillsEasy.IntegrationTests\BillsEasy.IntegrationTests.csproj
tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\AccountGroupRepositoryTests.cs
tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\CompanyRepositoryTests.cs
tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\DatabaseConstraintIntegrationTests.cs
tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\FinancialYearRepositoryTests.cs
tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\LedgerRepositoryTests.cs
tests\BillsEasy.IntegrationTests\Infrastructure\Persistence\SqliteConnectionFactoryTests.cs
tests\BillsEasy.UnitTests\BillsEasy.UnitTests.csproj
tests\BillsEasy.UnitTests\Application\Services\AccountGroupServiceTests.cs
tests\BillsEasy.UnitTests\Application\Services\CompanyServiceTests.cs
tests\BillsEasy.UnitTests\Application\Services\FinancialYearServiceTests.cs
tests\BillsEasy.UnitTests\Application\Services\LedgerServiceTests.cs
tests\BillsEasy.UnitTests\Domain\Common\AuditableEntityTests.cs
tests\BillsEasy.UnitTests\Domain\Entities\AccountGroupTests.cs
tests\BillsEasy.UnitTests\Domain\Entities\CompanyTests.cs
tests\BillsEasy.UnitTests\Domain\Entities\FinancialYearTests.cs
tests\BillsEasy.UnitTests\Domain\Entities\LedgerTests.cs
tests\BillsEasy.UnitTests\Domain\ValueObjects\MoneyTests.cs
``n
---

## SECTION B — DOMAIN SOURCE
The Domain Layer (`src/BillsEasy.Domain`) is a pure .NET 8 class library with **zero external package dependencies**.

### Domain Files Inventory
- `Common/Entity.cs` — Abstract base entity with GUID identity and equality operators.
- `Common/IAuditableEntity.cs` — Interface defining audit contract (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`).
- `Common/AuditableEntity.cs` — Base class implementing `IAuditableEntity`.
- `Common/DomainException.cs` — Custom domain exception thrown on invariant violations.
- `ValueObjects/Money.cs` — Immutable value object representing monetary values (`decimal Amount`, `string Currency`), 2-decimal Bankers/Midpoint rounding, `ToMinorUnits()` (paise `long`), `FromMinorUnits(long)`, exact arithmetic operators (`+`, `-`, `*`, `/`), comparisons, and implicit conversion from `decimal`.
- `Enums/GroupNature.cs` — `Asset = 1`, `Liability = 2`, `Capital = 3`, `Income = 4`, `Expense = 5`.
- `Enums/DebitCreditType.cs` — `Debit = 1`, `Credit = 2`.
- `Enums/GSTRegistrationType.cs` — `Regular = 1`, `Composition = 2`, `Unregistered = 3`, `Consumer = 4`, `Overseas = 5`, `SpecialEconomicZone = 6`.
- `Entities/Company.cs` — Root aggregate holding company profile, statutory GSTIN, book beginning date, currency symbol, and associated `FinancialYear` collection.
- `Entities/FinancialYear.cs` — Accounting year boundary entity with `StartDate < EndDate` validation and active/locked state.
- `Entities/AccountGroup.cs` — Hierarchical classification entity. Enforces tree validity, company co-ownership, cycle detection, fail-loud ancestor traversal, and ADR 06 Nature consistency.
- `Entities/Ledger.cs` — Single unified aggregate representing all General Ledger accounts, Customer parties, Supplier parties, Bank accounts, and Tax ledgers. Features `Money OpeningBalance`, `Money? CreditLimit`, GSTIN validation regex, and transaction-history opening balance locks.

*(Full unedited C# source for all 13 domain files is provided in [CLAUDE_REVIEW_DOMAIN.md](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_DOMAIN.md))*

---

## SECTION C — APPLICATION SOURCE
The Application Layer (`src/BillsEasy.Application`) contains application use-cases, orchestration services, repository interfaces, and DTO contracts.

### Application Files Inventory
- `Common/IndianStates.cs` — Statutory list of 36 Indian States and Union Territories with 2-digit GST state codes.
- `Common/Interfaces/ICompanyRepositories.cs` — `ICompanyRepository`, `IFinancialYearRepository` contracts.
- `Common/Interfaces/IAccountGroupRepository.cs` — `IAccountGroupRepository` contract.
- `Common/Interfaces/ILedgerRepository.cs` — `ILedgerRepository` contract.
- `Common/Interfaces/IAccountGroupService.cs` — `IAccountGroupService` contract.
- `Common/Interfaces/ILedgerService.cs` — `ILedgerService` contract.
- `Common/Interfaces/ICompanyContextService.cs` — In-memory company selection state contract.
- `Common/Interfaces/ICurrentUserService.cs` — Current user context interface (`UserId`, `UserName`, `WorkstationName`).
- `Common/Interfaces/IDateTimeProvider.cs` — Deterministic clock abstraction (`UtcNow`, `Now`).
- `Common/Interfaces/ISqliteDbConnectionFactory.cs` — Connection factory abstraction.
- `DTOs/CompanyDtos.cs` — `CompanyDto`, `CreateCompanyDto`, `UpdateCompanyDto` immutable records.
- `DTOs/AccountGroupDtos.cs` — `AccountGroupDto`, `AccountGroupNodeDto`, `AccountGroupLookupDto`, `CreateAccountGroupDto`, `UpdateAccountGroupDto`.
- `DTOs/LedgerDtos.cs` — `LedgerDto`, `LedgerLookupDto`, `CreateLedgerDto`, `UpdateLedgerDto`.
- `Services/CompanyService.cs` — Orchestrates company creation, name uniqueness, initial FY generation, and default taxonomy seeding.
- `Services/FinancialYearService.cs` — Manages FY date non-overlap validation and active FY switching.
- `Services/AccountGroupService.cs` — Manages group tree structure, sibling/root uniqueness, deletion checks, and default group seeding (28 standard groups).
- `Services/LedgerService.cs` — Manages ledger CRUD, case-insensitive uniqueness, deletion guards (system ledger, transaction history, bill references), and default ledger seeding (`Cash`, `Profit & Loss Account`).

*(Full unedited C# source for all 18 application files is provided in [CLAUDE_REVIEW_APPLICATION.md](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_APPLICATION.md))*

---

## SECTION D — INFRASTRUCTURE SOURCE
The Infrastructure Layer (`src/BillsEasy.Infrastructure`) implements raw parameterized ADO.NET data access over SQLite with strict WAL configuration.

### Infrastructure Files Inventory
- `Persistence/SqliteConnectionFactory.cs` — Enforces `PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;` on all opened connections.
- `Persistence/DatabaseInitializer.cs` — Authoritative runtime database schema generator.
- `Persistence/Repositories/CompanyRepository.cs` — ADO.NET repository for `Company` and associated `FinancialYear` aggregate hydration.
- `Persistence/Repositories/FinancialYearRepository.cs` — ADO.NET repository for `FinancialYear` queries and single-active-year updates.
- `Persistence/Repositories/AccountGroupRepository.cs` — ADO.NET repository for `AccountGroup` hierarchy, sibling uniqueness, and deletion reference checks (`HasChildrenAsync`, `HasLedgersAsync`).
- `Persistence/Repositories/LedgerRepository.cs` — ADO.NET repository for `Ledger`. Parameter binding converts `ledger.OpeningBalance.ToMinorUnits()` (paise `long`), reader hydration calls `Money.FromMinorUnits(reader.GetInt64(6))`. Contains `HasTransactionsAsync` and `HasBillReferencesAsync` defensive checks.
- `Services/CompanyContextService.cs` — Active company state management for UI session.
- `Services/DateTimeProvider.cs` — System clock provider + `StandaloneCurrentUserService` implementing `ICurrentUserService` returning `Environment.UserName.ToLowerInvariant()`.

*(Full unedited C# source for all 9 infrastructure files is provided in [CLAUDE_REVIEW_INFRASTRUCTURE.md](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_INFRASTRUCTURE.md))*

---
## SECTION E — COMPLETE DATABASE SCHEMA
### 1. Schema Authority Analysis
- **SOURCE FACT**: Runtime Authoritative Schema is defined in `src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs`.
- **SOURCE FACT**: Documentation Schema is defined in `docs/claude-review/DATABASE_SCHEMA.sql`.
- **SOURCE FACT**: The two schemas are **IDENTICAL** in table definitions, column names, column order, primary keys, foreign keys, on delete behaviors, check constraints, and unique/partial/ordinary indexes.

### 2. Full Current DDL
```sql
PRAGMA foreign_keys = ON;

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
CREATE UNIQUE INDEX IF NOT EXISTS UQ_Companies_Name ON Companies(CompanyName COLLATE NOCASE);

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
CREATE INDEX IF NOT EXISTS IX_FinancialYears_CompanyId ON FinancialYears(CompanyId);
CREATE UNIQUE INDEX IF NOT EXISTS UQ_FinancialYears_Company_OneActive ON FinancialYears(CompanyId) WHERE IsActive = 1;

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
CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Parent_Name ON AccountGroups(CompanyId, ParentGroupId, Name COLLATE NOCASE) WHERE ParentGroupId IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Root_Name ON AccountGroups(CompanyId, Name COLLATE NOCASE) WHERE ParentGroupId IS NULL;
CREATE INDEX IF NOT EXISTS IX_AccountGroups_ParentGroupId ON AccountGroups(ParentGroupId);
CREATE INDEX IF NOT EXISTS IX_AccountGroups_CompanyId ON AccountGroups(CompanyId);

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
CREATE UNIQUE INDEX IF NOT EXISTS UQ_Ledgers_Company_Name ON Ledgers(CompanyId, Name COLLATE NOCASE);
CREATE INDEX IF NOT EXISTS IX_Ledgers_GroupId ON Ledgers(GroupId);
CREATE INDEX IF NOT EXISTS IX_Ledgers_CompanyId ON Ledgers(CompanyId);
```

---
## SECTION F — PRESENTATION ARCHITECTURE (WPF & MVVM)
1. **Framework**: WPF (.NET 8 LTS) with `CommunityToolkit.Mvvm`.
2. **Centralized Theming**: Fully centralized XAML resources in `Themes/` (`Colors.xaml`, `Typography.xaml`, `ButtonStyles.xaml`, `InputStyles.xaml`, `DataGridStyles.xaml`, `TabControlStyles.xaml`, `DialogStyles.xaml`, `MenuStyles.xaml`, `StatusBarStyles.xaml`).
3. **Keyboard Architecture**: `KeyboardNavigationBehavior` (Enter moves forward, Shift+Enter moves backward) and `SelectAllOnFocusBehavior`.
4. **Dialog Chrome**: Reusable `AccountingDialogHeader` and `AccountingDialogFooter` UserControls.
5. **Formatters**: `IndianCurrencyConverter` (formats `Money`/`decimal` into `₹ 1,00,000.00`) and `DebitCreditConverter` (`Dr`/`Cr`).

---

## SECTION G — COMPLETE TEST SUITE BREAKDOWN
Total Tests: **124 Passed** (99 Unit Tests, 25 Integration Tests).

### Unit Tests (`tests/BillsEasy.UnitTests`) — 99 Tests
- `Domain/ValueObjects/MoneyTests.cs` (13 tests) — Arithmetic, rounding, minor units, comparison, immutable value equality.
- `Domain/Common/AuditableEntityTests.cs` (2 tests) — Audit assignment.
- `Domain/Entities/CompanyTests.cs` (9 tests) — Invariants, dates, name validation, currency defaults.
- `Domain/Entities/FinancialYearTests.cs` (12 tests) — Dates, boundaries, active flag.
- `Domain/Entities/AccountGroupTests.cs` (23 tests) — Sibling uniqueness, cycle detection, fail-loud ancestor checks, ADR 06 subtree protection, system group immutability.
- `Domain/Entities/LedgerTests.cs` (17 tests) — Exact `Money` fields, GSTIN regex, transaction-history opening balance locks.
- `Application/Services/CompanyServiceTests.cs` (5 tests) — Seeding, validation, duplicate rejection.
- `Application/Services/FinancialYearServiceTests.cs` (7 tests) — Overlap rejection, active switching.
- `Application/Services/AccountGroupServiceTests.cs` (5 tests) — Deletion guards, tree lookup.
- `Application/Services/LedgerServiceTests.cs` (6 tests) — Deletion guards (system ledger, transactions, bills), clean delete.

### Integration Tests (`tests/BillsEasy.IntegrationTests`) — 25 Tests
- `DatabaseConstraintIntegrationTests.cs` (8 tests) — Real SQLite on-disk testing of CHECK constraints (Nature, OpeningBalanceType, GSTRegistrationType), NOCASE uniqueness, partial active FY index, minor units (paise) exact round-trip, foreign key CASCADE & RESTRICT.
- `CompanyRepositoryTests.cs` (4 tests) — Company & FY persistence.
- `FinancialYearRepositoryTests.cs` (3 tests) — Single active FY query & updates.
- `AccountGroupRepositoryTests.cs` (4 tests) — Hierarchy persistence & deletion checks.
- `LedgerRepositoryTests.cs` (5 tests) — Exact `Money` persistence, search, duplicate detection.
- `SqliteConnectionFactoryTests.cs` (1 test) — WAL pragma configuration.

*(Full unedited C# source for all 18 test files is provided in [CLAUDE_REVIEW_TESTS.md](file:///W:/BillsEasy/docs/CLAUDE_REVIEW_TESTS.md))*

---

## SECTION H — AUDIT / USER ATTRIBUTION EVIDENCE
1. **Audit Columns**: Every table (`Companies`, `FinancialYears`, `AccountGroups`, `Ledgers`) contains `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`.
2. **Current Implementation**: `StandaloneCurrentUserService` implements `ICurrentUserService` and returns `Environment.UserName.ToLowerInvariant()`.
3. **Source Fact**: There is currently **NO `User` entity or `Users` database table**.
4. **Architectural Intent**: In Phase 1 desktop mode, the Windows login name provides operator attribution. In future multi-user / network phases, `ICurrentUserService` will be swapped for an authenticated session provider without altering domain entities or database audit columns.

---
## SECTION I — DELETE GUARD EVIDENCE
### 1. Ledger Deletion Guard Call Path
```
UI / ViewModel (LedgerListViewModel)
    └── LedgerService.DeleteLedgerAsync(companyId, ledgerId)
            ├── 1. GetByIdAsync(companyId, ledgerId)
            ├── 2. Check ledger.IsDefaultSystemLedger -> Throws DomainException
            ├── 3. Check HasTransactionsAsync(companyId, ledgerId) -> Throws DomainException
            ├── 4. Check HasBillReferencesAsync(companyId, ledgerId) -> Throws DomainException
            └── 5. LedgerRepository.DeleteAsync(companyId, ledgerId)
                    └── SQL: DELETE FROM Ledgers WHERE CompanyId = @CompanyId AND Id = @Id;
```

### 2. Forensic Answers on Deletion
- **Where is deletion blocked?** Blocked in `LedgerService.DeleteLedgerAsync` before any database deletion query is executed.
- **Is transaction history checked?** YES. `_ledgerRepository.HasTransactionsAsync(companyId, id)` defensively checks `VoucherLines`.
- **Are bill references checked?** YES. `_ledgerRepository.HasBillReferencesAsync(companyId, id)` defensively checks `BillReferences` / `BillAllocations`.
- **Are system ledgers protected?** YES. `ledger.IsDefaultSystemLedger` is validated.
- **Can someone bypass the service and delete directly via repository?** If code directly calls `ILedgerRepository.DeleteAsync`, the database level enforces `FOREIGN KEY (GroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT`. In Phase 1.5, `FOREIGN KEY (LedgerId) REFERENCES Ledgers(Id) ON DELETE RESTRICT` will be added to `VoucherLines`, ensuring database-level integrity even if the service layer were bypassed.

---

## SECTION J — CASCADE & DELETE RELATIONSHIP EVIDENCE
### 1. Foreign Key Relationship Topology
- `FinancialYears.CompanyId` -> `Companies.Id` `ON DELETE CASCADE`
- `AccountGroups.CompanyId` -> `Companies.Id` `ON DELETE CASCADE`
- `AccountGroups.ParentGroupId` -> `AccountGroups.Id` `ON DELETE RESTRICT`
- `Ledgers.CompanyId` -> `Companies.Id` `ON DELETE CASCADE`
- `Ledgers.GroupId` -> `AccountGroups.Id` `ON DELETE RESTRICT`

### 2. Forensic Answers on Cascade Behaviors
- **What gets deleted when a Company is deleted?** Due to `ON DELETE CASCADE`, deleting a Company row cascades and deletes all associated `FinancialYears`, `AccountGroups`, and `Ledgers` for that company.
- **What is RESTRICT?** `AccountGroups.ParentGroupId` and `Ledgers.GroupId` are `ON DELETE RESTRICT`. An `AccountGroup` cannot be deleted if child sub-groups exist or if ledgers point to it.
- **Can orphan records occur?** NO. `PRAGMA foreign_keys = ON` is strictly active on all connections. Foreign keys ensure all children reference valid parent entities.
- **ARCHITECTURAL INTENT**: Company deletion is a full tenant-purge operation. Deleting individual groups or ledgers within an active company is guarded at both the Application Service layer and the database `RESTRICT` level.

---

## SECTION K — NEGATIVE ARCHITECTURAL AUDIT
Source-level forensic search across `src/` for prohibited premature entities:

| Term | Status in `src/` | Forensic Evidence |
| :--- | :--- | :--- |
| `Party` | **NOT FOUND** as entity/table | Only appears in UI label ('Party Contact Information') and XML doc comment explaining unified Ledger model. |
| `Customer` | **NOT FOUND** as entity/table | Only appears in UI dialog subtitle and XML doc comment. |
| `Supplier` | **NOT FOUND** as entity/table | Only appears in UI dialog subtitle and XML doc comment. |
| `AccountingEntry` | **NOT FOUND** | 0 occurrences in entire `src/`. |
| `OpeningBalance` (entity) | **NOT FOUND** | Only exists as `Money` property on `Ledger` and integer column on `Ledgers` table. |
| `Voucher` (entity/table) | **NOT FOUND** | Only appears in `MainWindow.xaml` disabled menu items and defensive `sqlite_master` existence checks in `LedgerRepository`. |
| `VoucherLine` (entity/table) | **NOT FOUND** | Only appears in defensive query string in `LedgerRepository.HasTransactionsAsync`. |
| `PostingEngine` | **NOT FOUND** | 0 occurrences in entire `src/`. |
| `RunningBalance` | **NOT FOUND** | 0 occurrences in entire `src/`. |
| `ClosingBalance` | **NOT FOUND** | 0 occurrences in entire `src/`. |

---
## SECTION L — ARCHITECTURE DOCUMENTATION AUDIT

| Document | Status | Forensic Finding |
| :--- | :--- | :--- |
| `docs/DECISION_LOG.md` | **MATCH** | Contains authoritative ADRs 01 to 06. |
| `docs/PHASE_1_4_2_REMEDIATION_REPORT.md` | **MATCH** | Accurately documents the 4 pre-Phase-1.5 remediations. |
| `docs/PHASE_1_4_2_FINAL_ARCHITECTURE_REVIEW_PACKAGE.md` | **MATCH** | Pre-flight review package. |
| `docs/PHASE_1_4_1_UI_ASSESSMENT.md` | **MATCH** | UI assessment report. |
| `docs/DEVELOPMENT_ENVIRONMENT.md` | **MATCH** | Dev environment documentation. |
| `docs/REFERENCE_SOFTWARE.md` | **MATCH** | Reference software notes. |
| `docs/REFERENCE_SOURCES.md` | **MATCH** | Reference sources documentation. |
| `docs/TECHNOLOGY_NOTES.md` | **MATCH** | Technology notes. |
| `docs/claude-review/DATABASE_SCHEMA.sql` | **MATCH** | Matches `DatabaseInitializer.cs` 1:1. |
| `docs/claude-review/04_DATABASE_SCHEMA.md` | **MATCH** | Matches actual database schema. |
| `docs/claude-review/02_CURRENT_ARCHITECTURE.md` | **OUTDATED (Minor)** | Listed `StandaloneCurrentUserService` as returning static `'system_admin'`; actual code returns `Environment.UserName.ToLowerInvariant()`. |
| `docs/claude-review/03_DOMAIN_MODEL.md` | **OUTDATED (Minor)** | Listed `OpeningBalance` as `decimal`; updated to `Money` in Phase 1.4.2. |
| `docs/02_CURRENT_ARCHITECTURE.md` | **NOT PRESENT: docs/02_CURRENT_ARCHITECTURE.md** | Located under `docs/claude-review/02_CURRENT_ARCHITECTURE.md`. |
| `docs/03_DOMAIN_MODEL.md` | **NOT PRESENT: docs/03_DOMAIN_MODEL.md** | Located under `docs/claude-review/03_DOMAIN_MODEL.md`. |
| `docs/04_DATABASE_SCHEMA.md` | **NOT PRESENT: docs/04_DATABASE_SCHEMA.md** | Located under `docs/claude-review/04_DATABASE_SCHEMA.md`. |
| `docs/06_ACCOUNT_GROUPS.md` | **NOT PRESENT: docs/06_ACCOUNT_GROUPS.md** | Located under `docs/claude-review/06_ACCOUNT_GROUPS.md`. |
| `DATABASE_SCHEMA.sql` | **NOT PRESENT: DATABASE_SCHEMA.sql** | Located under `docs/claude-review/DATABASE_SCHEMA.sql`. |

---

## SECTION M — BUILD & TEST EVIDENCE
```bash
$ dotnet build BillsEasy.sln -c Release
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet test BillsEasy.sln -c Release
Passed! - Failed: 0, Passed: 99, Skipped: 0, Total: 99 (BillsEasy.UnitTests.dll)
Passed! - Failed: 0, Passed: 25, Skipped: 0, Total: 25 (BillsEasy.IntegrationTests.dll)
Total: 124 Tests Passing (100% Success Rate)
```

---

## SECTION N — KNOWN QUESTIONS REQUIRING PRINCIPAL ARCHITECT DECISION
Claude / Principal Architect is requested to provide authoritative guidance on the following:

1. **Monetary Precision & GST Rounding**:
   - `Money` uses `MidpointRounding.AwayFromZero` to 2 decimal places, and stores integer minor units (`paise`) in SQLite.
   - *Question for Claude*: In Phase 1.5 GST line-item tax calculations (CGST/SGST 9% on odd paise base amounts), should line-level tax rounding occur per line item or at the voucher summary level to comply with Indian GST statutory invoicing rules?

2. **Voucher Numbering Atomicity in SQLite WAL Mode**:
   - ADR 03 mandates dedicated counter tables updated within the same transaction as the voucher creation.
   - *Question for Claude*: In SQLite desktop WAL mode, what exact transaction locking statement (`BEGIN IMMEDIATE` vs `BEGIN EXCLUSIVE`) is optimal to prevent `SQLITE_BUSY` contention during rapid sequential voucher entry?

3. **Single-Active Financial Year Invariant**:
   - Current SQLite schema uses a partial unique index `UQ_FinancialYears_Company_OneActive ON FinancialYears(CompanyId) WHERE IsActive = 1;`.
   - *Question for Claude*: When users back-post prior-period adjustments across financial year boundaries, is the active FY flag purely a UI context default, or should cross-FY postings require temporary active-FY switching?

4. **Authoritative Confirmation for Phase 1.5 Kickoff**:
   - *Question for Claude*: Is the domain, application, database, and repository architecture 100% sound to unfreeze development and begin Phase 1.5 (Voucher Engine & Double-Entry Posting Engine)?

---

## FINAL STATUS: READY FOR CLAUDE REVIEW

### Priority Files for Claude to Inspect:
1. `src/BillsEasy.Domain/ValueObjects/Money.cs`
2. `src/BillsEasy.Domain/Entities/Ledger.cs`
3. `src/BillsEasy.Domain/Entities/AccountGroup.cs`
4. `src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs`
5. `src/BillsEasy.Infrastructure/Persistence/Repositories/LedgerRepository.cs`
6. `src/BillsEasy.Application/Services/LedgerService.cs`
7. `src/BillsEasy.Application/Services/AccountGroupService.cs`
8. `docs/DECISION_LOG.md`
