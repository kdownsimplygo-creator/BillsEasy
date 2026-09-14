using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Master Data aggregate entity representing an Account Group in the Chart of Accounts hierarchy.
/// </summary>
public class AccountGroup : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentGroupId { get; private set; }
    public GroupNature Nature { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsDefaultSystemGroup { get; private set; }

    // Parameterless constructor for persistence/ORM hydration
    private AccountGroup() : base()
    {
    }

    public AccountGroup(
        Guid id,
        Guid companyId,
        string name,
        GroupNature nature,
        Guid? parentGroupId = null,
        bool isPrimary = false,
        bool isDefaultSystemGroup = false) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        ValidateAndSetName(name);

        if (!Enum.IsDefined(typeof(GroupNature), nature))
            throw new DomainException($"Invalid group nature '{nature}'.");

        if (parentGroupId.HasValue && parentGroupId.Value == id)
            throw new DomainException("An account group cannot be its own parent.");

        CompanyId = companyId;
        Name = name.Trim();
        Nature = nature;
        ParentGroupId = parentGroupId;
        IsPrimary = isPrimary && !parentGroupId.HasValue;
        IsDefaultSystemGroup = isDefaultSystemGroup;
    }

    public static AccountGroup Create(
        Guid companyId,
        string name,
        GroupNature nature,
        Guid? parentGroupId = null,
        bool isPrimary = false,
        bool isDefaultSystemGroup = false)
    {
        return new AccountGroup(
            Guid.NewGuid(),
            companyId,
            name,
            nature,
            parentGroupId,
            isPrimary,
            isDefaultSystemGroup);
    }

    public void Reparent(AccountGroup? newParent, IEnumerable<AccountGroup> companyGroups)
    {
        if (companyGroups == null)
            throw new ArgumentNullException(nameof(companyGroups), "Company groups collection is required for hierarchy verification.");

        if (newParent != null)
        {
            if (newParent.CompanyId != CompanyId)
                throw new DomainException("Parent account group must belong to the same company.");

            if (newParent.Id == Id)
                throw new DomainException("An account group cannot be its own parent.");

            if (IsDefaultSystemGroup && newParent.Nature != Nature)
                throw new DomainException($"Cannot change the nature of protected system group '{Name}'.");

            if (newParent.Nature != Nature && companyGroups.Any(g => g.ParentGroupId == Id))
                throw new DomainException($"Cannot change the nature of account group '{Name}' to '{newParent.Nature}' because it has child sub-groups. Move child groups first.");

            // Cycle detection traversal using the required companyGroups hierarchy
            var groupDict = new Dictionary<Guid, AccountGroup>();
            foreach (var g in companyGroups)
            {
                groupDict[g.Id] = g;
            }

            var currentCheckId = (Guid?)newParent.Id;
            while (currentCheckId.HasValue)
            {
                if (currentCheckId.Value == Id)
                {
                    throw new DomainException($"Cannot set parent: circular hierarchy detected. Group '{Name}' is an ancestor of the target parent group '{newParent.Name}'.");
                }

                if (groupDict.TryGetValue(currentCheckId.Value, out var ancestor))
                {
                    currentCheckId = ancestor.ParentGroupId;
                }
                else
                {
                    throw new DomainException($"Incomplete account group hierarchy supplied. Ancestor group with ID '{currentCheckId.Value}' was not found in the company groups collection.");
                }
            }

            ParentGroupId = newParent.Id;
            Nature = newParent.Nature;
            IsPrimary = false;
        }
        else
        {
            ParentGroupId = null;
            IsPrimary = true;
        }
    }

    public void Update(string name, AccountGroup? newParent, IEnumerable<AccountGroup> companyGroups, GroupNature? rootNature = null)
    {
        ValidateAndSetName(name);

        if (newParent == null && rootNature.HasValue && rootNature.Value != Nature)
        {
            if (IsDefaultSystemGroup)
                throw new DomainException($"Cannot change the nature of protected system group '{Name}'.");

            if (!Enum.IsDefined(typeof(GroupNature), rootNature.Value))
                throw new DomainException($"Invalid group nature '{rootNature.Value}'.");

            if (companyGroups.Any(g => g.ParentGroupId == Id))
                throw new DomainException($"Cannot change the nature of account group '{Name}' because it has child sub-groups. Modify or move child groups first.");

            Nature = rootNature.Value;
        }

        Reparent(newParent, companyGroups);

        Name = name.Trim();
    }

    public void SetPrimary(bool isPrimary)
    {
        if (isPrimary && ParentGroupId.HasValue)
            throw new DomainException("A sub-group with a parent cannot be designated as a Primary Root group.");

        IsPrimary = isPrimary;
    }

    private static void ValidateAndSetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Account group name cannot be empty.");

        if (name.Trim().Length > 100)
            throw new DomainException("Account group name cannot exceed 100 characters.");
    }
}