namespace BillsEasy.Domain.Enums;

/// <summary>
/// Specifies the type of a bill reference.
/// </summary>
public enum BillReferenceType
{
    /// <summary>
    /// Standard commercial invoice or receivable/payable claim.
    /// </summary>
    NewRef = 1,

    /// <summary>
    /// Outstanding opening balance bill brought forward from prior accounting periods.
    /// </summary>
    OpeningRef = 2
}
