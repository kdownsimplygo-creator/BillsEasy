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

public class PostingService : IPostingService
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBillReferenceRepository? _billReferenceRepository;
    private readonly IBillAllocationRepository? _billAllocationRepository;

    public PostingService(
        IVoucherRepository voucherRepository,
        ICompanyRepository companyRepository,
        IFinancialYearRepository financialYearRepository,
        ILedgerRepository ledgerRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IBillReferenceRepository? billReferenceRepository = null,
        IBillAllocationRepository? billAllocationRepository = null)
    {
        _voucherRepository = voucherRepository;
        _companyRepository = companyRepository;
        _financialYearRepository = financialYearRepository;
        _ledgerRepository = ledgerRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _billReferenceRepository = billReferenceRepository;
        _billAllocationRepository = billAllocationRepository;
    }

    public Task<VoucherDto> PostVoucherAsync(CreateVoucherDto dto, CancellationToken cancellationToken = default)
        => PostVoucherAsync(dto, null, cancellationToken);

    public async Task<VoucherDto> PostVoucherAsync(CreateVoucherDto dto, System.Data.Common.DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.FinancialYearId == Guid.Empty)
            throw new DomainException("FinancialYearId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.VoucherNumber))
            throw new DomainException("Voucher number cannot be empty.");

        if (dto.Lines == null || dto.Lines.Count < 2)
            throw new DomainException("Voucher must contain at least 2 lines.");

        var company = await _companyRepository.GetByIdAsync(dto.CompanyId, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{dto.CompanyId}' not found.");

        var financialYear = await _financialYearRepository.GetByIdAsync(dto.FinancialYearId, cancellationToken);
        if (financialYear == null)
            throw new DomainException($"Financial year with ID '{dto.FinancialYearId}' not found.");

        if (financialYear.CompanyId != dto.CompanyId)
            throw new DomainException("Voucher CompanyId does not match FinancialYear CompanyId.");

        if (financialYear.IsLocked)
            throw new DomainException($"Financial year '{financialYear.YearName}' is locked. Posting is prohibited.");

        if (!financialYear.Contains(dto.VoucherDate))
            throw new DomainException($"Voucher date {dto.VoucherDate:yyyy-MM-dd} is outside financial year '{financialYear.YearName}' ({financialYear.StartDate:yyyy-MM-dd} to {financialYear.EndDate:yyyy-MM-dd}).");

        var trimmedNumber = dto.VoucherNumber.Trim();
        var exists = transaction != null
            ? await _voucherRepository.ExistsByNumberAsync(dto.CompanyId, dto.FinancialYearId, trimmedNumber, null, transaction, cancellationToken)
            : await _voucherRepository.ExistsByNumberAsync(dto.CompanyId, dto.FinancialYearId, trimmedNumber, null, cancellationToken);
        if (exists)
            throw new DomainException($"Voucher number '{trimmedNumber}' already exists in financial year '{financialYear.YearName}'.");

        var ledgerNames = new Dictionary<Guid, string>();
        foreach (var line in dto.Lines)
        {
            if (line.LedgerId == Guid.Empty)
                throw new DomainException("LedgerId cannot be empty.");

            if (line.Amount <= 0)
                throw new DomainException("Voucher line amount must be greater than zero.");

            if (!ledgerNames.ContainsKey(line.LedgerId))
            {
                var ledger = await _ledgerRepository.GetByIdAsync(dto.CompanyId, line.LedgerId, cancellationToken);
                if (ledger == null)
                    throw new DomainException($"Ledger with ID '{line.LedgerId}' not found in company.");

                if (ledger.CompanyId != dto.CompanyId)
                    throw new DomainException("All ledgers must belong to the same company as the voucher.");

                ledgerNames[line.LedgerId] = ledger.Name;
            }
        }

        var voucher = Voucher.Create(
            dto.CompanyId,
            dto.FinancialYearId,
            trimmedNumber,
            dto.Type,
            dto.VoucherDate,
            dto.ReferenceNumber,
            dto.Narration);

        foreach (var line in dto.Lines)
        {
            voucher.AddLine(line.LedgerId, line.Type, new Money(line.Amount), line.Narration);
        }

        var now = _dateTimeProvider.UtcNow;
        var user = _currentUserService.UserName;

        voucher.Post(financialYear, postedBy: user, postedAt: now);
        voucher.CreatedAt = now;
        voucher.CreatedBy = user;

        if (transaction != null)
            await _voucherRepository.AddAsync(voucher, transaction, cancellationToken);
        else
            await _voucherRepository.AddAsync(voucher, cancellationToken);

        return MapToDto(voucher, ledgerNames);
    }

    public async Task<VoucherDto> PostExistingVoucherAsync(Guid companyId, Guid voucherId, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(companyId, voucherId, cancellationToken);
        if (voucher == null)
            throw new DomainException($"Voucher with ID '{voucherId}' not found.");

        var financialYear = await _financialYearRepository.GetByIdAsync(voucher.FinancialYearId, cancellationToken);
        if (financialYear == null)
            throw new DomainException($"Financial year with ID '{voucher.FinancialYearId}' not found.");

        var now = _dateTimeProvider.UtcNow;
        var user = _currentUserService.UserName;

        voucher.Post(financialYear, postedBy: user, postedAt: now);
        await _voucherRepository.UpdateAsync(voucher, cancellationToken);

        return await LoadDtoWithLedgerNamesAsync(voucher, cancellationToken);
    }

    public async Task<VoucherDto> CancelVoucherAsync(Guid companyId, Guid voucherId, string? cancellationReason = null, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(companyId, voucherId, cancellationToken);
        if (voucher == null)
            throw new DomainException($"Voucher with ID '{voucherId}' not found.");

        if (_billReferenceRepository != null && _billAllocationRepository != null)
        {
            // 1. Originating Voucher check: If any line is a source for a bill reference
            foreach (var line in voucher.Lines)
            {
                var bill = await _billReferenceRepository.GetBySourceVoucherLineIdAsync(companyId, line.Id, cancellationToken);
                if (bill != null && bill.Status == BillReferenceStatus.Active)
                {
                    var totalAllocated = await _billReferenceRepository.GetTotalAllocatedAsync(companyId, bill.Id, cancellationToken);
                    if (totalAllocated.Amount > 0)
                    {
                        throw new DomainException($"Cannot cancel voucher because bill '{bill.ReferenceNumber}' has active settlement allocations. Cancel settlement vouchers first.");
                    }

                    // Soft-cancel the bill
                    bill.Cancel(_currentUserService.UserName, _dateTimeProvider.UtcNow);
                    await _billReferenceRepository.UpdateAsync(bill, cancellationToken);
                }
            }

            // 2. Settlement Voucher check: Delete allocations associated with its voucher lines
            foreach (var line in voucher.Lines)
            {
                await _billAllocationRepository.DeleteByVoucherLineIdAsync(companyId, line.Id, cancellationToken);
            }
        }

        voucher.Cancel(cancelledBy: _currentUserService.UserName);
        await _voucherRepository.UpdateAsync(voucher, cancellationToken);

        return await LoadDtoWithLedgerNamesAsync(voucher, cancellationToken);
    }

    public async Task<VoucherDto?> GetVoucherByIdAsync(Guid companyId, Guid voucherId, CancellationToken cancellationToken = default)
    {
        var voucher = await _voucherRepository.GetByIdAsync(companyId, voucherId, cancellationToken);
        if (voucher == null)
            return null;

        return await LoadDtoWithLedgerNamesAsync(voucher, cancellationToken);
    }

    public async Task<IReadOnlyList<VoucherDto>> GetVouchersAsync(VoucherFilterDto filter, CancellationToken cancellationToken = default)
    {
        var vouchers = await _voucherRepository.GetAllAsync(
            filter.CompanyId,
            filter.FinancialYearId,
            filter.Type,
            filter.Status,
            filter.FromDate,
            filter.ToDate,
            filter.LedgerId,
            cancellationToken);

        var ledgers = await _ledgerRepository.GetAllAsync(filter.CompanyId, cancellationToken);
        var ledgerNames = ledgers.ToDictionary(l => l.Id, l => l.Name);

        return vouchers.Select(v => MapToDto(v, ledgerNames)).ToList();
    }

    private async Task<VoucherDto> LoadDtoWithLedgerNamesAsync(Voucher voucher, CancellationToken cancellationToken)
    {
        var ledgerNames = new Dictionary<Guid, string>();
        foreach (var line in voucher.Lines)
        {
            if (!ledgerNames.ContainsKey(line.LedgerId))
            {
                var ledger = await _ledgerRepository.GetByIdAsync(voucher.CompanyId, line.LedgerId, cancellationToken);
                if (ledger != null)
                {
                    ledgerNames[line.LedgerId] = ledger.Name;
                }
            }
        }

        return MapToDto(voucher, ledgerNames);
    }

    private static VoucherDto MapToDto(Voucher voucher, IReadOnlyDictionary<Guid, string> ledgerNames)
    {
        var lines = voucher.Lines.Select(l => new VoucherLineDto(
            l.Id,
            l.VoucherId,
            l.LedgerId,
            ledgerNames.TryGetValue(l.LedgerId, out var name) ? name : null,
            l.SequenceNumber,
            l.Type,
            l.Amount.Amount,
            l.Narration
        )).ToList();

        return new VoucherDto(
            voucher.Id,
            voucher.CompanyId,
            voucher.FinancialYearId,
            voucher.VoucherNumber,
            voucher.Type,
            voucher.Status,
            voucher.VoucherDate,
            voucher.ReferenceNumber,
            voucher.Narration,
            voucher.TotalAmount.Amount,
            lines,
            voucher.CreatedAt,
            voucher.CreatedBy,
            voucher.PostedAt,
            voucher.PostedBy
        );
    }
}
