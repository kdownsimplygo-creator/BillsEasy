# 13. Risks & Hidden Gaps Analysis

## 1. Architectural Risks & Mitigations

| Risk / Gap Identified | Severity | Potential Impact | Recommended Architectural Posture |
|---|---|---|---|
| **SQLite Real vs Decimal Precision** | **MEDIUM** | SQLite stores `Money.Amount` as `REAL` (IEEE 754 float). In extreme calculations, binary floating point representation could introduce fractional cent drift. | In ADO.NET queries, keep `Money.Amount` cast/rounded or store as `INTEGER` (in paise/cents) or formatted text if exact decimal fidelity is required in SQLite. In .NET domain, `Money` uses `decimal` which is exact. |
| **Opening Balance Transition between FYs** | **MEDIUM** | If initial opening balance is kept on `Ledger`, cross-year transitions must not overwrite this master inception balance. | Treat `Ledger.OpeningBalance` strictly as inception opening balance at `BooksBeginningFrom`. All subsequent FY opening balances must be derived dynamically from prior vouchers or explicit opening vouchers. |
| **Voucher Number Concurrency** | **LOW-MEDIUM** | In multi-user / LAN scenarios, concurrent voucher creation could attempt duplicate voucher numbering. | Implement numbering generation inside a serializable transaction or sequential numbering generator table. |
| **Database Migrations** | **LOW** | Currently uses `DatabaseInitializer` with `CREATE TABLE IF NOT EXISTS`. Future schema alterations in production will require a structured migration mechanism (e.g. DbUp or FluentMigrator). | Keep raw DDL for Phase 1, introduce versioned migration runner before v1.0 public release. |

---

## 2. Code Quality & Maintainability Classification

- **CRITICAL:** None. (Clean Architecture boundaries strictly honored; 0 build errors/warnings; 99/99 tests pass).
- **HIGH:** None.
- **MEDIUM:** Monitor SQLite `REAL` storage for currency as transaction volume scales.
- **LOW:** Enhance desktop keyboard focus hooks in WPF forms during Phase 1.7 UI integration.
- **NICE-TO-HAVE:** Add audit trail history viewer dialog in Phase 2.
