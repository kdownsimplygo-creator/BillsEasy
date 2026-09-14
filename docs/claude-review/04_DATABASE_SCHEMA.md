# 04. Database Schema Review

## 1. Relational Model & Foreign Key Rules

```
+-------------------------------------------------------------+
|                          COMPANIES                          |
| PK: Id (TEXT)                                               |
| UQ: CompanyName (COLLATE NOCASE)                            |
+-------------------------------------------------------------+
         | 1                                  | 1
         |                                    |
         | ON DELETE CASCADE                  | ON DELETE CASCADE
         v N                                  v N
+--------------------------------+   +------------------------------------+
|         FINANCIALYEARS         |   |           ACCOUNTGROUPS            |
| PK: Id (TEXT)                  |   | PK: Id (TEXT)                      |
| FK: CompanyId -> Companies.Id  |   | FK: CompanyId -> Companies.Id      |
| IX: IX_FinancialYears_CompanyId|   | FK: ParentGroupId -> AccGroups.Id  |
+--------------------------------+   |     (ON DELETE RESTRICT)           |
                                     | UQ: (CompanyId, Parent, Name)      |
                                     | UQ: (CompanyId, Name) WHERE Root   |
                                     +------------------------------------+
                                                      | 1
                                                      |
                                                      | ON DELETE RESTRICT
                                                      v N
                                     +------------------------------------+
                                     |              LEDGERS               |
                                     | PK: Id (TEXT)                      |
                                     | FK: CompanyId -> Companies.Id      |
                                     |     (ON DELETE CASCADE)            |
                                     | FK: GroupId -> AccountGroups.Id    |
                                     |     (ON DELETE RESTRICT)           |
                                     | UQ: (CompanyId, Name)              |
                                     | IX: IX_Ledgers_GroupId             |
                                     | IX: IX_Ledgers_CompanyId           |
                                     +------------------------------------+
```

---

## 2. Table-by-Table Schema Specification

### A. `Companies` Table
| Column Name | SQLite Data Type | Nullable | Constraints & Defaults | Domain Mapping |
|---|---|---|---|---|
| `Id` | `TEXT` | NOT NULL | PRIMARY KEY | `Guid Id` |
| `CompanyName` | `TEXT` | NOT NULL | UNIQUE (`UQ_Companies_Name` COLLATE NOCASE) | `string CompanyName` |
| `PrintName` | `TEXT` | NOT NULL | | `string PrintName` |
| `ShortName` | `TEXT` | NULL | | `string? ShortName` |
| `Country` | `TEXT` | NOT NULL | DEFAULT `'India'` | `string Country` |
| `StateName` | `TEXT` | NOT NULL | | `string StateName` |
| `StateCode` | `TEXT` | NOT NULL | | `string StateCode` |
| `FinancialYearBeginning`| `TEXT` | NOT NULL | ISO-8601 String | `DateTime FinancialYearBeginning`|
| `BooksBeginningFrom` | `TEXT` | NOT NULL | ISO-8601 String | `DateTime BooksBeginningFrom` |
| `CurrencySymbol` | `TEXT` | NOT NULL | DEFAULT `'₹'` | `string CurrencySymbol` |
| `CurrencyFormalName` | `TEXT` | NOT NULL | DEFAULT `'INR'` | `string CurrencyFormalName` |
| `GSTIN` | `TEXT` | NULL | Alphanumeric (15) | `string? GSTIN` |
| `GSTRegistrationType` | `INTEGER` | NOT NULL | DEFAULT `1` CHECK (BETWEEN 1 AND 4) | `GSTRegistrationType` |
| `IsSecurityEnabled` | `INTEGER` | NOT NULL | DEFAULT `0` | `bool IsSecurityEnabled` |
| `CreatedAt`, `CreatedBy`| `TEXT` | NOT NULL | Audit Attribution | `AuditableEntity` |
| `ModifiedAt`, `ModifiedBy`| `TEXT` | NULL | Audit Attribution | `AuditableEntity` |

### B. `FinancialYears` Table
| Column Name | SQLite Data Type | Nullable | Constraints & Defaults |
|---|---|---|---|
| `Id` | `TEXT` | NOT NULL | PRIMARY KEY |
| `CompanyId` | `TEXT` | NOT NULL | FOREIGN KEY (`Companies.Id`) ON DELETE CASCADE |
| `YearName` | `TEXT` | NOT NULL | e.g. "FY 2026-2027" |
| `StartDate` | `TEXT` | NOT NULL | ISO-8601 String |
| `EndDate` | `TEXT` | NOT NULL | ISO-8601 String, CHECK (`EndDate > StartDate`) |
| `IsActive` | `INTEGER` | NOT NULL | DEFAULT `0` |
| `IsLocked` | `INTEGER` | NOT NULL | DEFAULT `0` |
| Audit Columns | `TEXT` | | `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` |

**Unique Indexes:**
1. `UQ_FinancialYears_Company_OneActive`: `UNIQUE(CompanyId) WHERE IsActive = 1`

### C. `AccountGroups` Table
| Column Name | SQLite Data Type | Nullable | Constraints & Defaults |
|---|---|---|---|
| `Id` | `TEXT` | NOT NULL | PRIMARY KEY |
| `CompanyId` | `TEXT` | NOT NULL | FOREIGN KEY (`Companies.Id`) ON DELETE CASCADE |
| `Name` | `TEXT` | NOT NULL | Group Name |
| `ParentGroupId` | `TEXT` | NULL | FOREIGN KEY (`AccountGroups.Id`) ON DELETE RESTRICT |
| `Nature` | `INTEGER` | NOT NULL | 1=Asset, 2=Liability, 3=Capital, 4=Income, 5=Expense CHECK(BETWEEN 1 AND 5) |
| `IsPrimary` | `INTEGER` | NOT NULL | DEFAULT `0` |
| `IsDefaultSystemGroup` | `INTEGER` | NOT NULL | DEFAULT `0` |
| Audit Columns | `TEXT` | | `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` |

**Unique Indexes:**
1. `UQ_AccountGroups_Company_Parent_Name`: `UNIQUE(CompanyId, ParentGroupId, Name COLLATE NOCASE) WHERE ParentGroupId IS NOT NULL`
2. `UQ_AccountGroups_Company_Root_Name`: `UNIQUE(CompanyId, Name COLLATE NOCASE) WHERE ParentGroupId IS NULL`

### D. `Ledgers` Table
| Column Name | SQLite Data Type | Nullable | Constraints & Defaults |
|---|---|---|---|
| `Id` | `TEXT` | NOT NULL | PRIMARY KEY |
| `CompanyId` | `TEXT` | NOT NULL | FOREIGN KEY (`Companies.Id`) ON DELETE CASCADE |
| `GroupId` | `TEXT` | NOT NULL | FOREIGN KEY (`AccountGroups.Id`) ON DELETE RESTRICT |
| `Name` | `TEXT` | NOT NULL | `UNIQUE(CompanyId, Name COLLATE NOCASE)` |
| `PrintName` | `TEXT` | NOT NULL | Defaults to `Name` |
| `Alias` | `TEXT` | NULL | |
| `OpeningBalance` | `INTEGER` | NOT NULL | DEFAULT `0` (Stored in minor units / paise) |
| `OpeningBalanceType` | `INTEGER` | NOT NULL | DEFAULT `1` (1=Debit, 2=Credit CHECK(IN (1, 2))) |
| `BillWiseEnabled` | `INTEGER` | NOT NULL | DEFAULT `0` (1=Enabled) |
| `CreditPeriodDays` | `INTEGER` | NULL | Default credit term in days |
| `CreditLimit` | `INTEGER` | NULL | Credit ceiling in minor units / paise |
| `AddressLine1`, `2` | `TEXT` | NULL | Postal address |
| `City`, `StateName`, `StateCode`, `PinCode`, `Country` | `TEXT` | | Jurisdiction info |
| `GSTIN`, `GSTRegistrationType`, `PAN`, `AadhaarNumber` | `TEXT`/`INT` | | Statutory tax metadata (GSTRegistrationType CHECK(BETWEEN 1 AND 4)) |
| `ContactPerson`, `MobileNumber`, `WhatsAppNumber`, `Email` | `TEXT` | NULL | Party contact info |
| `BankName`, `BankAccountNumber`, `BankIFSC`, `BankBranch` | `TEXT` | NULL | Banking info |
| `IsDefaultSystemLedger` | `INTEGER` | NOT NULL | DEFAULT `0` |
| Audit Columns | `TEXT` | | `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` |

---

## 3. Schema Quality & Integrity Findings
1. **Case-Insensitive Uniqueness:** Enforced via `COLLATE NOCASE` across all unique indexes, preventing case variation collision (e.g. "Cash" vs "cash").
2. **Referential Deletion Protection:** Deleting an `AccountGroup` is strictly blocked by foreign key constraint (`ON DELETE RESTRICT`) if any child group or ledger references it.
3. **Multi-Tenancy Isolation:** Every single table is strictly partitioned by `CompanyId`.
