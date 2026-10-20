// NOTE: Before modifying schema or adding tables in Phase 1.5-B, consult ADR 10 (Foreign Key & Cascade Deletion Invariants) in docs/DECISION_LOG.md.
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;

namespace BillsEasy.Infrastructure.Persistence;

public class DatabaseInitializer
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;

    public DatabaseInitializer(ISqliteDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = @"
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
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Companies_Name ON Companies(CompanyName COLLATE NOCASE);

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
            CREATE INDEX IF NOT EXISTS IX_FinancialYears_CompanyId ON FinancialYears(CompanyId);
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_FinancialYears_Company_OneActive ON FinancialYears(CompanyId) WHERE IsActive = 1;

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
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Ledgers_Company_Name ON Ledgers(CompanyId, Name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_Ledgers_GroupId ON Ledgers(GroupId);
            CREATE INDEX IF NOT EXISTS IX_Ledgers_CompanyId ON Ledgers(CompanyId);

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
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Vouchers_Company_FY_Number ON Vouchers(CompanyId, FinancialYearId, VoucherNumber COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_Vouchers_CompanyId ON Vouchers(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_Vouchers_FinancialYearId ON Vouchers(FinancialYearId);
            CREATE INDEX IF NOT EXISTS IX_Vouchers_VoucherDate ON Vouchers(VoucherDate);
            CREATE INDEX IF NOT EXISTS IX_Vouchers_Type ON Vouchers(Type);
            CREATE INDEX IF NOT EXISTS IX_Vouchers_Status ON Vouchers(Status);

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
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_VoucherLines_Voucher_Sequence ON VoucherLines(VoucherId, SequenceNumber);
            CREATE INDEX IF NOT EXISTS IX_VoucherLines_VoucherId ON VoucherLines(VoucherId);
            CREATE INDEX IF NOT EXISTS IX_VoucherLines_LedgerId ON VoucherLines(LedgerId);

            CREATE TABLE IF NOT EXISTS BillReferences (
                Id TEXT PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                LedgerId TEXT NOT NULL,
                ReferenceNumber TEXT NOT NULL,
                ReferenceType INTEGER NOT NULL CHECK (ReferenceType IN (1, 2)),
                Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (1, 2)),
                BillDate TEXT NOT NULL,
                DueDate TEXT,
                OriginalAmount INTEGER NOT NULL CHECK (OriginalAmount > 0),
                EntryType INTEGER NOT NULL CHECK (EntryType IN (1, 2)),
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
            CREATE INDEX IF NOT EXISTS IX_BillReferences_CompanyId ON BillReferences(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_BillReferences_LedgerId ON BillReferences(LedgerId);
            CREATE INDEX IF NOT EXISTS IX_BillReferences_Status ON BillReferences(Status);
            CREATE INDEX IF NOT EXISTS IX_BillReferences_BillDate ON BillReferences(BillDate);

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
            CREATE INDEX IF NOT EXISTS IX_BillAllocations_CompanyId ON BillAllocations(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_BillAllocations_VoucherLineId ON BillAllocations(VoucherLineId);
            CREATE INDEX IF NOT EXISTS IX_BillAllocations_BillReferenceId ON BillAllocations(BillReferenceId);

            CREATE TABLE IF NOT EXISTS Units (
                Id TEXT PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                Name TEXT NOT NULL,
                PrintName TEXT NOT NULL,
                DecimalPlaces INTEGER NOT NULL DEFAULT 0 CHECK (DecimalPlaces BETWEEN 0 AND 4),
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Units_Company_Name ON Units(CompanyId, Name COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_Units_CompanyId ON Units(CompanyId);

            CREATE TABLE IF NOT EXISTS ItemGroups (
                Id TEXT PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                Name TEXT NOT NULL,
                ParentGroupId TEXT,
                Description TEXT,
                IsDefaultSystemGroup INTEGER NOT NULL DEFAULT 0 CHECK (IsDefaultSystemGroup IN (0, 1)),
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
                FOREIGN KEY (ParentGroupId) REFERENCES ItemGroups(Id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_ItemGroups_Company_Parent_Name
                ON ItemGroups(CompanyId, ParentGroupId, Name COLLATE NOCASE)
                WHERE ParentGroupId IS NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_ItemGroups_Company_Root_Name
                ON ItemGroups(CompanyId, Name COLLATE NOCASE)
                WHERE ParentGroupId IS NULL;
            CREATE INDEX IF NOT EXISTS IX_ItemGroups_CompanyId ON ItemGroups(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_ItemGroups_ParentGroupId ON ItemGroups(ParentGroupId);

            CREATE TABLE IF NOT EXISTS Items (
                Id TEXT PRIMARY KEY,
                CompanyId TEXT NOT NULL,
                ItemGroupId TEXT NOT NULL,
                UnitId TEXT NOT NULL,
                AlternateUnitId TEXT,
                ConversionFactor REAL,
                Name TEXT NOT NULL,
                PrintName TEXT NOT NULL,
                Code TEXT,
                OpeningStockQuantity REAL NOT NULL DEFAULT 0 CHECK (OpeningStockQuantity >= 0),
                OpeningStockRate INTEGER NOT NULL DEFAULT 0 CHECK (OpeningStockRate >= 0),
                OpeningStockValue INTEGER NOT NULL DEFAULT 0 CHECK (OpeningStockValue >= 0),
                SalesPrice INTEGER CHECK (SalesPrice IS NULL OR SalesPrice >= 0),
                PurchasePrice INTEGER CHECK (PurchasePrice IS NULL OR PurchasePrice >= 0),
                MRP INTEGER CHECK (MRP IS NULL OR MRP >= 0),
                MinSalePrice INTEGER CHECK (MinSalePrice IS NULL OR MinSalePrice >= 0),
                DiscountPercent REAL CHECK (DiscountPercent IS NULL OR (DiscountPercent >= 0 AND DiscountPercent <= 100)),
                MinLevel REAL CHECK (MinLevel IS NULL OR MinLevel >= 0),
                MaxLevel REAL CHECK (MaxLevel IS NULL OR MaxLevel >= 0),
                ReorderLevel REAL CHECK (ReorderLevel IS NULL OR ReorderLevel >= 0),
                Description TEXT,
                Barcode TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE CASCADE,
                FOREIGN KEY (ItemGroupId) REFERENCES ItemGroups(Id) ON DELETE RESTRICT,
                FOREIGN KEY (UnitId) REFERENCES Units(Id) ON DELETE RESTRICT,
                FOREIGN KEY (AlternateUnitId) REFERENCES Units(Id) ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Items_Company_Name ON Items(CompanyId, Name COLLATE NOCASE);
            CREATE UNIQUE INDEX IF NOT EXISTS UQ_Items_Company_Code ON Items(CompanyId, Code COLLATE NOCASE) WHERE Code IS NOT NULL AND Code != '';
            CREATE INDEX IF NOT EXISTS IX_Items_CompanyId ON Items(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_Items_ItemGroupId ON Items(ItemGroupId);
            CREATE INDEX IF NOT EXISTS IX_Items_UnitId ON Items(UnitId);

            -- Phase 1.8: Purchase Invoices & Stock Movements
            CREATE TABLE IF NOT EXISTS PurchaseInvoices (
                Id TEXT PRIMARY KEY NOT NULL,
                CompanyId TEXT NOT NULL,
                FinancialYearId TEXT NOT NULL,
                VoucherId TEXT,
                Series TEXT NOT NULL DEFAULT 'Main',
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                SupplierId TEXT NOT NULL,
                MaterialCentre TEXT NOT NULL DEFAULT 'Main Store',
                Narration TEXT,
                ItemSubtotal INTEGER NOT NULL CHECK (ItemSubtotal >= 0),
                SundrySubtotal INTEGER NOT NULL DEFAULT 0 CHECK (SundrySubtotal >= 0),
                GrandTotal INTEGER NOT NULL CHECK (GrandTotal >= 0),
                Status INTEGER NOT NULL DEFAULT 1 CHECK (Status IN (1, 2)),
                TransportName TEXT,
                GrRrNumber TEXT,
                TransportDate TEXT,
                VehicleNumber TEXT,
                StationFromPlace TEXT,
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT,
                FOREIGN KEY (FinancialYearId) REFERENCES FinancialYears(Id) ON DELETE RESTRICT,
                FOREIGN KEY (SupplierId) REFERENCES Ledgers(Id) ON DELETE RESTRICT,
                FOREIGN KEY (VoucherId) REFERENCES Vouchers(Id) ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS UQ_PurchaseInvoices_Company_FY_InvoiceNumber
                ON PurchaseInvoices(CompanyId, FinancialYearId, InvoiceNumber COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS IX_PurchaseInvoices_CompanyId ON PurchaseInvoices(CompanyId);
            CREATE INDEX IF NOT EXISTS IX_PurchaseInvoices_SupplierId ON PurchaseInvoices(SupplierId);
            CREATE INDEX IF NOT EXISTS IX_PurchaseInvoices_VoucherId ON PurchaseInvoices(VoucherId);
            CREATE INDEX IF NOT EXISTS IX_PurchaseInvoices_InvoiceDate ON PurchaseInvoices(InvoiceDate);

            CREATE TABLE IF NOT EXISTS PurchaseInvoiceItemLines (
                Id TEXT PRIMARY KEY NOT NULL,
                PurchaseInvoiceId TEXT NOT NULL,
                SequenceNumber INTEGER NOT NULL CHECK (SequenceNumber > 0),
                ItemId TEXT NOT NULL,
                Quantity TEXT NOT NULL,
                UnitId TEXT NOT NULL,
                Price INTEGER NOT NULL CHECK (Price >= 0),
                Amount INTEGER NOT NULL CHECK (Amount >= 0),
                FOREIGN KEY (PurchaseInvoiceId) REFERENCES PurchaseInvoices(Id) ON DELETE CASCADE,
                FOREIGN KEY (ItemId) REFERENCES Items(Id) ON DELETE RESTRICT,
                FOREIGN KEY (UnitId) REFERENCES Units(Id) ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS UQ_PurchaseItemLines_Invoice_Seq
                ON PurchaseInvoiceItemLines(PurchaseInvoiceId, SequenceNumber);
            CREATE INDEX IF NOT EXISTS IX_PurchaseItemLines_ItemId ON PurchaseInvoiceItemLines(ItemId);

            CREATE TABLE IF NOT EXISTS PurchaseInvoiceSundryLines (
                Id TEXT PRIMARY KEY NOT NULL,
                PurchaseInvoiceId TEXT NOT NULL,
                SequenceNumber INTEGER NOT NULL CHECK (SequenceNumber > 0),
                LedgerId TEXT NOT NULL,
                Percentage REAL,
                Amount INTEGER NOT NULL CHECK (Amount > 0),
                FOREIGN KEY (PurchaseInvoiceId) REFERENCES PurchaseInvoices(Id) ON DELETE CASCADE,
                FOREIGN KEY (LedgerId) REFERENCES Ledgers(Id) ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS UQ_PurchaseSundryLines_Invoice_Seq
                ON PurchaseInvoiceSundryLines(PurchaseInvoiceId, SequenceNumber);
            CREATE INDEX IF NOT EXISTS IX_PurchaseSundryLines_LedgerId ON PurchaseInvoiceSundryLines(LedgerId);

            CREATE TABLE IF NOT EXISTS StockMovements (
                Id TEXT PRIMARY KEY NOT NULL,
                CompanyId TEXT NOT NULL,
                ItemId TEXT NOT NULL,
                MovementDate TEXT NOT NULL,
                MovementType INTEGER NOT NULL CHECK (MovementType IN (1, 2, 3)),
                SourceDocumentType TEXT NOT NULL,
                SourceDocumentId TEXT NOT NULL,
                Quantity TEXT NOT NULL,
                UnitId TEXT NOT NULL,
                Rate INTEGER NOT NULL CHECK (Rate >= 0),
                TotalValue INTEGER NOT NULL CHECK (TotalValue >= 0),
                MaterialCentre TEXT NOT NULL DEFAULT 'Main Store',
                IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0, 1)),
                CreatedAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL,
                ModifiedAt TEXT,
                ModifiedBy TEXT,
                FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE RESTRICT,
                FOREIGN KEY (ItemId) REFERENCES Items(Id) ON DELETE RESTRICT,
                FOREIGN KEY (UnitId) REFERENCES Units(Id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS IX_StockMovements_Company_Item ON StockMovements(CompanyId, ItemId);
            CREATE INDEX IF NOT EXISTS IX_StockMovements_Company_Date ON StockMovements(CompanyId, MovementDate);
            CREATE INDEX IF NOT EXISTS IX_StockMovements_SourceDoc ON StockMovements(SourceDocumentId);
        ";

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}