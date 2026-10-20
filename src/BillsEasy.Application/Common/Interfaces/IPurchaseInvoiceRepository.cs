using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IPurchaseInvoiceRepository
{
    Task<PurchaseInvoice?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<PurchaseInvoice?> GetByInvoiceNumberAsync(Guid companyId, Guid financialYearId, string invoiceNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsByInvoiceNumberAsync(Guid companyId, Guid financialYearId, string invoiceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoice>> GetAllAsync(
        Guid companyId,
        Guid? financialYearId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);
    Task AddAsync(PurchaseInvoice invoice, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(PurchaseInvoice invoice, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
