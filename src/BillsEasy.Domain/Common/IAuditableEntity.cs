using System;

namespace BillsEasy.Domain.Common;

/// <summary>
/// Foundational contract for entities that capture user attribution and audit timestamps.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string CreatedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
    string? ModifiedBy { get; set; }
}
