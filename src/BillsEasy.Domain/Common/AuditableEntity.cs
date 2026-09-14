using System;

namespace BillsEasy.Domain.Common;

/// <summary>
/// Base entity that encapsulates identity and standard audit metadata.
/// </summary>
/// <typeparam name=""TId"">The identifier type.</typeparam>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity
    where TId : notnull
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    protected AuditableEntity() : base()
    {
    }

    protected AuditableEntity(TId id) : base(id)
    {
    }
}
