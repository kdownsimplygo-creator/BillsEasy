using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.Services;

public class AccountGroupService : IAccountGroupService
{
    private readonly IAccountGroupRepository _accountGroupRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AccountGroupService(
        IAccountGroupRepository accountGroupRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _accountGroupRepository = accountGroupRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AccountGroupDto> CreateGroupAsync(CreateAccountGroupDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Account group name cannot be empty.");

        var trimmedName = dto.Name.Trim();
        if (await _accountGroupRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.ParentGroupId, null, cancellationToken))
            throw new DomainException($"An account group with name '{trimmedName}' already exists under the selected parent group.");

        GroupNature nature;
        bool isPrimary = false;

        if (dto.ParentGroupId.HasValue)
        {
            var parent = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.ParentGroupId.Value, cancellationToken);
            if (parent == null)
                throw new DomainException($"Parent account group with ID '{dto.ParentGroupId.Value}' not found.");

            if (parent.CompanyId != dto.CompanyId)
                throw new DomainException("Parent account group must belong to the same company.");

            nature = parent.Nature;
        }
        else
        {
            if (!dto.Nature.HasValue)
                throw new DomainException("Group Nature is required when creating a primary root group.");

            nature = dto.Nature.Value;
            isPrimary = true;
        }

        var group = AccountGroup.Create(
            dto.CompanyId,
            trimmedName,
            nature,
            dto.ParentGroupId,
            isPrimary,
            isDefaultSystemGroup: false);

        group.CreatedAt = _dateTimeProvider.UtcNow;
        group.CreatedBy = _currentUserService.UserId;

        await _accountGroupRepository.AddAsync(group, cancellationToken);

        return await MapToDtoAsync(group, cancellationToken);
    }

    public async Task<AccountGroupDto> UpdateGroupAsync(UpdateAccountGroupDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Account group name cannot be empty.");

        var group = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.Id, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{dto.Id}' not found.");

        var trimmedName = dto.Name.Trim();
        if (await _accountGroupRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.ParentGroupId, dto.Id, cancellationToken))
            throw new DomainException($"Another account group with name '{trimmedName}' already exists under the selected parent group.");

        var allGroups = await _accountGroupRepository.GetAllAsync(dto.CompanyId, cancellationToken);
        AccountGroup? parentGroup = null;
        if (dto.ParentGroupId.HasValue)
        {
            parentGroup = allGroups.FirstOrDefault(g => g.Id == dto.ParentGroupId.Value)
                       ?? await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.ParentGroupId.Value, cancellationToken);
            if (parentGroup == null)
                throw new DomainException($"Parent account group with ID '{dto.ParentGroupId.Value}' not found.");
        }

        group.Update(trimmedName, parentGroup, allGroups, dto.Nature);
        group.ModifiedAt = _dateTimeProvider.UtcNow;
        group.ModifiedBy = _currentUserService.UserId;

        await _accountGroupRepository.UpdateAsync(group, cancellationToken);

        return await MapToDtoAsync(group, cancellationToken);
    }

    public async Task DeleteGroupAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _accountGroupRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{id}' not found.");

        if (group.IsDefaultSystemGroup)
            throw new DomainException($"System predefined account group '{group.Name}' cannot be deleted.");

        if (await _accountGroupRepository.HasChildrenAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete account group '{group.Name}' because it has one or more child sub-groups.");

        if (await _accountGroupRepository.HasLedgersAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete account group '{group.Name}' because it has one or more ledgers assigned to it.");

        await _accountGroupRepository.DeleteAsync(companyId, id, cancellationToken);
    }

    public async Task<AccountGroupDto?> GetGroupByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _accountGroupRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (group == null) return null;

        return await MapToDtoAsync(group, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountGroupDto>> GetAllGroupsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        return groups.Select(g => MapToDto(g, groupDict)).OrderBy(g => g.Nature).ThenBy(g => g.Name).ToList();
    }

    public async Task<IReadOnlyList<AccountGroupNodeDto>> GetGroupTreeAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        var lookup = groups.ToLookup(g => g.ParentGroupId);

        List<AccountGroupNodeDto> BuildNodes(Guid? parentId, int level)
        {
            var list = new List<AccountGroupNodeDto>();
            foreach (var g in lookup[parentId].OrderBy(x => x.Name))
            {
                var node = new AccountGroupNodeDto
                {
                    Id = g.Id,
                    CompanyId = g.CompanyId,
                    Name = g.Name,
                    ParentGroupId = g.ParentGroupId,
                    ParentGroupName = g.ParentGroupId.HasValue && groupDict.TryGetValue(g.ParentGroupId.Value, out var parent) ? parent.Name : null,
                    Nature = g.Nature,
                    NatureName = g.Nature.ToString(),
                    IsPrimary = g.IsPrimary,
                    IsDefaultSystemGroup = g.IsDefaultSystemGroup,
                    Level = level,
                    Children = BuildNodes(g.Id, level + 1)
                };
                list.Add(node);
            }
            return list;
        }

        return BuildNodes(null, 0);
    }

    public async Task<IReadOnlyList<AccountGroupLookupDto>> GetLookupGroupsAsync(Guid companyId, Guid? excludeSubtreeOfGroupId = null, CancellationToken cancellationToken = default)
    {
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);

        HashSet<Guid> excludedIds = new();
        if (excludeSubtreeOfGroupId.HasValue)
        {
            excludedIds.Add(excludeSubtreeOfGroupId.Value);
            var lookup = groups.ToLookup(g => g.ParentGroupId);

            void CollectDescendants(Guid parentId)
            {
                foreach (var child in lookup[parentId])
                {
                    if (excludedIds.Add(child.Id))
                    {
                        CollectDescendants(child.Id);
                    }
                }
            }

            CollectDescendants(excludeSubtreeOfGroupId.Value);
        }

        return groups
            .Where(g => !excludedIds.Contains(g.Id))
            .OrderBy(g => g.Nature)
            .ThenBy(g => g.Name)
            .Select(g => new AccountGroupLookupDto(
                g.Id,
                g.Name,
                g.Nature,
                g.Nature.ToString(),
                g.ParentGroupId,
                g.IsDefaultSystemGroup))
            .ToList();
    }

    public async Task SeedDefaultGroupsAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default)
    {
        var existing = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        if (existing.Count > 0)
        {
            return; // Already seeded
        }

        var now = _dateTimeProvider.UtcNow;
        var seededGroups = new List<AccountGroup>();

        AccountGroup CreateSystemGroup(string name, GroupNature nature, Guid? parentId = null, bool isPrimary = false)
        {
            var grp = AccountGroup.Create(
                companyId,
                name,
                nature,
                parentId,
                isPrimary: isPrimary,
                isDefaultSystemGroup: true);

            grp.CreatedAt = now;
            grp.CreatedBy = createdBy;
            seededGroups.Add(grp);
            return grp;
        }

        // 1. Primary Roots
        var capPrimary = CreateSystemGroup("Capital Account", GroupNature.Capital, isPrimary: true);
        var liabPrimary = CreateSystemGroup("Liabilities", GroupNature.Liability, isPrimary: true);
        var assetPrimary = CreateSystemGroup("Assets", GroupNature.Asset, isPrimary: true);
        var incPrimary = CreateSystemGroup("Income", GroupNature.Income, isPrimary: true);
        var expPrimary = CreateSystemGroup("Expenses", GroupNature.Expense, isPrimary: true);

        // 2. Capital Subgroups
        CreateSystemGroup("Reserves & Surplus", GroupNature.Capital, capPrimary.Id);

        // 3. Liabilities Subgroups
        var currLiab = CreateSystemGroup("Current Liabilities", GroupNature.Liability, liabPrimary.Id);
        CreateSystemGroup("Sundry Creditors", GroupNature.Liability, currLiab.Id);
        CreateSystemGroup("Duties & Taxes", GroupNature.Liability, currLiab.Id);
        CreateSystemGroup("Outstanding Expenses", GroupNature.Liability, currLiab.Id);
        CreateSystemGroup("Provisions", GroupNature.Liability, currLiab.Id);

        var loansLiab = CreateSystemGroup("Loans (Liability)", GroupNature.Liability, liabPrimary.Id);
        CreateSystemGroup("Bank OD Accounts", GroupNature.Liability, loansLiab.Id);
        CreateSystemGroup("Secured Loans", GroupNature.Liability, loansLiab.Id);
        CreateSystemGroup("Unsecured Loans", GroupNature.Liability, loansLiab.Id);

        // 4. Assets Subgroups
        var currAssets = CreateSystemGroup("Current Assets", GroupNature.Asset, assetPrimary.Id);
        CreateSystemGroup("Sundry Debtors", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Cash-in-hand", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Bank Accounts", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Stock-in-hand", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Loans & Advances (Asset)", GroupNature.Asset, currAssets.Id);

        CreateSystemGroup("Fixed Assets", GroupNature.Asset, assetPrimary.Id);
        CreateSystemGroup("Investments", GroupNature.Asset, assetPrimary.Id);

        // 5. Income Subgroups
        var dirIncome = CreateSystemGroup("Direct Income", GroupNature.Income, incPrimary.Id);
        CreateSystemGroup("Sales Accounts", GroupNature.Income, dirIncome.Id);
        CreateSystemGroup("Indirect Income", GroupNature.Income, incPrimary.Id);

        // 6. Expense Subgroups
        var dirExp = CreateSystemGroup("Direct Expenses", GroupNature.Expense, expPrimary.Id);
        CreateSystemGroup("Purchase Accounts", GroupNature.Expense, dirExp.Id);
        CreateSystemGroup("Indirect Expenses", GroupNature.Expense, expPrimary.Id);

        await _accountGroupRepository.AddRangeAsync(seededGroups, cancellationToken);
    }

    private async Task<AccountGroupDto> MapToDtoAsync(AccountGroup group, CancellationToken cancellationToken)
    {
        string? parentName = null;
        if (group.ParentGroupId.HasValue)
        {
            var parent = await _accountGroupRepository.GetByIdAsync(group.CompanyId, group.ParentGroupId.Value, cancellationToken);
            parentName = parent?.Name;
        }

        return new AccountGroupDto(
            group.Id,
            group.CompanyId,
            group.Name,
            group.ParentGroupId,
            parentName,
            group.Nature,
            group.Nature.ToString(),
            group.IsPrimary,
            group.IsDefaultSystemGroup,
            group.CreatedAt,
            group.CreatedBy,
            group.ModifiedAt,
            group.ModifiedBy);
    }

    private static AccountGroupDto MapToDto(AccountGroup group, Dictionary<Guid, AccountGroup> allGroups)
    {
        string? parentName = null;
        if (group.ParentGroupId.HasValue && allGroups.TryGetValue(group.ParentGroupId.Value, out var parent))
        {
            parentName = parent.Name;
        }

        return new AccountGroupDto(
            group.Id,
            group.CompanyId,
            group.Name,
            group.ParentGroupId,
            parentName,
            group.Nature,
            group.Nature.ToString(),
            group.IsPrimary,
            group.IsDefaultSystemGroup,
            group.CreatedAt,
            group.CreatedBy,
            group.ModifiedAt,
            group.ModifiedBy);
    }
}