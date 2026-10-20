using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IBillAllocationRepository
{
    Task<IReadOnlyList<BillAllocation>> GetByVoucherLineIdAsync(Guid companyId, Guid voucherLineId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillAllocation>> GetByBillReferenceIdAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default);
    Task AddAsync(BillAllocation allocation, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<BillAllocation> allocations, CancellationToken cancellationToken = default);
    Task DeleteByVoucherLineIdAsync(Guid companyId, Guid voucherLineId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
