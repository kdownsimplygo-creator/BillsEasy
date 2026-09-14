# BillsEasy — Audit Trail & Edit Log Architecture Specification

**Mandate:** Prepare the architecture for unalterable accounting traceability (incorporating Indian MCA Audit Trail / Edit Log standards for corporate accounts).

---

## 1. Core Audit Questions

The audit trail engine is designed to answer with cryptographic certainty:
1. **Who created this transaction or master?** (UserId, Workstation)
2. **Who modified or reversed it?** (UserId, Timestamp)
3. **When did the event occur?** (Timestamp in UTC and Local Indian Standard Time)
4. **What exact fields changed?** (Field-level delta)
5. **What was the previous state (Old Value)?** (Serialized JSON snapshot)
6. **What is the current state (New Value)?** (Serialized JSON snapshot)

---

## 2. Audit Engine Design

### 2.1 Audit Event Interception
Within the Unit of Work / Repository persistence pipeline, before any entity modification or soft-deletion is committed:
1. The pre-change entity state is captured as JSON.
2. The post-change entity state is captured as JSON.
3. An AuditLog entry is generated and queued into the same atomic transaction.

`csharp
public class AuditLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string EntityType { get; set; } = string.Empty; // 'Voucher', 'Ledger', etc.
    public string EntityId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // 'Create', 'Modify', 'Reverse'
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string? Workstation { get; set; }
}
`

### 2.2 Immutability Guarantee
* The AuditLogs table has no UPDATE or DELETE methods exposed in the application repository.
* In SQLite, triggers can be activated to reject any UPDATE or DELETE statements executed directly on the AuditLogs table.

---

## 3. Statutory & Practical Considerations

* In India, MCA (Ministry of Corporate Affairs) mandates that companies using accounting software must maintain an audit trail of each and every transaction, creating an edit log of each change made in books of account along with the date and user, and ensuring the audit trail cannot be disabled.
* The BillsEasy audit schema is built directly into the database architecture from Phase 1 so that full statutory compliance can be activated seamlessly.
