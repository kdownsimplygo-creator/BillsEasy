using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.IntegrationTests.Workflows.Simulation;

/// <summary>
/// Fault-injecting repository decorators for atomicity testing across Failure Points A, B, C, and D.
/// </summary>
public static class FaultyTestRepositories
{
    // Failure Point A: Voucher insertion throws
    public class FaultyVoucherRepository : IVoucherRepository
    {
        private readonly IVoucherRepository _inner;
        private readonly bool _throwOnAdd;

        public FaultyVoucherRepository(IVoucherRepository inner, bool throwOnAdd = true)
        {
            _inner = inner;
            _throwOnAdd = throwOnAdd;
        }

        public Task<Voucher?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(companyId, id, cancellationToken);

        public Task<Voucher?> GetByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, CancellationToken cancellationToken = default)
            => _inner.GetByNumberAsync(companyId, financialYearId, voucherNumber, cancellationToken);

        public Task<IReadOnlyList<Voucher>> GetAllAsync(Guid companyId, Guid? financialYearId = null, VoucherType? type = null, VoucherStatus? status = null, DateOnly? fromDate = null, DateOnly? toDate = null, Guid? ledgerId = null, CancellationToken cancellationToken = default)
            => _inner.GetAllAsync(companyId, financialYearId, type, status, fromDate, toDate, ledgerId, cancellationToken);

        public Task<bool> ExistsByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => _inner.ExistsByNumberAsync(companyId, financialYearId, voucherNumber, excludeId, cancellationToken);

        public Task<bool> ExistsByNumberAsync(Guid companyId, Guid financialYearId, string voucherNumber, Guid? excludeId, DbTransaction? transaction, CancellationToken cancellationToken = default)
            => _inner.ExistsByNumberAsync(companyId, financialYearId, voucherNumber, excludeId, transaction, cancellationToken);

        public Task<(VoucherLine Line, Guid CompanyId)?> GetLineWithCompanyAsync(Guid voucherLineId, CancellationToken cancellationToken = default)
            => _inner.GetLineWithCompanyAsync(voucherLineId, cancellationToken);

        public Task<(VoucherLine Line, Guid CompanyId)?> GetLineWithCompanyAsync(Guid voucherLineId, DbTransaction? transaction, CancellationToken cancellationToken = default)
            => _inner.GetLineWithCompanyAsync(voucherLineId, transaction, cancellationToken);

        public Task AddAsync(Voucher voucher, CancellationToken cancellationToken = default)
        {
            if (_throwOnAdd)
                throw new InvalidOperationException("Simulated Failure Point A: Voucher insertion error");
            return _inner.AddAsync(voucher, cancellationToken);
        }

        public Task AddAsync(Voucher voucher, DbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            if (_throwOnAdd)
                throw new InvalidOperationException("Simulated Failure Point A: Voucher insertion error");
            return _inner.AddAsync(voucher, transaction, cancellationToken);
        }

        public Task UpdateAsync(Voucher voucher, CancellationToken cancellationToken = default)
            => _inner.UpdateAsync(voucher, cancellationToken);

        public Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(companyId, id, cancellationToken);
    }

    // Failure Point B: BillReference insertion throws
    public class FaultyBillReferenceRepository : IBillReferenceRepository
    {
        private readonly IBillReferenceRepository _inner;
        private readonly bool _throwOnAdd;

        public FaultyBillReferenceRepository(IBillReferenceRepository inner, bool throwOnAdd = true)
        {
            _inner = inner;
            _throwOnAdd = throwOnAdd;
        }

        public Task<BillReference?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(companyId, id, cancellationToken);

        public Task<BillReference?> GetByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, CancellationToken cancellationToken = default)
            => _inner.GetByReferenceNumberAsync(companyId, ledgerId, referenceNumber, cancellationToken);

        public Task<BillReference?> GetBySourceVoucherLineIdAsync(Guid companyId, Guid sourceVoucherLineId, CancellationToken cancellationToken = default)
            => _inner.GetBySourceVoucherLineIdAsync(companyId, sourceVoucherLineId, cancellationToken);

        public Task<IReadOnlyList<BillReferenceWithBalance>> GetActiveBillsByLedgerAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
            => _inner.GetActiveBillsByLedgerAsync(companyId, ledgerId, cancellationToken);

        public Task<Money> GetOutstandingBalanceAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default)
            => _inner.GetOutstandingBalanceAsync(companyId, billReferenceId, cancellationToken);

        public Task<Money> GetTotalAllocatedAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default)
            => _inner.GetTotalAllocatedAsync(companyId, billReferenceId, cancellationToken);

        public Task<bool> ExistsByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => _inner.ExistsByReferenceNumberAsync(companyId, ledgerId, referenceNumber, excludeId, cancellationToken);

        public Task<bool> ExistsByReferenceNumberAsync(Guid companyId, Guid ledgerId, string referenceNumber, Guid? excludeId, DbTransaction? transaction, CancellationToken cancellationToken = default)
            => _inner.ExistsByReferenceNumberAsync(companyId, ledgerId, referenceNumber, excludeId, transaction, cancellationToken);

        public Task AddAsync(BillReference billReference, CancellationToken cancellationToken = default)
        {
            if (_throwOnAdd)
                throw new InvalidOperationException("Simulated Failure Point B: BillReference insertion error");
            return _inner.AddAsync(billReference, cancellationToken);
        }

        public Task AddAsync(BillReference billReference, DbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            if (_throwOnAdd)
                throw new InvalidOperationException("Simulated Failure Point B: BillReference insertion error");
            return _inner.AddAsync(billReference, transaction, cancellationToken);
        }

        public Task UpdateAsync(BillReference billReference, CancellationToken cancellationToken = default)
            => _inner.UpdateAsync(billReference, cancellationToken);

        public Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(companyId, id, cancellationToken);
    }

    // Failure Point C: PurchaseInvoice insertion throws
    public class FaultyPurchaseInvoiceRepository : IPurchaseInvoiceRepository
    {
        private readonly IPurchaseInvoiceRepository _inner;
        private readonly bool _throwOnAdd;

        public FaultyPurchaseInvoiceRepository(IPurchaseInvoiceRepository inner, bool throwOnAdd = true)
        {
            _inner = inner;
            _throwOnAdd = throwOnAdd;
        }

        public Task<PurchaseInvoice?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(companyId, id, cancellationToken);

        public Task<PurchaseInvoice?> GetByInvoiceNumberAsync(Guid companyId, Guid financialYearId, string invoiceNumber, CancellationToken cancellationToken = default)
            => _inner.GetByInvoiceNumberAsync(companyId, financialYearId, invoiceNumber, cancellationToken);

        public Task<bool> ExistsByInvoiceNumberAsync(Guid companyId, Guid financialYearId, string invoiceNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
            => _inner.ExistsByInvoiceNumberAsync(companyId, financialYearId, invoiceNumber, excludeId, cancellationToken);

        public Task<IReadOnlyList<PurchaseInvoice>> GetAllAsync(Guid companyId, Guid? financialYearId = null, DateOnly? fromDate = null, DateOnly? toDate = null, Guid? supplierId = null, CancellationToken cancellationToken = default)
            => _inner.GetAllAsync(companyId, financialYearId, fromDate, toDate, supplierId, cancellationToken);

        public Task AddAsync(PurchaseInvoice invoice, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (_throwOnAdd)
                throw new InvalidOperationException("Simulated Failure Point C: PurchaseInvoice insertion error");
            return _inner.AddAsync(invoice, transaction, cancellationToken);
        }

        public Task UpdateAsync(PurchaseInvoice invoice, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
            => _inner.UpdateAsync(invoice, transaction, cancellationToken);
    }

    // Failure Point D: StockMovement insertion throws
    public class FaultyStockMovementRepository : IStockMovementRepository
    {
        private readonly IStockMovementRepository _inner;
        private readonly bool _throwOnAdd;

        public FaultyStockMovementRepository(IStockMovementRepository inner, bool throwOnAdd = true)
        {
            _inner = inner;
            _throwOnAdd = throwOnAdd;
        }

        public Task AddRangeAsync(IEnumerable<StockMovement> movements, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (_throwOnAdd)
                throw new InvalidOperationException("Simulated Failure Point D: StockMovement insertion error");
            return _inner.AddRangeAsync(movements, transaction, cancellationToken);
        }

        public Task<IReadOnlyList<StockMovement>> GetByItemIdAsync(Guid companyId, Guid itemId, DateOnly? fromDate = null, DateOnly? toDate = null, CancellationToken cancellationToken = default)
            => _inner.GetByItemIdAsync(companyId, itemId, fromDate, toDate, cancellationToken);

        public Task<decimal> GetCurrentQuantityAsync(Guid companyId, Guid itemId, string materialCentre = "Main Store", CancellationToken cancellationToken = default)
            => _inner.GetCurrentQuantityAsync(companyId, itemId, materialCentre, cancellationToken);
    }
}
