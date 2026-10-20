using System;

namespace BillsEasy.Application.DTOs;

public record UnitDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string PrintName,
    int DecimalPlaces,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);

public record CreateUnitDto(
    Guid CompanyId,
    string Name,
    string? PrintName = null,
    int DecimalPlaces = 0);

public record UpdateUnitDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string? PrintName = null,
    int DecimalPlaces = 0);

public record UnitLookupDto(
    Guid Id,
    string Name,
    string PrintName,
    int DecimalPlaces);
