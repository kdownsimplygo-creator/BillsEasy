# 09. SQLite Desktop Architecture & PostgreSQL Migration Review

## 1. Current SQLite Configuration
- **Connection Factory:** `SqliteConnectionFactory` implements `ISqliteDbConnectionFactory`.
- **Engine PRAGMAs Applied on Every Connection:**
  ```sql
  PRAGMA journal_mode = WAL;
  PRAGMA foreign_keys = ON;
  PRAGMA busy_timeout = 5000;
  PRAGMA synchronous = NORMAL;
  ```
- **Connection Lifetime:** Short-lived per repository operation (created, opened, and disposed asynchronously via `await using`).
- **Concurrency:** Write-Ahead Logging (WAL) allows concurrent readers alongside one writer without database locking deadlocks.

---

## 2. PostgreSQL / LAN Edition Portability Analysis
The project charter explicitly states:
- **Phase 1:** Single-PC native desktop with SQLite.
- **Future:** PostgreSQL for LAN / Multi-user client-server edition.

### Portability Checklist:
1. **Repository Abstractions:** All data access is mediated through Application layer interfaces (`ILedgerRepository`, `IAccountGroupRepository`, `ICompanyRepository`, `IFinancialYearRepository`).
2. **SQL Dialect Compatibility:**
   - Parameterization uses standard `@Param` syntax supported by both SQLite and Npgsql (PostgreSQL).
   - Primary keys are standard `Guid` represented as strings (`TEXT` in SQLite, maps cleanly to `UUID` in PostgreSQL).
   - Dates are stored as ISO-8601 strings (`TEXT`), easily mapped to `timestamptz`.
   - Money is stored as `REAL` / `NUMERIC(18,2)`.
3. **No SQLite Proprietary Locks:** No reliance on SQLite internal rowids or non-standard SQL extensions.
4. **Future Migration Effort:** Swapping SQLite for PostgreSQL requires only implementing a `PostgreSqlConnectionFactory` and corresponding repository implementations in a new `BillsEasy.Infrastructure.PostgreSql` assembly without touching Domain or Application layers.
