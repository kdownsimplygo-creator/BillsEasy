-- =============================================================================
-- BILLSEASY — CURRENT SQLITE DATABASE SCHEMA (PHASE 1.1 - PHASE 1.4)
-- Generated from src/BillsEasy.Infrastructure/Persistence/DatabaseInitializer.cs
-- Target Database: SQLite 3.x (WAL mode, foreign_keys = ON)
-- =============================================================================

PRAGMA foreign_keys = ON;

-- -----------------------------------------------------------------------------
-- TABLE: Companies
-- Authoritative root aggregate for company profile and accounting metadata.
-- -----------------------------------------------------------------------------
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
    GSTRegistrationType INTEGER NOT NULL DEFAULT 1 CHECK (GSTRegistrationType BETWEEN 1 AND 4),
    IsSecurityEnabled INTEGER NOT NULL DEFAULT 0 CHECK (IsSecurityEnabled IN (0, 1)),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_Companies_Name 
    ON Companies(CompanyName COLLATE NOCASE);

-- -----------------------------------------------------------------------------
-- TABLE: FinancialYears
-- Financial year definitions belonging to a Company.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS FinancialYears (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    YearName TEXT NOT NULL,
    StartDate TEXT NOT NULL,
    EndDate TEXT NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 0 CHECK (IsActive IN (0, 1)),
    IsLocked INTEGER NOT NULL DEFAULT 0 CHECK (IsLocked IN (0, 1)),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
    CHECK (EndDate > StartDate)
);

CREATE INDEX IF NOT EXISTS IX_FinancialYears_CompanyId 
    ON FinancialYears(CompanyId);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_FinancialYears_Company_OneActive 
    ON FinancialYears(CompanyId) WHERE IsActive = 1;

-- -----------------------------------------------------------------------------
-- TABLE: AccountGroups
-- Hierarchical taxonomy for account classification (Asset, Liability, Capital, Income, Expense).
-- Sibling uniqueness: (CompanyId, ParentGroupId, Name)
-- Root uniqueness:    (CompanyId, Name) WHERE ParentGroupId IS NULL
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS AccountGroups (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    Name TEXT NOT NULL,
    ParentGroupId TEXT,
    Nature INTEGER NOT NULL CHECK (Nature BETWEEN 1 AND 5),
    IsPrimary INTEGER NOT NULL DEFAULT 0 CHECK (IsPrimary IN (0, 1)),
    IsDefaultSystemGroup INTEGER NOT NULL DEFAULT 0 CHECK (IsDefaultSystemGroup IN (0, 1)),
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

-- -----------------------------------------------------------------------------
-- TABLE: Ledgers
-- Single authoritative entity for all accounts & parties (Customers, Suppliers, Banks, Expenses, etc.)
-- Unique ledger name per Company: (CompanyId, Name COLLATE NOCASE)
-- Delete restricted when referenced by group.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Ledgers (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    GroupId TEXT NOT NULL,
    Name TEXT NOT NULL,
    PrintName TEXT NOT NULL,
    Alias TEXT,
    OpeningBalance INTEGER NOT NULL DEFAULT 0,
    OpeningBalanceType INTEGER NOT NULL DEFAULT 1 CHECK (OpeningBalanceType IN (1, 2)),
    BillWiseEnabled INTEGER NOT NULL DEFAULT 0 CHECK (BillWiseEnabled IN (0, 1)),
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
    GSTRegistrationType INTEGER NOT NULL DEFAULT 1 CHECK (GSTRegistrationType BETWEEN 1 AND 4),
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
    IsDefaultSystemLedger INTEGER NOT NULL DEFAULT 0 CHECK (IsDefaultSystemLedger IN (0, 1)),
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

-- -----------------------------------------------------------------------------
-- TABLE: Vouchers
-- Core generic double-entry accounting transactions header.
-- Foreign keys to Companies and FinancialYears ON DELETE RESTRICT (ADR 10).
-- Unique voucher number per FinancialYear: (CompanyId, FinancialYearId, VoucherNumber COLLATE NOCASE)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Vouchers (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    FinancialYearId TEXT NOT NULL,
    VoucherNumber TEXT NOT NULL,
    Type INTEGER NOT NULL CHECK (Type BETWEEN 1 AND 8),
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status BETWEEN 1 AND 3),
    VoucherDate TEXT NOT NULL,
    ReferenceNumber TEXT,
    Narration TEXT,
    TotalAmount INTEGER NOT NULL CHECK (TotalAmount > 0),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    PostedAt TEXT,
    PostedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT,
    FOREIGN KEY (FinancialYearId) REFERENCES FinancialYears(Id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_Vouchers_Company_FY_Number 
    ON Vouchers(CompanyId, FinancialYearId, VoucherNumber COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS IX_Vouchers_CompanyId 
    ON Vouchers(CompanyId);

CREATE INDEX IF NOT EXISTS IX_Vouchers_FinancialYearId 
    ON Vouchers(FinancialYearId);

CREATE INDEX IF NOT EXISTS IX_Vouchers_VoucherDate 
    ON Vouchers(VoucherDate);

CREATE INDEX IF NOT EXISTS IX_Vouchers_Type 
    ON Vouchers(Type);

CREATE INDEX IF NOT EXISTS IX_Vouchers_Status 
    ON Vouchers(Status);

-- -----------------------------------------------------------------------------
-- TABLE: VoucherLines
-- Individual debit and credit transaction lines for a voucher.
-- Cascade deletes when voucher is deleted (aggregate boundary).
-- Delete restricted for referenced Ledger (ADR 10).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VoucherLines (
    Id TEXT PRIMARY KEY,
    VoucherId TEXT NOT NULL,
    LedgerId TEXT NOT NULL,
    SequenceNumber INTEGER NOT NULL CHECK (SequenceNumber > 0),
    Type INTEGER NOT NULL CHECK (Type IN (1, 2)),
    Amount INTEGER NOT NULL CHECK (Amount > 0),
    Narration TEXT,
    FOREIGN KEY (VoucherId) REFERENCES Vouchers(Id) ON DELETE CASCADE,
    FOREIGN KEY (LedgerId) REFERENCES Ledgers(Id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_VoucherLines_Voucher_Sequence 
    ON VoucherLines(VoucherId, SequenceNumber);

CREATE INDEX IF NOT EXISTS IX_VoucherLines_VoucherId 
    ON VoucherLines(VoucherId);

CREATE INDEX IF NOT EXISTS IX_VoucherLines_LedgerId 
    ON VoucherLines(LedgerId);

-- -----------------------------------------------------------------------------
-- TABLE: BillReferences
-- Tracks specific commercial debt obligations (Invoices and Opening bills).
-- Scope: Partitioned by (CompanyId, LedgerId). Persists across financial years.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS BillReferences (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    LedgerId TEXT NOT NULL,
    ReferenceNumber TEXT NOT NULL,
    ReferenceType INTEGER NOT NULL CHECK (ReferenceType IN (1, 2)), -- 1: NewRef, 2: OpeningRef
    Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (1, 2)),      -- 1: Active, 2: Cancelled
    BillDate TEXT NOT NULL,
    DueDate TEXT,
    OriginalAmount INTEGER NOT NULL CHECK (OriginalAmount > 0),
    EntryType INTEGER NOT NULL CHECK (EntryType IN (1, 2)),          -- 1: Debit (Receivable), 2: Credit (Payable)
    SourceVoucherLineId TEXT,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT,
    FOREIGN KEY (LedgerId) REFERENCES Ledgers(Id) ON DELETE RESTRICT,
    FOREIGN KEY (SourceVoucherLineId) REFERENCES VoucherLines(Id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX IF NOT EXISTS UQ_BillReferences_Company_Ledger_RefNo 
    ON BillReferences(CompanyId, LedgerId, ReferenceNumber COLLATE NOCASE);

CREATE INDEX IF NOT EXISTS IX_BillReferences_CompanyId 
    ON BillReferences(CompanyId);

CREATE INDEX IF NOT EXISTS IX_BillReferences_LedgerId 
    ON BillReferences(LedgerId);

CREATE INDEX IF NOT EXISTS IX_BillReferences_Status 
    ON BillReferences(Status);

CREATE INDEX IF NOT EXISTS IX_BillReferences_BillDate 
    ON BillReferences(BillDate);

-- -----------------------------------------------------------------------------
-- TABLE: BillAllocations
-- Links a settlement VoucherLine to a specific BillReference.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS BillAllocations (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    VoucherLineId TEXT NOT NULL,
    BillReferenceId TEXT NOT NULL,
    AllocatedAmount INTEGER NOT NULL CHECK (AllocatedAmount > 0),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT,
    FOREIGN KEY (VoucherLineId) REFERENCES VoucherLines(Id) ON DELETE CASCADE,
    FOREIGN KEY (BillReferenceId) REFERENCES BillReferences(Id) ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS IX_BillAllocations_CompanyId 
    ON BillAllocations(CompanyId);

CREATE INDEX IF NOT EXISTS IX_BillAllocations_VoucherLineId 
    ON BillAllocations(VoucherLineId);

CREATE INDEX IF NOT EXISTS IX_BillAllocations_BillReferenceId 
    ON BillAllocations(BillReferenceId);

