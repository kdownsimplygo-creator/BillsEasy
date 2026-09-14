# BillsEasy — Database Architecture & Schema Specification (Phase 1 SQLite)

**Database Engine:** Embedded SQLite 3 (WAL Mode, Strict Foreign Keys)  
**ORM / Data Access:** Microsoft.Data.Sqlite + Dapper / Lightweight Repository (cleanly abstractable to PostgreSQL)

---

## 1. Table Verification & Justification Matrix

Before creating any table, its business necessity and single source of truth have been rigorously scrutinized:

| Table Name | Business Fact Represented | Owner Aggregate | Source of Truth? | Can It Be Derived? | Why It Must Exist | Duplicate State Risk? |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Companies** | Organization identity, fiscal calendar, state/GST registration | Company | Yes | No | Master organizational entity | None |
| **FinancialYears** | Fiscal date ranges, lock status, year names | Company | Yes | No | Defines accounting periods & boundaries | None |
| **AccountGroups** | Chart of accounts hierarchy and nature (Asset, Liability, etc.) | AccountGroup | Yes | No | Structural classification tree | None |
| **Ledgers** | Accounting accounts and Parties (Debtors, Creditors, Bank, Cash, Expenses) | Ledger | Yes | No | Primary accounting and business party entity | None (No duplicate Party table) |
| **Vouchers** | Header for an atomic financial transaction (Date, Number, Type, Narration) | Voucher | Yes | No | Legal and operational transaction header | None (No duplicate AccountingEntry table) |
| **VoucherLines** | Debit/Credit components of a financial transaction | Voucher | Yes | No | Atomic double-entry financial facts | None |
| **BillReferences** | Specific bill commitment or claim (Invoice/Opening/Advance ref) | Ledger | Yes | No | Required for bill-by-bill tracking & aging | None |
| **BillAllocations** | Payment/Receipt application against a specific BillReference | VoucherLine | Yes | No | Required to establish settlement links | None |
| **AuditLogs** | Immutable record of who modified what, when, old/new values | Audit | Yes | No | Statutory compliance & forensic trace | None |

---

## 2. Table Schemas & DDL Specifications

`sql
-- 1. COMPANIES
CREATE TABLE Companies (
    Id TEXT PRIMARY KEY,                       -- UUID / GUID
    CompanyName TEXT NOT NULL,
    PrintName TEXT NOT NULL,
    ShortName TEXT,
    Country TEXT NOT NULL DEFAULT 'India',
    StateName TEXT NOT NULL,
    StateCode TEXT NOT NULL,                   -- 2-digit GST state code (e.g. '24' for Gujarat)
    FinancialYearStart TEXT NOT NULL,          -- ISO-8601 Date: YYYY-MM-DD
    BooksBeginningFrom TEXT NOT NULL,          -- ISO-8601 Date: YYYY-MM-DD
    CurrencySymbol TEXT NOT NULL DEFAULT '?',
    CurrencyFormalName TEXT NOT NULL DEFAULT 'INR',
    GSTIN TEXT,                                -- 15-character GST identification number
    GSTRegistrationType TEXT NOT NULL DEFAULT 'Regular', -- Regular, Composition, Unregistered
    IsSecurityEnabled INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT
);

-- 2. ACCOUNT GROUPS
CREATE TABLE AccountGroups (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    Name TEXT NOT NULL,
    ParentGroupId TEXT,                        -- NULL for Top-level Roots
    Nature TEXT NOT NULL,                      -- 'Asset', 'Liability', 'Income', 'Expense'
    IsPrimary INTEGER NOT NULL DEFAULT 0,
    IsDefaultSystemGroup INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParentGroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX UQ_AccountGroups_Company_Name ON AccountGroups(CompanyId, Name);
CREATE INDEX IX_AccountGroups_ParentGroupId ON AccountGroups(ParentGroupId);

-- 3. LEDGERS (Accounts & Parties)
CREATE TABLE Ledgers (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    GroupId TEXT NOT NULL,
    Name TEXT NOT NULL,
    PrintName TEXT,
    OpeningBalance REAL NOT NULL DEFAULT 0.0,
    OpeningBalanceType TEXT NOT NULL DEFAULT 'Debit', -- 'Debit' or 'Credit'
    IsBillWiseEnabled INTEGER NOT NULL DEFAULT 0,
    CreditPeriodDays INTEGER NOT NULL DEFAULT 0,
    CreditLimit REAL,
    -- Address & Contact Information (Source-derived from BUSY Class 2)
    AddressLine1 TEXT,
    AddressLine2 TEXT,
    City TEXT,
    StateName TEXT,
    StateCode TEXT,
    Pincode TEXT,
    MobileNo TEXT,
    WhatsAppNo TEXT,
    Email TEXT,
    ContactPerson TEXT,
    -- Statutory Information
    GSTIN TEXT,
    PAN TEXT,
    GSTRegistrationType TEXT NOT NULL DEFAULT 'Unregistered',
    -- Bank Information
    BankAccountNo TEXT,
    BankIFSC TEXT,
    BankName TEXT,
    BankBranch TEXT,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
    FOREIGN KEY (GroupId) REFERENCES AccountGroups(Id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX UQ_Ledgers_Company_Name ON Ledgers(CompanyId, Name);
CREATE INDEX IX_Ledgers_GroupId ON Ledgers(GroupId);
CREATE INDEX IX_Ledgers_Company_GSTIN ON Ledgers(CompanyId, GSTIN);

-- 4. VOUCHERS (Transaction Headers)
CREATE TABLE Vouchers (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    VoucherType TEXT NOT NULL,                 -- 'Payment', 'Receipt', 'Journal', 'Contra', 'Sales', 'Purchase', 'OpeningBalance'
    VoucherNumber TEXT NOT NULL,
    VoucherDate TEXT NOT NULL,                 -- ISO-8601 Date: YYYY-MM-DD
    Narration TEXT,
    IsReversed INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    ModifiedAt TEXT,
    ModifiedBy TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX UQ_Vouchers_Company_Type_Number ON Vouchers(CompanyId, VoucherType, VoucherNumber);
CREATE INDEX IX_Vouchers_Date ON Vouchers(CompanyId, VoucherDate);

-- 5. VOUCHER LINES (Atomic Debit/Credit components)
CREATE TABLE VoucherLines (
    Id TEXT PRIMARY KEY,
    VoucherId TEXT NOT NULL,
    LedgerId TEXT NOT NULL,
    EntryType TEXT NOT NULL,                   -- 'Debit' or 'Credit'
    Amount REAL NOT NULL CHECK (Amount > 0),
    SequenceNumber INTEGER NOT NULL DEFAULT 1,
    LineNarration TEXT,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    FOREIGN KEY (VoucherId) REFERENCES Vouchers(Id) ON DELETE CASCADE,
    FOREIGN KEY (LedgerId) REFERENCES Ledgers(Id) ON DELETE RESTRICT
);
CREATE INDEX IX_VoucherLines_VoucherId ON VoucherLines(VoucherId);
CREATE INDEX IX_VoucherLines_LedgerId ON VoucherLines(LedgerId);

-- 6. BILL REFERENCES (Tracking outstanding bill commitments)
CREATE TABLE BillReferences (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    LedgerId TEXT NOT NULL,
    ReferenceNo TEXT NOT NULL,                 -- Bill / Ref Number (e.g. 'INV-001', 'OB-001')
    ReferenceType TEXT NOT NULL,               -- 'NewRef', 'OpeningRef', 'Advance'
    BillDate TEXT NOT NULL,                    -- ISO-8601 Date
    DueDate TEXT,                              -- ISO-8601 Date
    OriginalAmount REAL NOT NULL CHECK (OriginalAmount > 0),
    EntryType TEXT NOT NULL,                   -- 'Debit' (Receivable) or 'Credit' (Payable)
    SourceVoucherId TEXT,                      -- Link to originating voucher (NULL for direct OpeningRef)
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
    FOREIGN KEY (LedgerId) REFERENCES Ledgers(Id) ON DELETE RESTRICT,
    FOREIGN KEY (SourceVoucherId) REFERENCES Vouchers(Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX UQ_BillReferences_Ledger_RefNo ON BillReferences(LedgerId, ReferenceNo);
CREATE INDEX IX_BillReferences_LedgerId ON BillReferences(LedgerId);

-- 7. BILL ALLOCATIONS (Settlement applications)
CREATE TABLE BillAllocations (
    Id TEXT PRIMARY KEY,
    VoucherLineId TEXT NOT NULL,
    BillReferenceId TEXT NOT NULL,
    AllocationType TEXT NOT NULL,              -- 'AgainstRef', 'Advance', 'OnAccount'
    AllocatedAmount REAL NOT NULL CHECK (AllocatedAmount > 0),
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    FOREIGN KEY (VoucherLineId) REFERENCES VoucherLines(Id) ON DELETE CASCADE,
    FOREIGN KEY (BillReferenceId) REFERENCES BillReferences(Id) ON DELETE RESTRICT
);
CREATE INDEX IX_BillAllocations_VoucherLineId ON BillAllocations(VoucherLineId);
CREATE INDEX IX_BillAllocations_BillReferenceId ON BillAllocations(BillReferenceId);

-- 8. AUDIT LOGS (Immutable History)
CREATE TABLE AuditLogs (
    Id TEXT PRIMARY KEY,
    CompanyId TEXT NOT NULL,
    EntityType TEXT NOT NULL,                  -- 'Voucher', 'Ledger', 'AccountGroup', 'Company'
    EntityId TEXT NOT NULL,
    ActionType TEXT NOT NULL,                  -- 'Created', 'Modified', 'Deleted', 'Reversed'
    OldValuesJson TEXT,
    NewValuesJson TEXT,
    Timestamp TEXT NOT NULL,                   -- ISO-8601 UTC
    UserId TEXT NOT NULL,
    Workstation TEXT,
    FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
);
CREATE INDEX IX_AuditLogs_Entity ON AuditLogs(CompanyId, EntityType, EntityId);
`

---

## 3. Transaction Boundaries & Performance Indexes

1. **Voucher Persistence Unit of Work:**
   - When a voucher is saved or edited:
     BEGIN IMMEDIATE TRANSACTION
     $\to$ Insert/Update Vouchers
     $\to$ Insert/Update VoucherLines
     $\to$ Insert/Update BillAllocations
     $\to$ Insert AuditLogs
     $\to$ Verify Double-Entry Balance Assertion ( == Credit$)
     $\to$ COMMIT
2. **Query Performance Optimization:**
   - Ledger Account Statement: IX_VoucherLines_LedgerId joined with Vouchers.VoucherDate for lightning-fast sub-second statements across millions of rows.
   - Pending Bills Computation: IX_BillAllocations_BillReferenceId joined with BillReferences yields immediate calculation of unsettled bills.
