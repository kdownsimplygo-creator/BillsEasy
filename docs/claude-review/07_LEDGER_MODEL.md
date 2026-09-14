# 07. Ledger & Party Model Review

## 1. The Unified Ledger Principle (No Parallel Party Entity)
In classic Indian accounting systems (BUSY, Tally):
- **A Customer is simply a Ledger under `Sundry Debtors`** (or a child group thereof).
- **A Supplier is simply a Ledger under `Sundry Creditors`** (or a child group thereof).
- **A Bank is simply a Ledger under `Bank Accounts`**.
- **An Expense is simply a Ledger under `Direct Expenses` or `Indirect Expenses`**.

### Architecture Compliance:
- **`Party` Table:** **NONE.**
- **`Customer` Table:** **NONE.**
- **`Supplier` Table:** **NONE.**
- **`AccountingEntry` Table:** **NONE.**

---

## 2. Field Categorization & Ledger Schema

```
LEDGER AGGREGATE
├── Identity & Classification
│   ├── Id (Guid)
│   ├── CompanyId (Guid)
│   ├── GroupId (Guid)
│   ├── Name (string, unique per company)
│   ├── PrintName (string)
│   └── Alias (string?)
├── Setup Balance & Credit Terms
│   ├── OpeningBalance (Money, >= 0)
│   ├── OpeningBalanceType (Debit/Credit)
│   ├── BillWiseEnabled (bool)
│   ├── CreditPeriodDays (int?)
│   └── CreditLimit (Money?)
├── Statutory & Jurisdiction
│   ├── AddressLine1, AddressLine2, City, PinCode, Country
│   ├── StateName, StateCode
│   ├── GSTIN (15-char regex validated)
│   ├── GSTRegistrationType
│   ├── PAN (10-char regex validated)
│   └── AadhaarNumber (12-digit format)
├── Contact & Banking
│   ├── ContactPerson, MobileNumber, WhatsAppNumber, Email
│   └── BankName, BankAccountNumber, BankIFSC, BankBranch
└── Governance & Audit
    ├── IsDefaultSystemLedger (bool)
    └── CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
```

---

## 3. Opening Balance Architectural Analysis & Recommendation

### The Question:
Should `OpeningBalance` and `OpeningBalanceType` remain on `Ledger` as setup information, or should they eventually be represented through the Voucher engine?

### Architectural Finding & Recommendation:
1. **Current State:** Storing `OpeningBalance` and `OpeningBalanceType` directly on the `Ledger` aggregate represents initial setup master data when a business is first migrated into BillsEasy.
2. **Future State (Phase 1.5+):** 
   - Dynamic running balances in statements will compute:
     $$\text{Closing Balance} = \text{Opening Balance} + \sum \text{Debits} - \sum \text{Credits}$$
   - When transitioning between financial years (e.g. FY 2026-27 to FY 2027-28), the closing balance of FY 2026-27 will either:
     - Option A: Generate a synthetic/special `Opening Voucher` in FY 2027-28, OR
     - Option B: Be dynamically evaluated up to the FY start date from prior transactions.
   - **Recommendation:** Maintain `OpeningBalance` on `Ledger` as the **initial books inception balance** (at `Company.BooksBeginningFrom`). Do **not** mutate this field during day-to-day transactions or normal voucher posting. All subsequent period opening balances should be derived from the transaction ledger.
