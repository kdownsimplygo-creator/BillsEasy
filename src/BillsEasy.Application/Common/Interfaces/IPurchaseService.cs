using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IPurchaseService
{
    Task<PurchaseInvoiceDto> CreatePurchaseInvoiceAsync(CreatePurchaseInvoiceDto dto, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceSummaryDto>> GetSummariesAsync(
        Guid companyId,
        Guid? financialYearId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);
    Task<decimal> GetLiveStockQuantityAsync(
        Guid companyId,
        Guid itemId,
        string materialCentre = "Main Store",
        CancellationToken cancellationToken = default);
}
