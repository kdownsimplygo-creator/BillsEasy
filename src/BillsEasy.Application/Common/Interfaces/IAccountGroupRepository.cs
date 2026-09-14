using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IAccountGroupRepository
{
    Task<AccountGroup?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<AccountGroup?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroup>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroup>> GetByParentIdAsync(Guid companyId, Guid parentId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? parentGroupId = null, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task<bool> HasLedgersAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task AddAsync(AccountGroup group, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<AccountGroup> groups, CancellationToken cancellationToken = default);
    Task UpdateAsync(AccountGroup group, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}