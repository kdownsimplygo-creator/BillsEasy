using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IStockMovementRepository
{
    Task AddRangeAsync(IEnumerable<StockMovement> movements, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockMovement>> GetByItemIdAsync(
        Guid companyId,
        Guid itemId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);
    Task<decimal> GetCurrentQuantityAsync(
        Guid companyId,
        Guid itemId,
        string materialCentre = "Main Store",
        CancellationToken cancellationToken = default);
}
