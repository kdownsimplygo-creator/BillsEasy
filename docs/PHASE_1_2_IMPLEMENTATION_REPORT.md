# BillsEasy — Stage 1.2 Implementation Report

**Stage:** Phase 1.2 — Company & Financial Year Management  
**Status:** Completed & Fully Verified  
**Date:** September 2026  

---

## 1. What Was Implemented

1. **Domain Entities & Invariants (`BillsEasy.Domain`):**
   * **`Company` Aggregate Root:**
     * Properties: `Id`, `CompanyName`, `PrintName`, `ShortName`, `Country`, `StateName`, `StateCode`, `FinancialYearBeginning`, `BooksBeginningFrom`, `CurrencySymbol`, `CurrencyFormalName`, `GSTIN`, `GSTRegistrationType`, `IsSecurityEnabled`, and standard audit metadata (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`).
     * Invariants:
       * `CompanyName`, `StateName`, and `StateCode` cannot be empty.
       * `BooksBeginningFrom` must be on or after `FinancialYearBeginning`.
       * `GSTIN` is validated against official 15-character statutory alphanumeric regex pattern when provided.
       * Auto-generates initial 1-year `FinancialYear` upon company creation.
       * Encapsulates `_financialYears` collection and prevents adding overlapping financial periods.
   * **`FinancialYear` Entity:**
     * Properties: `Id`, `CompanyId`, `YearName`, `StartDate`, `EndDate`, `IsActive`, `IsLocked`, and audit metadata.
     * Invariants:
       * `StartDate < EndDate` strictly enforced.
       * Range containment (`Contains(DateOnly)`) and overlap check (`OverlapsWith(FinancialYear)`).
   * **`GSTRegistrationType` Enum:** `Regular`, `Composition`, `Unregistered`, `OverseasOrSEZ`.

2. **Application Contracts, DTOs & Services (`BillsEasy.Application`):**
   * `CompanyDto`, `FinancialYearDto`, and standard `IndianStates.All` list (with 2-digit GST state codes e.g. Gujarat "24", Maharashtra "27", etc.).
   * `ICompanyRepository` & `IFinancialYearRepository` contracts.
   * `ICompanyContextService`: In-memory session context managing active open company and active financial year with change events.
   * `CompanyService`: Orchestrates `CreateCompany`, `UpdateCompany`, `DeleteCompany`, `GetAllCompanies`, and `GetCompanyById` with case-insensitive unique name validation and audit attribution.
   * `FinancialYearService`: Orchestrates `CreateFinancialYear`, `GetFinancialYearsByCompany`, and `SelectActiveFinancialYear` with overlap prevention.

3. **Infrastructure & Persistence (`BillsEasy.Infrastructure`):**
   * `DatabaseInitializer`: Automatically migrates and verifies `Companies` and `FinancialYears` tables in SQLite.
   * `CompanyRepository`: Parameterized SQLite queries managing company records and cascading financial years inside explicit atomic transactions (`BEGIN TRANSACTION`).
   * `FinancialYearRepository`: Parameterized SQLite queries managing financial years and atomic active year switches (`IsActive` toggling).
   * `CompanyContextService`: Desktop runtime state management for open company and fiscal year context.

4. **WPF UI Views & ViewModels (`BillsEasy.Presentation.Wpf`):**
   * `CompanyListDialog`: Native dialog to view, open, and delete existing companies.
   * `CompanyEditDialog`: Comprehensive company creation and alteration form with real-time Indian state picker, auto-filling state codes, date pickers, GSTIN validation, and keyboard shortcuts (`Enter` navigation, `Esc` cancel).
   * `FinancialYearManagementDialog`: View all fiscal periods for the active company, switch active year, and create subsequent financial years.
   * `MainWindow.xaml`: Updated desktop shell displaying the active company and active financial year in the title bar and status bar, with functional Company menu actions (`Alt+N` Create, `Alt+O` Open, `Alt+F` FY Management, Close Company).

---

## 2. Database Schema Introduced

```sql
-- Companies Table
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
    GSTRegistrationType INTEGER NOT NULL DEFAULT 1,
    IsSecurityEnabled INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS UQ_Companies_Name ON Companies(CompanyName);

-- FinancialYears Table
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
```

---

## 3. Automated Test Suite Results

* **Unit Tests (`BillsEasy.UnitTests`):** **39 Passed (100%)**
  * `CompanyTests`: Valid creation, initial FY auto-creation, empty name rejection, books beginning before FY start rejection, invalid GSTIN regex rejection, update property validation.
  * `FinancialYearTests`: Valid date bounds, StartDate $\ge$ EndDate rejection, date range containment, overlap detection, rejection of overlapping periods on Company aggregate.
  * `CompanyServiceTests` & `FinancialYearServiceTests`: Use-case workflows, duplicate company name rejection, audit attribution verification, overlapping FY service rejection.
  * `MoneyTests` & `AuditableEntityTests`: Foundation value object and entity equality tests.
* **Integration Tests (`BillsEasy.IntegrationTests`):** **7 Passed (100%)**
  * `CompanyRepositoryTests`: Add & get by ID, case-insensitive duplicate name detection (`ExistsByNameAsync`), update details with audit attribution, delete company with cascading foreign key cleanup.
  * `FinancialYearRepositoryTests`: Atomic active financial year switching across multiple years.
  * `SqliteConnectionFactoryTests`: WAL mode verification, foreign keys enforcement, DDL/DML execution.
* **Overall:** **46 / 46 Tests Passing. 0 Failures.**

---

## 4. Build Status
* **`dotnet build BillsEasy.sln`:** `Build Succeeded.` **0 Warnings, 0 Errors.**

---

## 5. Deliberately Deferred Functionality (Not in Scope)
* **No Accounting Masters:** `AccountGroup`, `Ledger`, `Party` (Scheduled for Stages 1.3 & 1.4).
* **No Transaction Core:** `Voucher`, `VoucherLine`, `OpeningBalance` vouchers (Scheduled for Stage 1.6).
* **No Commercial Vouchers:** `Sales`, `Purchase`, `Payment`, `Receipt` (Scheduled for Stages 1.7+).
* **No Inventory or Advanced GST Engines:** (Reserved for later phases).

---

## 6. Known Risks & Architectural Decisions
1. **Case-Insensitive Company Names:** Implemented `LOWER(CompanyName) = LOWER(@Name)` check to prevent subtle duplicate company collisions on Windows.
2. **Atomic Initial FY Creation:** When creating a company, its initial `FinancialYear` record is committed within the exact same database transaction as the `Company` row.
3. **Session Context Management:** `CompanyContextService` manages the active company lifecycle in memory, keeping Presentation views reactive via `ActiveCompanyChanged` events without tight coupling.
