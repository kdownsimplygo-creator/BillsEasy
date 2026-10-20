using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<Item?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    Task<Item?> GetByCodeAsync(Guid companyId, string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetByGroupIdAsync(Guid companyId, Guid itemGroupId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Item item, CancellationToken cancellationToken = default);
    Task UpdateAsync(Item item, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
