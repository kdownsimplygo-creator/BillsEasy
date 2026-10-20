using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IUnitRepository
{
    Task<Unit?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<Unit?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Unit>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasReferencingItemsAsync(Guid companyId, Guid unitId, CancellationToken cancellationToken = default);
    Task AddAsync(Unit unit, CancellationToken cancellationToken = default);
    Task UpdateAsync(Unit unit, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
