using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IItemGroupRepository
{
    Task<ItemGroup?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<ItemGroup?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemGroup>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemGroup>> GetByParentIdAsync(Guid companyId, Guid parentId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? parentGroupId = null, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task<bool> HasReferencingItemsAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task AddAsync(ItemGroup group, CancellationToken cancellationToken = default);
    Task UpdateAsync(ItemGroup group, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
