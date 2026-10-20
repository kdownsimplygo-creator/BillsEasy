using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IBillWiseService
{
    Task<BillReferenceDto> CreateBillReferenceAsync(CreateBillReferenceDto dto, CancellationToken cancellationToken = default);
    Task<BillReferenceDto> CreateBillReferenceAsync(CreateBillReferenceDto dto, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default);
    Task<BillAllocationDto> AllocateBillAsync(CreateBillAllocationDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingBillDto>> GetPendingBillsAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default);
    Task<BillReferenceDto?> GetBillByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillAllocationDto>> GetAllocationsForVoucherLineAsync(Guid companyId, Guid voucherLineId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BillAllocationDto>> GetAllocationsForBillAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default);
    Task CancelBillReferenceAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default);
}
