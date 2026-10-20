using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Application.Services;

public class BillWiseService : IBillWiseService
{
    private readonly IBillReferenceRepository _billReferenceRepository;
    private readonly IBillAllocationRepository _billAllocationRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BillWiseService(
        IBillReferenceRepository billReferenceRepository,
        IBillAllocationRepository billAllocationRepository,
        IVoucherRepository voucherRepository,
        ICompanyRepository companyRepository,
        ILedgerRepository ledgerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _billReferenceRepository = billReferenceRepository;
        _billAllocationRepository = billAllocationRepository;
        _voucherRepository = voucherRepository;
        _companyRepository = companyRepository;
        _ledgerRepository = ledgerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public Task<BillReferenceDto> CreateBillReferenceAsync(CreateBillReferenceDto dto, CancellationToken cancellationToken = default)
        => CreateBillReferenceAsync(dto, null, cancellationToken);

    public async Task<BillReferenceDto> CreateBillReferenceAsync(CreateBillReferenceDto dto, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.LedgerId == Guid.Empty)
            throw new DomainException("LedgerId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.ReferenceNumber))
            throw new DomainException("ReferenceNumber cannot be empty.");

        if (dto.OriginalAmount <= 0)
            throw new DomainException("OriginalAmount must be greater than zero.");

        var company = await _companyRepository.GetByIdAsync(dto.CompanyId, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{dto.CompanyId}' not found.");

        var ledger = await _ledgerRepository.GetByIdAsync(dto.CompanyId, dto.LedgerId, cancellationToken);
        if (ledger == null)
            throw new DomainException($"Ledger with ID '{dto.LedgerId}' not found in company.");

        if (!ledger.BillWiseEnabled)
            throw new DomainException($"Ledger '{ledger.Name}' does not have bill-by-bill tracking enabled.");

        var trimmedRef = dto.ReferenceNumber.Trim();
        var exists = transaction != null
            ? await _billReferenceRepository.ExistsByReferenceNumberAsync(dto.CompanyId, dto.LedgerId, trimmedRef, null, transaction, cancellationToken)
            : await _billReferenceRepository.ExistsByReferenceNumberAsync(dto.CompanyId, dto.LedgerId, trimmedRef, null, cancellationToken);
        if (exists)
            throw new DomainException($"Bill reference '{trimmedRef}' already exists for ledger '{ledger.Name}'.");

        if (dto.SourceVoucherLineId.HasValue)
        {
            var lineWithCompany = transaction != null
                ? await _voucherRepository.GetLineWithCompanyAsync(dto.SourceVoucherLineId.Value, transaction, cancellationToken)
                : await _voucherRepository.GetLineWithCompanyAsync(dto.SourceVoucherLineId.Value, cancellationToken);
            if (lineWithCompany == null || lineWithCompany.Value.CompanyId != dto.CompanyId)
                throw new DomainException("Source voucher line not found or belongs to a different company.");

            if (lineWithCompany.Value.Line.LedgerId != dto.LedgerId)
                throw new DomainException("Source voucher line ledger does not match bill reference ledger.");
        }

        var bill = BillReference.Create(
            dto.CompanyId,
            dto.LedgerId,
            trimmedRef,
            dto.ReferenceType,
            dto.BillDate,
            new Money(dto.OriginalAmount),
            dto.EntryType,
            dto.DueDate,
            dto.SourceVoucherLineId);

        var now = _dateTimeProvider.UtcNow;
        var user = _currentUserService.UserName;
        bill.CreatedAt = now;
        bill.CreatedBy = user;

        if (transaction != null)
            await _billReferenceRepository.AddAsync(bill, transaction, cancellationToken);
        else
            await _billReferenceRepository.AddAsync(bill, cancellationToken);

        return MapToDto(bill);
    }

    public async Task<BillAllocationDto> AllocateBillAsync(CreateBillAllocationDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.VoucherLineId == Guid.Empty)
            throw new DomainException("VoucherLineId cannot be empty.");

        if (dto.BillReferenceId == Guid.Empty)
            throw new DomainException("BillReferenceId cannot be empty.");

        if (dto.AllocatedAmount <= 0)
            throw new DomainException("AllocatedAmount must be greater than zero.");

        var bill = await _billReferenceRepository.GetByIdAsync(dto.CompanyId, dto.BillReferenceId, cancellationToken);
        if (bill == null)
            throw new DomainException($"BillReference with ID '{dto.BillReferenceId}' not found in company.");

        if (bill.Status != BillReferenceStatus.Active)
            throw new DomainException($"Cannot allocate against bill '{bill.ReferenceNumber}' because it is in '{bill.Status}' status.");

        var lineWithCompany = await _voucherRepository.GetLineWithCompanyAsync(dto.VoucherLineId, cancellationToken);
        if (lineWithCompany == null)
            throw new DomainException($"VoucherLine with ID '{dto.VoucherLineId}' not found.");

        var (line, lineCompanyId) = lineWithCompany.Value;

        // Allocation Identity Invariant (Application / Domain Layer Ownership)
        if (dto.CompanyId != bill.CompanyId || dto.CompanyId != lineCompanyId)
            throw new DomainException("Cross-company allocation is prohibited. VoucherLine, BillReference, and Allocation must belong to the same company.");

        if (bill.LedgerId != line.LedgerId)
            throw new DomainException("Cross-ledger allocation is prohibited. Settlement line party does not match bill reference party.");

        // Accounting Direction Invariant
        if (bill.EntryType == DebitCreditType.Debit && line.Type != DebitCreditType.Credit)
            throw new DomainException("Allocation direction mismatch. A Debit (receivable) bill can only be settled by a Credit line.");

        if (bill.EntryType == DebitCreditType.Credit && line.Type != DebitCreditType.Debit)
            throw new DomainException("Allocation direction mismatch. A Credit (payable) bill can only be settled by a Debit line.");

        // State-Dependent Outstanding Limit Validation
        var allocatedMoney = new Money(dto.AllocatedAmount);
        var outstanding = await _billReferenceRepository.GetOutstandingBalanceAsync(dto.CompanyId, bill.Id, cancellationToken);
        if (allocatedMoney > outstanding)
            throw new DomainException($"Allocated amount ({dto.AllocatedAmount:F2}) exceeds current outstanding balance ({outstanding.Amount:F2}) for bill '{bill.ReferenceNumber}'.");

        // Voucher Line Capacity Validation
        var existingAllocations = await _billAllocationRepository.GetByVoucherLineIdAsync(dto.CompanyId, dto.VoucherLineId, cancellationToken);
        var currentLineAllocatedTotal = existingAllocations.Aggregate(Money.Zero(), (acc, a) => acc + a.AllocatedAmount);
        if (currentLineAllocatedTotal + allocatedMoney > line.Amount)
            throw new DomainException($"Total allocated amount cannot exceed the voucher line amount ({line.Amount.Amount:F2}).");

        var now = _dateTimeProvider.UtcNow;
        var user = _currentUserService.UserName;

        var allocation = BillAllocation.Create(
            dto.CompanyId,
            dto.VoucherLineId,
            dto.BillReferenceId,
            allocatedMoney,
            user,
            now);

        await _billAllocationRepository.AddAsync(allocation, cancellationToken);

        return MapToDto(allocation);
    }

    public async Task<IReadOnlyList<PendingBillDto>> GetPendingBillsAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
    {
        var billsWithBalance = await _billReferenceRepository.GetActiveBillsByLedgerAsync(companyId, ledgerId, cancellationToken);
        return billsWithBalance.Select(b => new PendingBillDto(
            MapToDto(b.Bill),
            b.TotalAllocated.Amount,
            b.OutstandingAmount.Amount)).ToList();
    }

    public async Task<BillReferenceDto?> GetBillByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var bill = await _billReferenceRepository.GetByIdAsync(companyId, id, cancellationToken);
        return bill == null ? null : MapToDto(bill);
    }

    public async Task<IReadOnlyList<BillAllocationDto>> GetAllocationsForVoucherLineAsync(Guid companyId, Guid voucherLineId, CancellationToken cancellationToken = default)
    {
        var allocations = await _billAllocationRepository.GetByVoucherLineIdAsync(companyId, voucherLineId, cancellationToken);
        return allocations.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<BillAllocationDto>> GetAllocationsForBillAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default)
    {
        var allocations = await _billAllocationRepository.GetByBillReferenceIdAsync(companyId, billReferenceId, cancellationToken);
        return allocations.Select(MapToDto).ToList();
    }

    public async Task CancelBillReferenceAsync(Guid companyId, Guid billReferenceId, CancellationToken cancellationToken = default)
    {
        var bill = await _billReferenceRepository.GetByIdAsync(companyId, billReferenceId, cancellationToken);
        if (bill == null)
            throw new DomainException($"BillReference with ID '{billReferenceId}' not found.");

        var totalAllocated = await _billReferenceRepository.GetTotalAllocatedAsync(companyId, billReferenceId, cancellationToken);
        if (totalAllocated.Amount > 0)
            throw new DomainException($"Cannot cancel bill '{bill.ReferenceNumber}' because it has active settlement allocations. Cancel settlement vouchers first.");

        bill.Cancel(_currentUserService.UserName, _dateTimeProvider.UtcNow);
        await _billReferenceRepository.UpdateAsync(bill, cancellationToken);
    }

    private static BillReferenceDto MapToDto(BillReference bill)
    {
        return new BillReferenceDto(
            bill.Id,
            bill.CompanyId,
            bill.LedgerId,
            bill.ReferenceNumber,
            bill.ReferenceType,
            bill.Status,
            bill.BillDate,
            bill.DueDate,
            bill.OriginalAmount.Amount,
            bill.EntryType,
            bill.SourceVoucherLineId,
            bill.CreatedAt,
            bill.CreatedBy,
            bill.ModifiedAt,
            bill.ModifiedBy);
    }

    private static BillAllocationDto MapToDto(BillAllocation allocation)
    {
        return new BillAllocationDto(
            allocation.Id,
            allocation.CompanyId,
            allocation.VoucherLineId,
            allocation.BillReferenceId,
            allocation.AllocatedAmount.Amount,
            allocation.CreatedAt,
            allocation.CreatedBy);
    }
}
