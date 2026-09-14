# Phase 1.4 Implementation Report: Ledger / Account Master

## 1. Executive Summary
Phase 1.4 of **BillsEasy** has been implemented, verified, and documented according to the approved architecture. This phase introduces the single authoritative `Ledger` aggregate entity, representing both general ledger accounts and business parties (Customers and Suppliers) without introducing separate `Party`, `Customer`, or `Supplier` entities.

In accordance with the Master Project Charter and accounting engine constraints:
- **Single Authoritative Ledger Master**: No parallel Party entity or competing balance tables were introduced.
- **Initial Setup Opening Balance vs. Transaction Balances**: `Ledger` stores master setup opening balance attributes (`OpeningBalance`, `OpeningBalanceType`), but does NOT store running or closing cached balance columns.
- **Party Configuration**: Supports `BillWiseEnabled`, `CreditPeriodDays`, `CreditLimit`, GSTIN validation (15 alphanumeric characters), statutory PAN/Aadhaar, address, contact persons, and banking details.
- **System / Protected Ledgers**: Seeds standard initial accounts (`Cash` under `Cash-in-hand`, `Profit & Loss Account` under `Capital Account`) and guards them against accidental deletion.
- **Quality Gate Passed**: 99/99 automated tests passing (80 Unit Tests, 19 Integration Tests), 0 build warnings, 0 build errors.

---

## 2. Files Created and Modified

### Domain Layer (`BillsEasy.Domain`)
- [`src/BillsEasy.Domain/Enums/DebitCreditType.cs`](file:///w:/BillsEasy/src/BillsEasy.Domain/Enums/DebitCreditType.cs) — Enum for debit/credit indication (`Debit = 1`, `Credit = 2`).
- [`src/BillsEasy.Domain/Entities/Ledger.cs`](file:///w:/BillsEasy/src/BillsEasy.Domain/Entities/Ledger.cs) — Single authoritative aggregate entity for accounts and parties.

### Application Layer (`BillsEasy.Application`)
- [`src/BillsEasy.Application/DTOs/LedgerDtos.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/DTOs/LedgerDtos.cs) — DTOs for ledger creation, modification, full display, and lookup.
- [`src/BillsEasy.Application/Common/Interfaces/ILedgerRepository.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Common/Interfaces/ILedgerRepository.cs) — Repository contract for Ledger persistence.
- [`src/BillsEasy.Application/Common/Interfaces/ILedgerService.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Common/Interfaces/ILedgerService.cs) — Application service interface.
- [`src/BillsEasy.Application/Services/LedgerService.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Services/LedgerService.cs) — Use cases for ledger CRUD, search, delete protection, and system ledger seeding.
- [`src/BillsEasy.Application/Services/CompanyService.cs`](file:///w:/BillsEasy/src/BillsEasy.Application/Services/CompanyService.cs) — Integrated default ledger seeding upon company creation.

### Infrastructure Layer (`BillsEasy.Infrastructure`)
- [`src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs`](file:///w:/BillsEasy/src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs) — SQLite schema creation for `Ledgers` table with foreign keys and unique index `UQ_Ledgers_Company_Name`.
- [`src/BillsEasy.Infrastructure/Persistence/Repositories/LedgerRepository.cs`](file:///w:/BillsEasy/src/BillsEasy.Infrastructure/Persistence/Repositories/LedgerRepository.cs) — Parameterized SQLite repository implementation.

### Presentation Layer (`BillsEasy.Presentation.Wpf`)
- [`src/BillsEasy.Presentation.Wpf/ViewModels/LedgerEditViewModel.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/ViewModels/LedgerEditViewModel.cs) — Tabbed form ViewModel for basic, opening/credit, statutory/address, and contact/banking details.
- [`src/BillsEasy.Presentation.Wpf/ViewModels/LedgerListViewModel.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/ViewModels/LedgerListViewModel.cs) — Master search, group filtering, and management ViewModel.
- [`src/BillsEasy.Presentation.Wpf/ViewModels/MainWindowViewModel.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/ViewModels/MainWindowViewModel.cs) — Added `ManageLedgersCommand` (`Alt+L`).
- [`src/BillsEasy.Presentation.Wpf/Views/Masters/LedgerEditDialog.xaml`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/Views/Masters/LedgerEditDialog.xaml) & `.cs` — Modal dialog with structured tabs for ledger creation and editing.
- [`src/BillsEasy.Presentation.Wpf/Views/Masters/LedgerManagementDialog.xaml`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/Views/Masters/LedgerManagementDialog.xaml) & `.cs` — Master management dialog with live search, group filter dropdown, and delete protection.
- [`src/BillsEasy.Presentation.Wpf/MainWindow.xaml`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/MainWindow.xaml) — Enabled `Masters -> Accounts / Ledgers... (Alt+L)` menu item.
- [`src/BillsEasy.Presentation.Wpf/App.xaml.cs`](file:///w:/BillsEasy/src/BillsEasy.Presentation.Wpf/App.xaml.cs) — Registered Ledger repositories, services, and viewmodels in DI.

### Automated Test Suite
- [`tests/BillsEasy.UnitTests/Domain/Entities/LedgerTests.cs`](file:///w:/BillsEasy/tests/BillsEasy.UnitTests/Domain/Entities/LedgerTests.cs) — Domain invariant tests (validation of name, company, group, non-negative amounts, GSTIN regex).
- [`tests/BillsEasy.UnitTests/Application/Services/LedgerServiceTests.cs`](file:///w:/BillsEasy/tests/BillsEasy.UnitTests/Application/Services/LedgerServiceTests.cs) — Use case tests (cross-company validation, duplicate name rejection, transaction/bill delete guards, seeding).
- [`tests/BillsEasy.IntegrationTests/Infrastructure/Persistence/LedgerRepositoryTests.cs`](file:///w:/BillsEasy/tests/BillsEasy.IntegrationTests/Infrastructure/Persistence/LedgerRepositoryTests.cs) — SQLite integration tests (CRUD, foreign key cascade from Company, foreign key restrict from AccountGroup, company-scoped case-insensitive unique constraint, company isolation).

---

## 3. Database Schema

```sql
CREATE TABLE IF NOT EXISTS Ledgers (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    GroupId TEXT NOT NULL,
    Name TEXT NOT NULL,
    PrintName TEXT NOT NULL,
    Alias TEXT,
    OpeningBalance REAL NOT NULL DEFAULT 0.0,
    OpeningBalanceType INTEGER NOT NULL DEFAULT 1,
    BillWiseEnabled INTEGER NOT NULL DEFAULT 0,
    CreditPeriodDays INTEGER,
    CreditLimit REAL,
    AddressLine1 TEXT,
    AddressLine2 TEXT,
    City TEXT,
    StateName TEXT,
    StateCode TEXT,
    PinCode TEXT,
    Country TEXT NOT NULL DEFAULT 'India',
    GSTIN TEXT,
    GSTRegistrationType INTEGER NOT NULL DEFAULT 1,
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

## 4. Key Architectural Decisions & Invariants

1. **Party as Ledger**:
   - Customer = Ledger categorized under `Sundry Debtors`.
   - Supplier = Ledger categorized under `Sundry Creditors`.
   - No parallel `Party` table or entity exists.
2. **Initial Setup Opening Balance vs. Transaction Balances**:
   - `OpeningBalance` and `OpeningBalanceType` represent initial setup parameters.
   - Running balances (`CurrentBalance`, `ClosingBalance`) are **NOT** stored on the ledger table and will be derived from `VoucherLine` entries in Phase 1.6/1.8.
3. **Company Scoping & Isolation**:
   - Every Ledger strictly belongs to one Company.
   - A Ledger cannot reference an AccountGroup belonging to a different company.
   - Ledger names are unique per company (case-insensitive via `COLLATE NOCASE`).
4. **Delete Protection**:
   - System ledgers (`IsDefaultSystemLedger = true`) cannot be deleted.
   - Ledgers with financial transactions or bill references cannot be deleted.
   - Foreign key constraint `ON DELETE RESTRICT` protects AccountGroups from deletion if any Ledgers reference them.

---

## 5. Source Provenance Classification

- **SOURCE-DERIVED (Classes 2–3)**:
  - Party is a Ledger under Sundry Debtors / Sundry Creditors.
  - Setup opening balance (Debit/Credit).
  - Bill-by-bill maintenance toggle (`BillWiseEnabled`).
  - Credit period (days) and credit limit amount.
  - Address, State, GSTIN, PAN, Contact (Mobile, WhatsApp, Email), and Banking (A/C, IFSC).
  - `Cash` and `Profit & Loss Account` pre-existing standard ledgers.
- **DOMAIN INFERENCE**:
  - `DebitCreditType` enum definition (`Debit = 1`, `Credit = 2`).
  - Nullable credit term fields for non-party ledgers.
- **ARCHITECTURAL DECISION**:
  - SQLite foreign key restrict on `GroupId` and cascade on `CompanyId`.
  - Case-insensitive unique indexing for company-scoped ledger names.

---

## 6. Verification & Test Results

### Build Verification
```text
dotnet build BillsEasy.sln -c Release
Result: Build succeeded. 0 Warning(s), 0 Error(s).
```

### Test Suite Execution
```text
dotnet test BillsEasy.sln -c Release
Total tests: 99
Passed: 99 (100%)
Failed: 0
Skipped: 0
```
- **Unit Tests**: 80 passed (LedgerTests, LedgerServiceTests, AccountGroupTests, AccountGroupServiceTests, CompanyServiceTests, FinancialYearServiceTests, CompanyTests, FinancialYearTests, MoneyTests, AuditableEntityTests)
- **Integration Tests**: 19 passed (LedgerRepositoryTests, AccountGroupRepositoryTests, CompanyRepositoryTests, FinancialYearRepositoryTests, SqliteConnectionFactoryTests)

---

## 7. Deferred Items & Future Dependencies
1. **Voucher & VoucherLine Engine (Stage 1.6)**: Dynamic derivation of ledger running balances and statements will query transactional voucher lines.
2. **Bill-Wise Allocation Subsystem (Stage 1.7)**: Outstanding bill tracking and allocation against `BillReference` records will link to party ledgers having `BillWiseEnabled == true`.

---

## 8. Stop Condition
Phase 1.4 implementation is complete and verified. Awaiting review and explicit authorization for the next phase.