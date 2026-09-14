using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IAccountGroupService
{
    Task<AccountGroupDto> CreateGroupAsync(CreateAccountGroupDto dto, CancellationToken cancellationToken = default);
    Task<AccountGroupDto> UpdateGroupAsync(UpdateAccountGroupDto dto, CancellationToken cancellationToken = default);
    Task DeleteGroupAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<AccountGroupDto?> GetGroupByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroupDto>> GetAllGroupsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroupNodeDto>> GetGroupTreeAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroupLookupDto>> GetLookupGroupsAsync(Guid companyId, Guid? excludeSubtreeOfGroupId = null, CancellationToken cancellationToken = default);
    Task SeedDefaultGroupsAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default);
}