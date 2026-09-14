# BillsEasy — Security & User Attribution Specification

---

## 1. Security Philosophy & Phase 1 Scope

In Phase 1 (Single-PC standalone desktop application), complex multi-tenant cloud IAM or LDAP systems are unnecessary. However, **every accounting entity and database table is designed with user attribution from Day 1** to guarantee that multi-user permissions, passwords, and operator attribution can be activated without schema redesign.

---

## 2. User Attribution Schema Standard

All persistent business entities implement the IAuditableEntity interface:

`csharp
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string CreatedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
    string? ModifiedBy { get; set; }
}
`

### Attribution Properties
1. **CreatedAt (DateTime UTC):** Exact UTC timestamp when the record was inserted.
2. **CreatedBy (string):** User ID or Login Name of the operator who created the record (defaults to \"admin\" or current Windows username in Phase 1 standalone mode).
3. **ModifiedAt (DateTime? UTC):** Nullable timestamp of the most recent modification.
4. **ModifiedBy (string?):** User ID or Login Name of the operator who performed the modification.

---

## 3. Runtime Security Context Service

The application injects ICurrentUserService into application handlers:

`csharp
public interface ICurrentUserService
{
    string UserId { get; }
    string UserName { get; }
    string WorkstationName { get; }
    bool IsAdmin { get; }
}
`

In Phase 1, StandaloneCurrentUserService resolves the current workstation name (Environment.MachineName) and operator username. When user authentication is toggled on during company configuration (Source-derived from BUSY Class 1 Company Security), the active logged-in user credentials populate this context.

---

## 4. Future LAN / Multi-User Security Extensibility (Phase 2+)

* **Role-Based Access Control (RBAC):** Prepared for roles such as Administrator, Accountant, Sales Operator, and Audit Viewer.
* **Voucher-Level Restrictions:** Back-dated voucher entry restrictions, maximum discount authorization limits, and view-only permissions for financial statements.
