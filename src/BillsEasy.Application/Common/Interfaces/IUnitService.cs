using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IUnitService
{
    Task<UnitDto> CreateUnitAsync(CreateUnitDto dto, CancellationToken cancellationToken = default);
    Task<UnitDto> UpdateUnitAsync(UpdateUnitDto dto, CancellationToken cancellationToken = default);
    Task DeleteUnitAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<UnitDto?> GetUnitByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnitDto>> GetAllUnitsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UnitLookupDto>> GetLookupUnitsAsync(Guid companyId, CancellationToken cancellationToken = default);
}
