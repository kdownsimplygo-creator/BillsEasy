using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IPostingService
{
    Task<VoucherDto> PostVoucherAsync(CreateVoucherDto dto, CancellationToken cancellationToken = default);
    Task<VoucherDto> PostVoucherAsync(CreateVoucherDto dto, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default);
    Task<VoucherDto> PostExistingVoucherAsync(Guid companyId, Guid voucherId, CancellationToken cancellationToken = default);
    Task<VoucherDto> CancelVoucherAsync(Guid companyId, Guid voucherId, string? cancellationReason = null, CancellationToken cancellationToken = default);
    Task<VoucherDto?> GetVoucherByIdAsync(Guid companyId, Guid voucherId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VoucherDto>> GetVouchersAsync(VoucherFilterDto filter, CancellationToken cancellationToken = default);
}
