using System;
using BillsEasy.Domain.Common;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Domain entity representing a Unit of Measurement (e.g., Pcs, Box, Kg, Mtr) for inventory items.
/// </summary>
public class Unit : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string PrintName { get; private set; } = string.Empty;
    public int DecimalPlaces { get; private set; }

    // Parameterless constructor for persistence/ADO.NET hydration
    private Unit() : base()
    {
    }

    public Unit(
        Guid id,
        Guid companyId,
        string name,
        string? printName = null,
        int decimalPlaces = 0) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        ValidateAndSetName(name);
        ValidateAndSetPrintName(printName);
        ValidateAndSetDecimalPlaces(decimalPlaces);

        CompanyId = companyId;
    }

    public static Unit Create(
        Guid companyId,
        string name,
        string? printName = null,
        int decimalPlaces = 0)
    {
        return new Unit(Guid.NewGuid(), companyId, name, printName, decimalPlaces);
    }

    public void Update(string name, string? printName, int decimalPlaces)
    {
        ValidateAndSetName(name);
        ValidateAndSetPrintName(printName);
        ValidateAndSetDecimalPlaces(decimalPlaces);
    }

    private void ValidateAndSetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Unit name is required.");

        Name = name.Trim();
    }

    private void ValidateAndSetPrintName(string? printName)
    {
        if (string.IsNullOrWhiteSpace(printName))
        {
            PrintName = Name;
        }
        else
        {
            PrintName = printName.Trim();
        }
    }

    private void ValidateAndSetDecimalPlaces(int decimalPlaces)
    {
        if (decimalPlaces < 0 || decimalPlaces > 4)
            throw new DomainException("Decimal places must be between 0 and 4.");

        DecimalPlaces = decimalPlaces;
    }
}
