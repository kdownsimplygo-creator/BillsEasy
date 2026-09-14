# 03. Domain Model Inventory

## 1. Domain Entities & Value Objects Matrix

| Domain Type | Type Classification | Identity / Key | Ownership / Scope | Purpose | Authoritative vs Derived |
|---|---|---|---|---|---|
| **`Company`** | Aggregate Root | `Guid Id` | Independent Root | Authoritative master for business identity, GSTIN, and financial year collection. | **Authoritative** |
| **`FinancialYear`** | Entity | `Guid Id` | Child of `Company` (`CompanyId`) | Defines accounting period bounds (`StartDate` to `EndDate`), active status, and lock status. | **Authoritative** |
| **`AccountGroup`** | Aggregate Root | `Guid Id` | Scoped to `Company` (`CompanyId`) | Defines classification hierarchy and fundamental accounting nature (`GroupNature`). | **Authoritative** |
| **`Ledger`** | Aggregate Root | `Guid Id` | Scoped to `Company` (`CompanyId`) & `AccountGroup` (`GroupId`) | Single authoritative entity for all ledger accounts, parties (customers/suppliers), banks, and expenses. | **Authoritative** |
| **`Money`** | Value Object | Value Equality (`Amount`) | Owned by host entity | Encapsulates monetary values with 2-decimal arithmetic invariants and non-negative assertions. | **Authoritative Value Object** |
| **`DebitCreditType`** | Domain Enum | `int` (1 = Debit, 2 = Credit) | N/A | Classifies opening balance and future transaction line debit/credit orientation. | **Domain Primitive** |
| **`GroupNature`** | Domain Enum | `int` (1=Asset, 2=Liability, 3=Capital, 4=Income, 5=Expense) | N/A | Governs financial statement placement (Balance Sheet vs Profit & Loss). | **Domain Primitive** |
| **`GSTRegistrationType`** | Domain Enum | `int` (1=Regular, 2=Composition, 3=Unregistered, 4=Consumer, 5=Overseas, 6=SEZ) | N/A | Determines GST tax applicability and compliance reporting. | **Domain Primitive** |

---

## 2. Invariants & Domain Rules Detailed Inspection

### A. `Company` Invariants
- `CompanyName` and `PrintName` cannot be null or whitespace.
- `StateName` and `StateCode` (2-digit Indian State code e.g. "24") are mandatory.
- `FinancialYearBeginning` must not be default `DateTime.MinValue`.
- `BooksBeginningFrom` must be on or after `FinancialYearBeginning` (`BooksBeginningFrom >= FinancialYearBeginning`).
- When created, automatically initializes an active initial `FinancialYear` spanning from `FinancialYearBeginning` to `FinancialYearBeginning.AddYears(1).AddDays(-1)`.
- If `GSTIN` is provided, it must strictly match the 15-character statutory alphanumeric regex: `^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$`.

### B. `FinancialYear` Invariants
- `YearName` cannot be null or whitespace.
- `EndDate` must be strictly after `StartDate` (`EndDate > StartDate`).
- Overlapping date ranges for the same company are prohibited.
- Only exactly one `FinancialYear` per `Company` can have `IsActive == true` at any given time.
- Locked financial years (`IsLocked == true`) prohibit adding or modifying accounting transactions.

### C. `AccountGroup` Invariants
- `Name` cannot be null or whitespace.
- `ParentGroupId` must belong to the same `CompanyId`.
- Cannot set `ParentGroupId` to itself (`ParentGroupId != Id`).
- Hierarchy updates prohibit circular dependency chains (e.g. A $\rightarrow$ B $\rightarrow$ C $\rightarrow$ A).
- Sub-groups automatically inherit their parent's `GroupNature`.
- Duplicate sibling group names under the same parent (or duplicate root group names) within the same company are rejected (case-insensitive).
- System groups (`IsDefaultSystemGroup == true`) cannot be deleted or renamed.

### D. `Ledger` Invariants
- Single unified entity: Customers are ledgers under `Sundry Debtors`, Suppliers are ledgers under `Sundry Creditors`.
- `Name` and `PrintName` cannot be null or whitespace (defaults `PrintName` to `Name` if omitted).
- `CompanyId` and `GroupId` must be non-empty and point to existing entities.
- Ledger name must be unique within the same company (case-insensitive).
- `OpeningBalance` is non-negative (`Amount >= 0`), stored as minor units integer paise.
- `OpeningBalance` and `OpeningBalanceType` cannot be modified once the ledger has recorded financial transactions / vouchers (`hasTransactionHistory == true`).
- `CreditPeriodDays` cannot be negative.
- If `GSTIN` is provided, it is validated against the 15-character statutory regex.
- System ledgers (`IsDefaultSystemLedger == true`, e.g. `Cash`, `Profit & Loss Account`) cannot be deleted.
- Deletion is prohibited if the ledger has associated transactions or bill-wise references.
