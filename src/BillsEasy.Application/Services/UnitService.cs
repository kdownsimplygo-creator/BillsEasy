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

public class UnitService : IUnitService
{
    private readonly IUnitRepository _unitRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UnitService(
        IUnitRepository unitRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitRepository = unitRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UnitDto> CreateUnitAsync(CreateUnitDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Unit name is required.");

        var trimmedName = dto.Name.Trim();
        if (await _unitRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, null, cancellationToken))
            throw new DomainException($"A unit with name '{trimmedName}' already exists.");

        var unit = Unit.Create(dto.CompanyId, trimmedName, dto.PrintName, dto.DecimalPlaces);
        unit.CreatedAt = _dateTimeProvider.UtcNow;
        unit.CreatedBy = _currentUserService.UserName ?? "system";

        await _unitRepository.AddAsync(unit, cancellationToken);

        return MapToDto(unit);
    }

    public async Task<UnitDto> UpdateUnitAsync(UpdateUnitDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        var unit = await _unitRepository.GetByIdAsync(dto.CompanyId, dto.Id, cancellationToken);
        if (unit == null)
            throw new DomainException($"Unit with ID '{dto.Id}' not found.");

        if (unit.CompanyId != dto.CompanyId)
            throw new DomainException("Unit does not belong to the active company.");

        var trimmedName = dto.Name.Trim();
        if (await _unitRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.Id, cancellationToken))
            throw new DomainException($"A unit with name '{trimmedName}' already exists.");

        unit.Update(trimmedName, dto.PrintName, dto.DecimalPlaces);
        unit.ModifiedAt = _dateTimeProvider.UtcNow;
        unit.ModifiedBy = _currentUserService.UserName ?? "system";

        await _unitRepository.UpdateAsync(unit, cancellationToken);

        return MapToDto(unit);
    }

    public async Task DeleteUnitAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        var unit = await _unitRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (unit == null)
            throw new DomainException($"Unit with ID '{id}' not found.");

        if (unit.CompanyId != companyId)
            throw new DomainException("Unit does not belong to the active company.");

        if (await _unitRepository.HasReferencingItemsAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete unit '{unit.Name}' because it is referenced by one or more items.");

        await _unitRepository.DeleteAsync(companyId, id, cancellationToken);
    }

    public async Task<UnitDto?> GetUnitByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return null;

        var unit = await _unitRepository.GetByIdAsync(companyId, id, cancellationToken);
        return unit == null ? null : MapToDto(unit);
    }

    public async Task<IReadOnlyList<UnitDto>> GetAllUnitsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<UnitDto>();

        var units = await _unitRepository.GetAllAsync(companyId, cancellationToken);
        return units.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<UnitLookupDto>> GetLookupUnitsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            return Array.Empty<UnitLookupDto>();

        var units = await _unitRepository.GetAllAsync(companyId, cancellationToken);
        return units.Select(u => new UnitLookupDto(u.Id, u.Name, u.PrintName, u.DecimalPlaces)).ToList().AsReadOnly();
    }

    private static UnitDto MapToDto(Unit unit)
    {
        return new UnitDto(
            unit.Id,
            unit.CompanyId,
            unit.Name,
            unit.PrintName,
            unit.DecimalPlaces,
            unit.CreatedAt,
            unit.CreatedBy,
            unit.ModifiedAt,
            unit.ModifiedBy);
    }
}
