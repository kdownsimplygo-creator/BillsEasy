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
        ";

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}