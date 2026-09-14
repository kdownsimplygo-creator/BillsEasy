# 05. Company & Financial Year Review

## 1. Company Aggregate Model
- **Boundary:** `Company` is the top-level aggregate root. All master data (`FinancialYears`, `AccountGroups`, `Ledgers`, and future `Vouchers`) belong to a `Company`.
- **Creation Lifecycle:**
  1. `Company` is validated (Statutory state, books date >= FY beginning date, GSTIN).
  2. Initial `FinancialYear` is generated and marked active.
  3. Standard taxonomy of 25+ `AccountGroups` is seeded under this company.
  4. Default system ledgers (`Cash` under `Cash-in-Hand`, `Profit & Loss Account` under `Reserves & Surplus / Capital`) are seeded.
- **Isolation:** All queries filter on `CompanyId`. SQLite unique indexes on `Companies(CompanyName)` prevent collision at the root level.

---

## 2. Financial Year Invariants & Transition Readiness

### A. Core Invariants
1. **Non-Overlapping Periods:** `FinancialYearService.CreateFinancialYearAsync` checks for date overlaps with existing financial years in the same company.
2. **Single Active Year:** `FinancialYearRepository.SetActiveAsync` executes an atomic SQLite transaction:
   ```sql
   UPDATE FinancialYears SET IsActive = 0, ModifiedAt = @Now, ModifiedBy = @User WHERE CompanyId = @CompanyId;
   UPDATE FinancialYears SET IsActive = 1, ModifiedAt = @Now, ModifiedBy = @User WHERE Id = @FinancialYearId AND CompanyId = @CompanyId;
   ```
3. **Lock Protection:** `IsLocked` flag allows closing an audited or filed financial year to prevent retrospective voucher modifications.

### B. Future Transition Architecture Verification
- **Multiple Financial Years:** Fully supported. Users can switch active financial years or view historical records without deleting prior years.
- **Year-End Closing:** The design supports carrying forward ending balances as generated opening vouchers in the succeeding financial year.
- **Cross-FY Bill Settlement:** In Indian accounting (BUSY/Tally), invoices raised in FY1 may be settled via payment vouchers in FY2. Because bills belong to `Ledger` (which is company-wide, not FY-restricted), bill references can be settled seamlessly across financial year boundaries.
