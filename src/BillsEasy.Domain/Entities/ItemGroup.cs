using System;
using BillsEasy.Domain.Common;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Domain entity representing an Item Group / Category in the inventory organization hierarchy.
/// </summary>
public class ItemGroup : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentGroupId { get; private set; }
    public string? Description { get; private set; }
    public bool IsDefaultSystemGroup { get; private set; }

    // Parameterless constructor for persistence/ADO.NET hydration
    private ItemGroup() : base()
    {
    }

    public ItemGroup(
        Guid id,
        Guid companyId,
        string name,
        Guid? parentGroupId = null,
        string? description = null,
        bool isDefaultSystemGroup = false) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        ValidateAndSetName(name);

        if (parentGroupId.HasValue && parentGroupId.Value == id)
            throw new DomainException("An item group cannot be its own parent.");

        CompanyId = companyId;
        ParentGroupId = parentGroupId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsDefaultSystemGroup = isDefaultSystemGroup;
    }

    public static ItemGroup Create(
        Guid companyId,
        string name,
        Guid? parentGroupId = null,
        string? description = null,
        bool isDefaultSystemGroup = false)
    {
        return new ItemGroup(Guid.NewGuid(), companyId, name, parentGroupId, description, isDefaultSystemGroup);
    }

    public void Update(string name, Guid? parentGroupId, string? description)
    {
        ValidateAndSetName(name);

        if (parentGroupId.HasValue && parentGroupId.Value == Id)
            throw new DomainException("An item group cannot be its own parent.");

        ParentGroupId = parentGroupId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private void ValidateAndSetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Item group name is required.");

        Name = name.Trim();
    }
}
