using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.Common.Interfaces;

public interface IVoucherRepository
{
    Task<Voucher?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<Voucher?> GetByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Voucher>> GetAllAsync(
        Guid companyId,
        Guid? financialYearId = null,
        VoucherType? type = null,
        VoucherStatus? status = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? ledgerId = null,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, Guid? excludeId, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default);
    Task<(VoucherLine Line, Guid CompanyId)?> GetLineWithCompanyAsync(Guid voucherLineId, CancellationToken cancellationToken = default);
    Task<(VoucherLine Line, Guid CompanyId)?> GetLineWithCompanyAsync(Guid voucherLineId, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default);
    Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default);
    Task AddAsync(Voucher voucher, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(Voucher voucher, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
