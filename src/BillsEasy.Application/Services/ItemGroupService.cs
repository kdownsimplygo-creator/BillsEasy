using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Services;

public class ItemGroupService : IItemGroupService
{
    private readonly IItemGroupRepository _itemGroupRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ItemGroupService(
        IItemGroupRepository itemGroupRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _itemGroupRepository = itemGroupRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ItemGroupDto> CreateGroupAsync(CreateItemGroupDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Item group name cannot be empty.");

        var trimmedName = dto.Name.Trim();
        if (await _itemGroupRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.ParentGroupId, null, cancellationToken))
            throw new DomainException($"An item group with name '{trimmedName}' already exists under the selected parent group.");

        string? parentGroupName = null;
        if (dto.ParentGroupId.HasValue)
        {
            var parent = await _itemGroupRepository.GetByIdAsync(dto.CompanyId, dto.ParentGroupId.Value, cancellationToken);
            if (parent == null)
                throw new DomainException($"Parent item group with ID '{dto.ParentGroupId.Value}' not found.");

            if (parent.CompanyId != dto.CompanyId)
                throw new DomainException("Parent item group must belong to the active company.");

            parentGroupName = parent.Name;
        }

        var group = ItemGroup.Create(dto.CompanyId, trimmedName, dto.ParentGroupId, dto.Description);
        group.CreatedAt = _dateTimeProvider.UtcNow;
        group.CreatedBy = _currentUserService.UserName ?? "system";

        await _itemGroupRepository.AddAsync(group, cancellationToken);

        return MapToDto(group, parentGroupName);
    }

    public async Task<ItemGroupDto> UpdateGroupAsync(UpdateItemGroupDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        var group = await _itemGroupRepository.GetByIdAsync(dto.CompanyId, dto.Id, cancellationToken);
        if (group == null)
            throw new DomainException($"Item group with ID '{dto.Id}' not found.");

        if (group.CompanyId != dto.CompanyId)
            throw new DomainException("Item group does not belong to the active company.");

        var trimmedName = dto.Name.Trim();
        if (await _itemGroupRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.ParentGroupId, dto.Id, cancellationToken))
            throw new DomainException($"An item group with name '{trimmedName}' already exists under the selected parent group.");

        string? parentGroupName = null;
        if (dto.ParentGroupId.HasValue)
        {
            if (dto.ParentGroupId.Value == dto.Id)
                throw new DomainException("An item group cannot be its own parent.");

            var parent = await _itemGroupRepository.GetByIdAsync(dto.CompanyId, dto.ParentGroupId.Value, cancellationToken);
            if (parent == null)
                throw new DomainException($"Parent item group with ID '{dto.ParentGroupId.Value}' not found.");

            if (parent.CompanyId != dto.CompanyId)
                throw new DomainException("Parent item group must belong to the active company.");

            // Check circular hierarchy
            var allGroups = await _itemGroupRepository.GetAllAsync(dto.CompanyId, cancellationToken);
            if (IsDescendantOf(dto.ParentGroupId.Value, dto.Id, allGroups))
                throw new DomainException("Cannot move an item group to be a child of one of its own sub-groups.");

            parentGroupName = parent.Name;
        }

        group.Update(trimmedName, dto.ParentGroupId, dto.Description);
        group.ModifiedAt = _dateTimeProvider.UtcNow;
        group.ModifiedBy = _currentUserService.UserName ?? "system";

        await _itemGroupRepository.UpdateAsync(group, cancellationToken);

        return MapToDto(group, parentGroupName);
    }

    public async Task DeleteGroupAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        var group = await _itemGroupRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (group == null)
            throw new DomainException($"Item group with ID '{id}' not found.");

        if (group.CompanyId != companyId)
            throw new DomainException("Item group does not belong to the active company.");

        if (group.IsDefaultSystemGroup)
            throw new DomainException("System default item groups cannot be deleted.");

        if (await _itemGroupRepository.HasChildrenAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete item group '{group.Name}' because it has one or more sub-groups.");

        if (await _itemGroupRepository.HasReferencingItemsAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete item group '{group.Name}' because it is referenced by one or more items.");

        await _itemGroupRepository.DeleteAsync(companyId, id, cancellationToken);
    }

    public async Task<ItemGroupDto?> GetGroupByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return null;

        var group = await _itemGroupRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (group == null)
            return null;

        string? parentName = null;
        if (group.ParentGroupId.HasValue)
        {
            var parent = await _itemGroupRepository.GetByIdAsync(companyId, group.ParentGroupId.Value, cancellationToken);
            parentName = parent?.Name;
        }

        return MapToDto(group, parentName);
    }

    public async Task<IReadOnlyList<ItemGroupDto>> GetAllGroupsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<ItemGroupDto>();

        var groups = await _itemGroupRepository.GetAllAsync(companyId, cancellationToken);
        var map = groups.ToDictionary(g => g.Id, g => g.Name);

        return groups.Select(g => MapToDto(g, g.ParentGroupId.HasValue && map.TryGetValue(g.ParentGroupId.Value, out var pName) ? pName : null)).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<ItemGroupNodeDto>> GetGroupTreeAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<ItemGroupNodeDto>();

        var allGroups = await _itemGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupMap = allGroups.ToDictionary(g => g.Id, g => g.Name);

        var nodeMap = allGroups.ToDictionary(
            g => g.Id,
            g => new ItemGroupNodeDto
            {
                Id = g.Id,
                CompanyId = g.CompanyId,
                Name = g.Name,
                ParentGroupId = g.ParentGroupId,
                ParentGroupName = g.ParentGroupId.HasValue && groupMap.TryGetValue(g.ParentGroupId.Value, out var pn) ? pn : null,
                Description = g.Description,
                IsDefaultSystemGroup = g.IsDefaultSystemGroup,
                Level = 0
            });

        var rootNodes = new List<ItemGroupNodeDto>();
        foreach (var group in allGroups)
        {
            var node = nodeMap[group.Id];
            if (group.ParentGroupId.HasValue && nodeMap.TryGetValue(group.ParentGroupId.Value, out var parentNode))
            {
                parentNode.Children.Add(node);
            }
            else
            {
                rootNodes.Add(node);
            }
        }

        AssignLevels(rootNodes, 0);
        return rootNodes.AsReadOnly();
    }

    public async Task<IReadOnlyList<ItemGroupLookupDto>> GetLookupGroupsAsync(Guid companyId, Guid? excludeSubtreeOfGroupId = null, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<ItemGroupLookupDto>();

        var allGroups = await _itemGroupRepository.GetAllAsync(companyId, cancellationToken);

        HashSet<Guid> excludedIds = new();
        if (excludeSubtreeOfGroupId.HasValue)
        {
            excludedIds.Add(excludeSubtreeOfGroupId.Value);
            CollectDescendantIds(excludeSubtreeOfGroupId.Value, allGroups, excludedIds);
        }

        return allGroups
            .Where(g => !excludedIds.Contains(g.Id))
            .OrderBy(g => g.Name)
            .Select(g => new ItemGroupLookupDto(g.Id, g.Name, g.ParentGroupId, g.IsDefaultSystemGroup))
            .ToList()
            .AsReadOnly();
    }

    private static void AssignLevels(IEnumerable<ItemGroupNodeDto> nodes, int currentLevel)
    {
        foreach (var node in nodes)
        {
            node.Level = currentLevel;
            AssignLevels(node.Children, currentLevel + 1);
        }
    }

    private static bool IsDescendantOf(Guid candidateDescendantId, Guid potentialAncestorId, IReadOnlyList<ItemGroup> allGroups)
    {
        var groupMap = allGroups.ToDictionary(g => g.Id);
        var current = groupMap.TryGetValue(candidateDescendantId, out var g) ? g : null;

        while (current != null && current.ParentGroupId.HasValue)
        {
            if (current.ParentGroupId.Value == potentialAncestorId)
                return true;

            current = groupMap.TryGetValue(current.ParentGroupId.Value, out var parent) ? parent : null;
        }

        return false;
    }

    private static void CollectDescendantIds(Guid parentId, IReadOnlyList<ItemGroup> allGroups, HashSet<Guid> collector)
    {
        var children = allGroups.Where(g => g.ParentGroupId == parentId).ToList();
        foreach (var child in children)
        {
            if (collector.Add(child.Id))
            {
                CollectDescendantIds(child.Id, allGroups, collector);
            }
        }
    }

    private static ItemGroupDto MapToDto(ItemGroup group, string? parentGroupName)
    {
        return new ItemGroupDto(
            group.Id,
            group.CompanyId,
            group.Name,
            group.ParentGroupId,
            parentGroupName,
            group.Description,
            group.IsDefaultSystemGroup,
            group.CreatedAt,
            group.CreatedBy,
            group.ModifiedAt,
            group.ModifiedBy);
    }
}
