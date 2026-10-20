using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Application.Services;

public class ItemService : IItemService
{
    private readonly IItemRepository _itemRepository;
    private readonly IItemGroupRepository _itemGroupRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ItemService(
        IItemRepository itemRepository,
        IItemGroupRepository itemGroupRepository,
        IUnitRepository unitRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _itemRepository = itemRepository;
        _itemGroupRepository = itemGroupRepository;
        _unitRepository = unitRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<ItemDto> CreateItemAsync(CreateItemDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Item name is required.");

        var group = await _itemGroupRepository.GetByIdAsync(dto.CompanyId, dto.ItemGroupId, cancellationToken);
        if (group == null)
            throw new DomainException($"Item group with ID '{dto.ItemGroupId}' not found.");
        if (group.CompanyId != dto.CompanyId)
            throw new DomainException("Item group does not belong to the active company.");

        var mainUnit = await _unitRepository.GetByIdAsync(dto.CompanyId, dto.UnitId, cancellationToken);
        if (mainUnit == null)
            throw new DomainException($"Unit with ID '{dto.UnitId}' not found.");
        if (mainUnit.CompanyId != dto.CompanyId)
            throw new DomainException("Unit does not belong to the active company.");

        string? altUnitName = null;
        if (dto.AlternateUnitId.HasValue)
        {
            if (dto.AlternateUnitId.Value == dto.UnitId)
                throw new DomainException("Alternate unit cannot be the same as main unit.");

            var altUnit = await _unitRepository.GetByIdAsync(dto.CompanyId, dto.AlternateUnitId.Value, cancellationToken);
            if (altUnit == null)
                throw new DomainException($"Alternate unit with ID '{dto.AlternateUnitId.Value}' not found.");
            if (altUnit.CompanyId != dto.CompanyId)
                throw new DomainException("Alternate unit does not belong to the active company.");

            if (!dto.ConversionFactor.HasValue || dto.ConversionFactor.Value <= 0)
                throw new DomainException("Conversion factor must be greater than zero when an alternate unit is specified.");

            altUnitName = altUnit.Name;
        }

        var trimmedName = dto.Name.Trim();
        if (await _itemRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, null, cancellationToken))
            throw new DomainException($"An item with name '{trimmedName}' already exists.");

        string? trimmedCode = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
        if (trimmedCode != null && await _itemRepository.ExistsByCodeAsync(dto.CompanyId, trimmedCode, null, cancellationToken))
            throw new DomainException($"An item with code '{trimmedCode}' already exists.");

        var opRate = Money.FromDecimal(dto.OpeningStockRate);
        var opValue = dto.OpeningStockValue > 0
            ? Money.FromDecimal(dto.OpeningStockValue)
            : (dto.OpeningStockQuantity > 0 && dto.OpeningStockRate > 0
                ? Money.FromDecimal(dto.OpeningStockQuantity * dto.OpeningStockRate)
                : Money.Zero());

        Money? salesPrice = dto.SalesPrice.HasValue ? Money.FromDecimal(dto.SalesPrice.Value) : null;
        Money? purchasePrice = dto.PurchasePrice.HasValue ? Money.FromDecimal(dto.PurchasePrice.Value) : null;
        Money? mrp = dto.MRP.HasValue ? Money.FromDecimal(dto.MRP.Value) : null;
        Money? minSalePrice = dto.MinSalePrice.HasValue ? Money.FromDecimal(dto.MinSalePrice.Value) : null;

        var item = Item.Create(
            dto.CompanyId,
            dto.ItemGroupId,
            dto.UnitId,
            trimmedName,
            dto.PrintName,
            trimmedCode,
            dto.AlternateUnitId,
            dto.ConversionFactor,
            dto.OpeningStockQuantity,
            opRate,
            opValue,
            salesPrice,
            purchasePrice,
            mrp,
            minSalePrice,
            dto.DiscountPercent,
            dto.MinLevel,
            dto.MaxLevel,
            dto.ReorderLevel,
            dto.Description,
            dto.Barcode,
            dto.IsActive);

        item.CreatedAt = _dateTimeProvider.UtcNow;
        item.CreatedBy = _currentUserService.UserName ?? "system";

        await _itemRepository.AddAsync(item, cancellationToken);

        return MapToDto(item, group.Name, mainUnit.Name, altUnitName);
    }

    public async Task<ItemDto> UpdateItemAsync(UpdateItemDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        var item = await _itemRepository.GetByIdAsync(dto.CompanyId, dto.Id, cancellationToken);
        if (item == null)
            throw new DomainException($"Item with ID '{dto.Id}' not found.");
        if (item.CompanyId != dto.CompanyId)
            throw new DomainException("Item does not belong to the active company.");

        var group = await _itemGroupRepository.GetByIdAsync(dto.CompanyId, dto.ItemGroupId, cancellationToken);
        if (group == null)
            throw new DomainException($"Item group with ID '{dto.ItemGroupId}' not found.");
        if (group.CompanyId != dto.CompanyId)
            throw new DomainException("Item group does not belong to the active company.");

        var mainUnit = await _unitRepository.GetByIdAsync(dto.CompanyId, dto.UnitId, cancellationToken);
        if (mainUnit == null)
            throw new DomainException($"Unit with ID '{dto.UnitId}' not found.");
        if (mainUnit.CompanyId != dto.CompanyId)
            throw new DomainException("Unit does not belong to the active company.");

        string? altUnitName = null;
        if (dto.AlternateUnitId.HasValue)
        {
            if (dto.AlternateUnitId.Value == dto.UnitId)
                throw new DomainException("Alternate unit cannot be the same as main unit.");

            var altUnit = await _unitRepository.GetByIdAsync(dto.CompanyId, dto.AlternateUnitId.Value, cancellationToken);
            if (altUnit == null)
                throw new DomainException($"Alternate unit with ID '{dto.AlternateUnitId.Value}' not found.");
            if (altUnit.CompanyId != dto.CompanyId)
                throw new DomainException("Alternate unit does not belong to the active company.");

            if (!dto.ConversionFactor.HasValue || dto.ConversionFactor.Value <= 0)
                throw new DomainException("Conversion factor must be greater than zero when an alternate unit is specified.");

            altUnitName = altUnit.Name;
        }

        var trimmedName = dto.Name.Trim();
        if (await _itemRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.Id, cancellationToken))
            throw new DomainException($"An item with name '{trimmedName}' already exists.");

        string? trimmedCode = string.IsNullOrWhiteSpace(dto.Code) ? null : dto.Code.Trim();
        if (trimmedCode != null && await _itemRepository.ExistsByCodeAsync(dto.CompanyId, trimmedCode, dto.Id, cancellationToken))
            throw new DomainException($"An item with code '{trimmedCode}' already exists.");

        var opRate = Money.FromDecimal(dto.OpeningStockRate);
        var opValue = dto.OpeningStockValue > 0
            ? Money.FromDecimal(dto.OpeningStockValue)
            : (dto.OpeningStockQuantity > 0 && dto.OpeningStockRate > 0
                ? Money.FromDecimal(dto.OpeningStockQuantity * dto.OpeningStockRate)
                : Money.Zero());

        Money? salesPrice = dto.SalesPrice.HasValue ? Money.FromDecimal(dto.SalesPrice.Value) : null;
        Money? purchasePrice = dto.PurchasePrice.HasValue ? Money.FromDecimal(dto.PurchasePrice.Value) : null;
        Money? mrp = dto.MRP.HasValue ? Money.FromDecimal(dto.MRP.Value) : null;
        Money? minSalePrice = dto.MinSalePrice.HasValue ? Money.FromDecimal(dto.MinSalePrice.Value) : null;

        item.Update(
            dto.ItemGroupId,
            dto.UnitId,
            trimmedName,
            dto.PrintName,
            trimmedCode,
            dto.AlternateUnitId,
            dto.ConversionFactor,
            dto.OpeningStockQuantity,
            opRate,
            opValue,
            salesPrice,
            purchasePrice,
            mrp,
            minSalePrice,
            dto.DiscountPercent,
            dto.MinLevel,
            dto.MaxLevel,
            dto.ReorderLevel,
            dto.Description,
            dto.Barcode,
            dto.IsActive);

        item.ModifiedAt = _dateTimeProvider.UtcNow;
        item.ModifiedBy = _currentUserService.UserName ?? "system";

        await _itemRepository.UpdateAsync(item, cancellationToken);

        return MapToDto(item, group.Name, mainUnit.Name, altUnitName);
    }

    public async Task DeleteItemAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        var item = await _itemRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (item == null)
            throw new DomainException($"Item with ID '{id}' not found.");

        if (item.CompanyId != companyId)
            throw new DomainException("Item does not belong to the active company.");

        await _itemRepository.DeleteAsync(companyId, id, cancellationToken);
    }

    public async Task<ItemDto?> GetItemByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return null;

        var item = await _itemRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (item == null)
            return null;

        var group = await _itemGroupRepository.GetByIdAsync(companyId, item.ItemGroupId, cancellationToken);
        var unit = await _unitRepository.GetByIdAsync(companyId, item.UnitId, cancellationToken);
        string? altUnitName = null;
        if (item.AlternateUnitId.HasValue)
        {
            var altUnit = await _unitRepository.GetByIdAsync(companyId, item.AlternateUnitId.Value, cancellationToken);
            altUnitName = altUnit?.Name;
        }

        return MapToDto(item, group?.Name ?? string.Empty, unit?.Name ?? string.Empty, altUnitName);
    }

    public async Task<IReadOnlyList<ItemListDto>> GetAllItemsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<ItemListDto>();

        var items = await _itemRepository.GetAllAsync(companyId, cancellationToken);
        if (items.Count == 0)
            return Array.Empty<ItemListDto>();

        var groups = (await _itemGroupRepository.GetAllAsync(companyId, cancellationToken)).ToDictionary(g => g.Id, g => g.Name);
        var units = (await _unitRepository.GetAllAsync(companyId, cancellationToken)).ToDictionary(u => u.Id, u => u.Name);

        return items.Select(item => new ItemListDto(
            item.Id,
            item.Name,
            item.Code,
            groups.TryGetValue(item.ItemGroupId, out var gName) ? gName : string.Empty,
            units.TryGetValue(item.UnitId, out var uName) ? uName : string.Empty,
            item.OpeningStockQuantity,
            item.OpeningStockRate,
            item.OpeningStockValue,
            item.SalesPrice,
            item.MRP,
            item.IsActive)).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<ItemListDto>> GetItemsByGroupIdAsync(Guid companyId, Guid itemGroupId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<ItemListDto>();

        var items = await _itemRepository.GetByGroupIdAsync(companyId, itemGroupId, cancellationToken);
        if (items.Count == 0)
            return Array.Empty<ItemListDto>();

        var group = await _itemGroupRepository.GetByIdAsync(companyId, itemGroupId, cancellationToken);
        var groupName = group?.Name ?? string.Empty;
        var units = (await _unitRepository.GetAllAsync(companyId, cancellationToken)).ToDictionary(u => u.Id, u => u.Name);

        return items.Select(item => new ItemListDto(
            item.Id,
            item.Name,
            item.Code,
            groupName,
            units.TryGetValue(item.UnitId, out var uName) ? uName : string.Empty,
            item.OpeningStockQuantity,
            item.OpeningStockRate,
            item.OpeningStockValue,
            item.SalesPrice,
            item.MRP,
            item.IsActive)).ToList().AsReadOnly();
    }

    private static ItemDto MapToDto(Item item, string groupName, string unitName, string? altUnitName)
    {
        return new ItemDto(
            item.Id,
            item.CompanyId,
            item.ItemGroupId,
            groupName,
            item.UnitId,
            unitName,
            item.AlternateUnitId,
            altUnitName,
            item.ConversionFactor,
            item.Name,
            item.PrintName,
            item.Code,
            item.OpeningStockQuantity,
            item.OpeningStockRate,
            item.OpeningStockValue,
            item.SalesPrice,
            item.PurchasePrice,
            item.MRP,
            item.MinSalePrice,
            item.DiscountPercent,
            item.MinLevel,
            item.MaxLevel,
            item.ReorderLevel,
            item.Description,
            item.Barcode,
            item.IsActive,
            item.CreatedAt,
            item.CreatedBy,
            item.ModifiedAt,
            item.ModifiedBy);
    }
}
