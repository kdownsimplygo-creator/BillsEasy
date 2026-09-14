# 14. Architectural Decisions (Locked Decisions Record)

The following architectural decisions have been locked and MUST NOT change without formal re-authorization:

1. **WPF on .NET 8.0:** Native Windows desktop user experience, high-density form rendering, and keyboard productivity over web-based or WinUI 3 wrappers.
2. **SQLite (Phase 1) & PostgreSQL (Future Multi-User):** Embedded SQLite with WAL mode and strict foreign keys for single-PC desktop edition. PostgreSQL reserved for client-server LAN edition.
3. **Clean Architecture / Layered Monolith:** Strict one-way dependency rule (`Presentation -> Application -> Domain <- Infrastructure`).
4. **MVVM with `CommunityToolkit.Mvvm`:** Pure ViewModels, zero code-behind logic.
5. **Unified Ledger Model (No Party Table):** Customers and Suppliers are represented solely as `Ledger` instances under `Sundry Debtors` and `Sundry Creditors`.
6. **No Separate `AccountingEntry` Table:** All transactions will be recorded via `Voucher` and `VoucherLine`.
7. **Zero Cached Balances:** No `CurrentBalance`, `ClosingBalance`, or cached balance columns in the database. All ledger balances in reports are dynamically derived from transaction lines.
8. **Master Setup Opening Balance:** `OpeningBalance` on `Ledger` is setup information only.
9. **User Attribution from Day 1:** All entities inherit from `AuditableEntity<TId>` (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`).
10. **Case-Insensitive Unique Constraints:** All name uniqueness constraints (`UQ_Companies_Name`, `UQ_AccountGroups_Company_Parent_Name`, `UQ_Ledgers_Company_Name`) use `COLLATE NOCASE`.
