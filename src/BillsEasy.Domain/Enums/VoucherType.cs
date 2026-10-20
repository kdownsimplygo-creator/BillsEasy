namespace BillsEasy.Domain.Enums;

/// <summary>
/// Identifies the accounting classification and workflow purpose of a voucher.
/// </summary>
public enum VoucherType
{
    Journal = 1,
    Payment = 2,
    Receipt = 3,
    Contra = 4,
    Sales = 5,
    Purchase = 6,
    DebitNote = 7,
    CreditNote = 8
}
