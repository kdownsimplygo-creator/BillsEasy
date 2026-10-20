namespace BillsEasy.Domain.Enums;

/// <summary>
/// Represents the lifecycle state of an accounting voucher.
/// </summary>
public enum VoucherStatus
{
    Draft = 1,
    Posted = 2,
    Cancelled = 3
}
