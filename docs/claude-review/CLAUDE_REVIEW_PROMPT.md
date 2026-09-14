# CLAUDE ARCHITECTURE & DOMAIN REVIEW PROMPT

> **Instructions for the Reviewer:**
> You are acting as an independent Principal Software Architect and Lead Accounting Domain Specialist.
> Your task is to perform an exhaustive, critical, and unsparing review of the **BillsEasy** accounting software foundation (Phases 1.1 through 1.4) before development is authorized to proceed to Phase 1.5 (Voucher Engine).

---

## 📋 Context & Project Identity
- **Project:** BillsEasy (Native Windows Desktop Accounting Application for Indian SMEs).
- **Core Stack:** C# (.NET 8.0 LTS), WPF (MVVM with CommunityToolkit.Mvvm), Embedded SQLite (WAL mode, Foreign Keys enabled), Clean Architecture.
- **Current Milestone:** Completed Phase 1.1 (Core Foundation), Phase 1.2 (Company & Financial Year), Phase 1.3 (Account Groups & Hierarchy), and Phase 1.4 (Ledger / Account Master).
- **Review Artifacts:** All review documents are located in `docs/claude-review/`:
  - `01_EXECUTIVE_SUMMARY.md`
  - `02_CURRENT_ARCHITECTURE.md`
  - `03_DOMAIN_MODEL.md`
  - `04_DATABASE_SCHEMA.md`
  - `05_COMPANY_FINANCIAL_YEAR.md`
  - `06_ACCOUNT_GROUPS.md`
  - `07_LEDGER_MODEL.md`
  - `08_AUDIT_USER_MODEL.md`
  - `09_SQLITE_DESKTOP_ARCHITECTURE.md`
  - `10_WPF_MVVM_ARCHITECTURE.md`
  - `11_TESTING_STATUS.md`
  - `12_VOUCHER_ENGINE_READINESS.md`
  - `13_RISKS_AND_GAPS.md`
  - `14_ARCHITECTURAL_DECISIONS.md`
  - `15_REVIEW_QUESTIONS_FOR_CLAUDE.md`
  - `DATABASE_SCHEMA.sql`
  - `CURRENT_PROJECT_TREE.txt`

---

## 🎯 Review Objectives
Please read all the review documents and provide an independent architectural verdict.

In your review, please address:
1. **Core Accounting Domain Modeling:**
   - Verify that treating Customers and Suppliers purely as `Ledger` instances under `Sundry Debtors` / `Sundry Creditors` (without a separate `Party` table) is sound and adheres to established Indian accounting principles (BUSY/Tally).
   - Evaluate the setup `OpeningBalance` on `Ledger` vs transaction-derived balances.
2. **Relational Schema & Database Design:**
   - Critique the SQLite schema in `DATABASE_SCHEMA.sql`, foreign keys, composite unique indexes (`COLLATE NOCASE`), and delete restrictions.
   - Evaluate currency precision (`REAL` vs `INTEGER` minor units / paise).
3. **Clean Architecture & Code Separation:**
   - Verify strict separation of Domain, Application, Infrastructure, and WPF Presentation.
4. **Voucher Engine Readiness (Phase 1.5):**
   - Identify any missing domain fields, aggregate boundary conflicts, or locking risks that must be fixed before Phase 1.5 implementation starts.

---

## 📊 Required Verdict Format

Please conclude your review with one of three formal verdicts:

- **[A] GO** — The architecture and domain model are fully sound, verified, and ready for Phase 1.5 Voucher Engine.
- **[B] GO WITH REQUIRED CHANGES** — Minor adjustments or constraints required before proceeding to Phase 1.5 (specify exact changes).
- **[C] NO-GO** — Critical architectural or domain defects identified; require refactoring before proceeding.

Along with a prioritized action list categorized as:
- **Blockers (Must fix before Phase 1.5)**
- **Recommendations for Phase 1.5+**
- **Future Considerations (PostgreSQL / LAN edition)**
