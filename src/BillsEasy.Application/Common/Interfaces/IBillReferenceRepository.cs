using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Application.Common.Interfaces;

public record BillReferenceWithBalance(
    BillReference Bill,
    Money TotalAllocated,
    Money OutstandingAmount);

public interface IBillReferenceRepository
{
    Task<BillReference?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<BillReference?> GetByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, CancellationToken cancellationToken = default);
    Task<BillReference?> GetBySourceVoucherLineIdAsync(Guid companyId, Guid sourceVoucherLineId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillReferenceWithBalance>> GetActiveBillsByLedgerAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default);
    Task<Money> GetOutstandingBalanceAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default);
    Task<Money> GetTotalAllocatedAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, Guid? excludeId, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default);
    Task AddAsync(BillReference billReference, CancellationToken cancellationToken = default);
    Task AddAsync(BillReference billReference, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(BillReference billReference, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
