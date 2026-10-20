namespace BillsEasy.Domain.Enums;

/// <summary>
/// Status of a commercial purchase invoice.
/// Class 8 demonstrates immediate saved/active behavior; cancellation is supported for integrity.
/// </summary>
public enum PurchaseInvoiceStatus
{
    Active = 1,
    Cancelled = 2
}
