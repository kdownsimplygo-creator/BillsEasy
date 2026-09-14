# Phase 1.4.2 — Principal Architect Remediation Report (Pre-Phase-1.5 Hardening)

**Date**: 2026-09-13  
**Status**: Completed & Verified  
**Scope**: Remediation of Phase 1.1–1.4 foundation findings prior to Phase 1.5 Voucher Engine  
**Build Status**: `0 Warning(s)`, `0 Error(s)`  
**Test Suite Status**: `124 Passed`, `0 Failed`, `0 Skipped` (100% Passing across Unit and Integration test suites)

---

## 1. Executive Summary

Following an independent line-by-line Principal Architect review of the BillsEasy Phase 1.1–1.4 codebase, four targeted architectural remediations were identified to harden the foundation before unfreezing Phase 1.5 (Voucher Engine). Zero feature creep or premature voucher development was introduced during this phase.

All four remediations have been implemented in accordance with Clean Architecture, fully tested with automated unit and integration tests, and verified.

---

## 2. Detailed Remediations & Technical Report

### Remediation 1: Single Monetary Model (`Money` as Authoritative Domain Representation)
- **Finding**: Inconsistent monetary representation across layers. Domain entity `Ledger` exposed decimal properties (`OpeningBalance`, `CreditLimit`), while `Money` value object was introduced in Phase 1.4.1. Repository code performed redundant `Math.Round(* 100m)` conversions rather than delegating to `Money.ToMinorUnits()` / `Money.FromMinorUnits()`.
- **Existing Implementation**:
  - `Ledger.OpeningBalance` was typed as `decimal`.
  - `Ledger.CreditLimit` was typed as `decimal?`.
  - `LedgerRepository.cs` contained ad-hoc `Convert.ToInt64(Math.Round(ledger.OpeningBalance * 100m, MidpointRounding.AwayFromZero))` calls.
- **Correction**:
  - Changed `Ledger.OpeningBalance` property to `Money`.
  - Changed `Ledger.CreditLimit` property to `Money?`.
  - Updated `Ledger` constructors and `Ledger.Create` to accept `Money` and `Money?`.
  - Added implicit conversion from `decimal` to `Money` in `Money.cs` for zero-friction numeric literal consumption while preserving type safety.
  - Refactored `LedgerRepository.cs` to exclusively use `ledger.OpeningBalance.ToMinorUnits()` for parameter binding and `Money.FromMinorUnits(reader.GetInt64(...))` for reader hydration.
  - In `LedgerService.cs`, DTO mapping explicitly extracts `.Amount` from `Money` value objects.
- **Architectural Rationale**: Prevents fragmented representation and eliminates duplicate arithmetic conversion logic. Minor units (`paise`) in SQLite database are converted cleanly at the repository boundary into immutable `Money` value objects with zero floating-point drift.
- **Tests Added / Updated**:
  - `LedgerTests.cs`: Verified `OpeningBalance` and `CreditLimit` are `Money` structs holding exact amounts.
  - `DatabaseConstraintIntegrationTests.cs`: Verified round-trip persistence of exact paise to `Money`.
  - `LedgerRepositoryTests.cs`: Verified hydration of `Money` fields from database rows.

---

### Remediation 2: Transaction-Safe Ledger Deletion
- **Finding**: Ensure deletion guards in `LedgerService.DeleteLedgerAsync` are rock-solid, reject deletion of system ledgers or ledgers with transactional history, and ensure queries are defensively scoped by `CompanyId`.
- **Existing Implementation**: `LedgerService.DeleteLedgerAsync` already validated `IsDefaultSystemLedger`, `HasTransactionsAsync`, and `HasBillReferencesAsync`.
- **Correction**:
  - Maintained application-layer deletion guards in `LedgerService.DeleteLedgerAsync`.
  - Verified `HasTransactionsAsync` SQL query in `LedgerRepository.cs` enforces `CompanyId = @CompanyId AND LedgerId = @LedgerId` for defense in depth.
- **Architectural Rationale**: In accounting systems, deleting a ledger with active journal postings or bill allocations corrupts trial balance and subledger audit trails. Application-layer guards ensure friendly domain exception messages, while database foreign key constraints provide backing safety.
- **Tests Added / Updated**:
  - `LedgerServiceTests.cs`: Added tests confirming rejection for system ledgers, rejection for ledgers with transactions, rejection for ledgers with bill references, and clean deletion of unreferenced user ledgers.

---

### Remediation 3: AccountGroup Reparent Precondition & Fail-Loud Ancestor Validation
- **Finding**: In `AccountGroup.Reparent`, during cycle detection traversal, if an ancestor `ParentGroupId` was not present in the supplied `companyGroups` collection, the loop previously broke silently (`break`), potentially masking corrupted or incomplete hierarchy input.
- **Existing Implementation**:
  ```csharp
  if (!groupDict.TryGetValue(currentCheckId.Value, out var ancestorGroup))
      break; // Silently stopped traversing
  ```
- **Correction**:
  - Replaced silent `break` with an explicit `DomainException`:
  ```csharp
  if (!groupDict.TryGetValue(currentCheckId.Value, out var ancestorGroup))
  {
      throw new DomainException(
          $"Incomplete account group hierarchy supplied. Ancestor group with ID '{currentCheckId.Value}' was not found in the company groups collection.");
  }
  ```
- **Architectural Rationale**: Fail-loud domain invariant enforcement prevents partial graph traversal from silently approving circular references when callers pass an incomplete group set.
- **Tests Added / Updated**:
  - `AccountGroupTests.cs`: `Reparent_WhenAncestorNotFoundInCompanyGroups_ThrowsDomainException` verifying that missing ancestors throw immediately.

---

### Remediation 4: AccountGroup Nature Consistency & Subtree Protection (ADR 06)
- **Finding**: Changing an `AccountGroup`'s `Nature` (Asset, Liability, Capital, Income, Expense) when the group has child sub-groups risks creating inconsistent balance sheet classifications if children remain under a mutated parent.
- **Existing Implementation**: `AccountGroup.Reparent` and `AccountGroup.Update` allowed nature mutation on groups with descendants.
- **Correction**:
  - Added subtree check: If a group has child sub-groups (`companyGroups.Any(g => g.ParentGroupId == Id)`), any operation that would change its `Nature` (either directly via `Update` or via `Reparent` to a parent of differing `Nature`) throws a `DomainException`.
  - Child sub-groups must be relocated first before changing a parent group's classification.
  - Documented as **ADR 06: Account Group Nature Consistency & Subtree Protection** in `docs/DECISION_LOG.md` and explicitly labeled as an *Architectural Recommendation* distinct from legacy source systems.
- **Architectural Rationale**: Financial integrity requires strict hierarchical purity for tree-based financial statements (Balance Sheet, Profit & Loss). Mutating a branch nature without restructuring sub-trees produces invalid ledger classifications.
- **Tests Added / Updated**:
  - `AccountGroupTests.cs`:
    - `Reparent_WhenTargetParentHasDifferentNatureAndGroupHasDescendants_ThrowsDomainException`
    - `Reparent_WhenLeafGroupReparentedToDifferentNature_SucceedsAndDerivesParentNature`
    - `Update_WhenRootGroupChangesNatureWithDescendants_ThrowsDomainException`
    - `Update_WhenLeafRootGroupChangesNature_Succeeds`

---

## 3. Build & Test Verification Results

### Build Verification
- Command: `dotnet build BillsEasy.sln -c Release`
- Warnings: `0`
- Errors: `0`
- Status: **PASSED**

### Test Suite Execution
- Command: `dotnet test BillsEasy.sln -c Release`
- Unit Tests (`BillsEasy.UnitTests`): `99 Passed`, `0 Failed`, `0 Skipped`
- Integration Tests (`BillsEasy.IntegrationTests`): `25 Passed`, `0 Failed`, `0 Skipped`
- Total Tests: **124 Passed** (100% Success Rate)
- Status: **PASSED**

---

## 4. Architectural Boundaries & Phase 1.5 Readiness

- **Current State**: Phase 1.1, 1.2, 1.3, 1.4, 1.4.1 (UI Foundation), and 1.4.2 (Remediation Hardening) are 100% completed, tested, and documented.
- **Strict Stop Condition Maintained**:
  - No Voucher entities, VoucherLine entities, or posting engine code created.
  - No premature Phase 1.5 implementations started.
  - The codebase is now in an optimal, clean, hardened state ready for authorized kickoff of **Phase 1.5: Voucher Engine & Double-Entry Posting Engine**.
