# BillsEasy — Testing Debt & Deferred Integration Test Guards

**Status:** All Items Resolved  
**Target Milestone:** Phase 1.5-B (Core Voucher Engine & Double-Entry Posting)

---

## 1. Overview

During the Pre-Phase-1.5 and Phase 1.5-A foundation hardening passes, critical business guards protecting historical accounting data were architected and unit-tested in the Domain and Application layers. Because transactional tables (`Vouchers` and `VoucherLines`) were introduced in Phase 1.5-B, repository-level transaction existence queries (`HasTransactionsAsync`) against production tables now run against live posting data.

All deferred integration tests have been implemented and verified in Phase 1.5-B.

---

## 2. Tracked Testing Debt Items

### TD-01: `Ledger.UpdateOpeningBalance` & `LedgerService.UpdateLedgerAsync` Transaction Guard
- **Status:** **RESOLVED** (Phase 1.5-B)
- **Guard Logic:**
  - `Ledger.UpdateOpeningBalance(Money amount, DebitCreditType type, bool hasTransactionHistory)` throws `DomainException("Cannot modify Opening Balance for a ledger that has recorded transaction history / vouchers.")` when `hasTransactionHistory == true` and opening balance is modified.
  - `LedgerService.UpdateLedgerAsync` queries `_ledgerRepository.HasTransactionsAsync(companyId, ledgerId)`.
- **Verified Integration Test Coverage:**
  - `LedgerRepositoryTests.TD01_UpdateOpeningBalance_WhenLedgerHasVouchers_ThrowsDomainExceptionAndBlocksUpdate`
    1. Seeds Company, FY, AccountGroup, and Ledger.
    2. Posts a real financial voucher referencing this `LedgerId` in `VoucherLines`.
    3. Asserts `HasTransactionsAsync` transitions from `false` to `true`.
    4. Invokes `LedgerService.UpdateLedgerAsync` modifying `OpeningBalance`.
    5. Verifies `DomainException` is thrown: `"Cannot modify Opening Balance for a ledger that has recorded transaction history / vouchers."`.

---

### TD-02: `CompanyService.DeleteCompanyAsync` & `CompanyRepository.DeleteAsync` Atomic Transaction Guard
- **Status:** **RESOLVED** (Phase 1.5-B)
- **Guard Logic:**
  - `CompanyRepository.DeleteAsync` checks if `Vouchers` table has any transaction records for `CompanyId` inside an atomic SQLite transaction, throwing `DomainException` if count > 0.
  - `CompanyService.DeleteCompanyAsync` validates `_companyRepository.HasTransactionsAsync(id)`.
- **Verified Integration Test Coverage:**
  - `CompanyRepositoryTests.TD02_CompanyService_DeleteCompanyAsync_WhenRealVoucherExists_ThrowsDomainExceptionAndPreservesCompanyAndFY`
    1. Seeds Company, FY, AccountGroups, and Ledgers.
    2. Posts a real financial voucher through the Phase 1.5-B voucher repository.
    3. Invokes `CompanyService.DeleteCompanyAsync(companyId)`.
    4. Verifies `DeleteCompanyAsync` throws `DomainException` (`"Cannot delete company ... because it has one or more recorded financial transactions"`).
    5. Verifies company, financial years, and vouchers remain intact.
  - `CompanyRepositoryTests.DeleteAsync_WhenTransactionInsertedBetweenCheckAndDelete_AtomicallyBlocksDeletion` (Atomic race guard test).
  - `VoucherRepositoryTests.ADR10_DeleteCompany_WhenVoucherExists_FailsWithRestrictConstraint` (Database SQLite engine foreign key `ON DELETE RESTRICT` test).
