using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IItemService
{
    Task<ItemDto> CreateItemAsync(CreateItemDto dto, CancellationToken cancellationToken = default);
    Task<ItemDto> UpdateItemAsync(UpdateItemDto dto, CancellationToken cancellationToken = default);
    Task DeleteItemAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<ItemDto?> GetItemByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemListDto>> GetAllItemsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemListDto>> GetItemsByGroupIdAsync(Guid companyId, Guid itemGroupId, CancellationToken cancellationToken = default);
}
