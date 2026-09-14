namespace BillsEasy.Domain.Enums;

/// <summary>
/// Defines the fundamental accounting nature/classification of an Account Group.
/// </summary>
public enum GroupNature
{
    Asset = 1,
    Liability = 2,
    Capital = 3,
    Income = 4,
    Expense = 5
}