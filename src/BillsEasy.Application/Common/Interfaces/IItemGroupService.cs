using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IItemGroupService
{
    Task<ItemGroupDto> CreateGroupAsync(CreateItemGroupDto dto, CancellationToken cancellationToken = default);
    Task<ItemGroupDto> UpdateGroupAsync(UpdateItemGroupDto dto, CancellationToken cancellationToken = default);
    Task DeleteGroupAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<ItemGroupDto?> GetGroupByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemGroupDto>> GetAllGroupsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemGroupNodeDto>> GetGroupTreeAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemGroupLookupDto>> GetLookupGroupsAsync(Guid companyId, Guid? excludeSubtreeOfGroupId = null, CancellationToken cancellationToken = default);
}
