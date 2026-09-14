# 08. Audit & User Attribution Model Review

## 1. Current Audit Infrastructure
All domain aggregates (`Company`, `FinancialYear`, `AccountGroup`, `Ledger`) inherit from:
```csharp
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    public void UpdateAudit(string modifiedBy, DateTime modifiedAt)
    {
        ModifiedBy = modifiedBy;
        ModifiedAt = modifiedAt;
    }
}
```

- **User Resolution:** `ICurrentUserService` abstraction resolves the acting user.
- **Timestamp Resolution:** `IDateTimeProvider` abstraction resolves timestamps in UTC (`DateTime.UtcNow`).
- **Desktop Default:** `StandaloneCurrentUserService` returns `"system_admin"` for single-user desktop mode.

---

## 2. Readiness for Accounting Transaction Auditability (Phase 1.5+)
Accounting legislation in India (e.g. MCA Audit Trail / Edit Log rules for Indian companies) requires an immutable record of every transaction change.

### How Current Model Scales to Voucher Engine:
1. **Vouchers:** Will inherit `AuditableEntity<Guid>`, capturing who created the voucher and who modified/cancelled it.
2. **Cancellation vs Deletion:** Vouchers in double-entry accounting should ideally be **Cancelled** (retaining full audit history) rather than hard deleted.
3. **Multi-User / LAN Readiness:** When multi-user mode is introduced, swapping `ICurrentUserService` for a session/login-backed implementation provides user attribution across all layers with **zero changes to Domain entities**.
