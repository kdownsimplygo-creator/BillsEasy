namespace BillsEasy.Domain.Enums;

/// <summary>
/// Lifecycle status of a commercial bill reference.
/// </summary>
public enum BillReferenceStatus
{
    /// <summary>
    /// Active commercial claim subject to pending settlement.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Cancelled along with its source voucher; excluded from pending settlement queries.
    /// </summary>
    Cancelled = 2
}
