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

namespace BillsEasy.Application.Services;

public class LedgerService : ILedgerService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IAccountGroupRepository _accountGroupRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LedgerService(
        ILedgerRepository ledgerRepository,
        IAccountGroupRepository accountGroupRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _ledgerRepository = ledgerRepository;
        _accountGroupRepository = accountGroupRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<LedgerDto> CreateLedgerAsync(CreateLedgerDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.GroupId == Guid.Empty)
            throw new DomainException("GroupId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Ledger name cannot be empty.");

        var group = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.GroupId, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{dto.GroupId}' not found.");

        if (group.CompanyId != dto.CompanyId)
            throw new DomainException("Account group must belong to the same company.");

        var trimmedName = dto.Name.Trim();
        if (await _ledgerRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, null, cancellationToken))
            throw new DomainException($"A ledger with name '{trimmedName}' already exists in this company.");

        var ledger = Ledger.Create(
            dto.CompanyId,
            dto.GroupId,
            trimmedName,
            dto.PrintName,
            dto.Alias,
            dto.OpeningBalance,
            dto.OpeningBalanceType,
            dto.BillWiseEnabled,
            dto.CreditPeriodDays,
            dto.CreditLimit,
            dto.AddressLine1,
            dto.AddressLine2,
            dto.City,
            dto.StateName,
            dto.StateCode,
            dto.PinCode,
            dto.Country,
            dto.GSTIN,
            dto.GSTRegistrationType,
            dto.PAN,
            dto.AadhaarNumber,
            dto.ContactPerson,
            dto.MobileNumber,
            dto.WhatsAppNumber,
            dto.Email,
            dto.BankName,
            dto.BankAccountNumber,
            dto.BankIFSC,
            dto.BankBranch,
            isDefaultSystemLedger: false);

        ledger.CreatedAt = _dateTimeProvider.UtcNow;
        ledger.CreatedBy = _currentUserService.UserId;

        await _ledgerRepository.AddAsync(ledger, cancellationToken);

        return MapToDto(ledger, group);
    }

    public async Task<LedgerDto> UpdateLedgerAsync(UpdateLedgerDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.GroupId == Guid.Empty)
            throw new DomainException("GroupId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Ledger name cannot be empty.");

        var ledger = await _ledgerRepository.GetByIdAsync(dto.CompanyId, dto.Id, cancellationToken);
        if (ledger == null)
            throw new DomainException($"Ledger with ID '{dto.Id}' not found.");

        var group = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.GroupId, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{dto.GroupId}' not found.");

        if (group.CompanyId != dto.CompanyId)
            throw new DomainException("Account group must belong to the same company.");

        var trimmedName = dto.Name.Trim();
        if (await _ledgerRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.Id, cancellationToken))
            throw new DomainException($"Another ledger with name '{trimmedName}' already exists in this company.");

        var hasTransactions = await _ledgerRepository.HasTransactionsAsync(dto.CompanyId, dto.Id, cancellationToken);

        ledger.Update(
            dto.GroupId,
            trimmedName,
            hasTransactions,
            dto.PrintName,
            dto.Alias,
            dto.OpeningBalance,
            dto.OpeningBalanceType,
            dto.BillWiseEnabled,
            dto.CreditPeriodDays,
            dto.CreditLimit,
            dto.AddressLine1,
            dto.AddressLine2,
            dto.City,
            dto.StateName,
            dto.StateCode,
            dto.PinCode,
            dto.Country,
            dto.GSTIN,
            dto.GSTRegistrationType,
            dto.PAN,
            dto.AadhaarNumber,
            dto.ContactPerson,
            dto.MobileNumber,
            dto.WhatsAppNumber,
            dto.Email,
            dto.BankName,
            dto.BankAccountNumber,
            dto.BankIFSC,
            dto.BankBranch);

        ledger.ModifiedAt = _dateTimeProvider.UtcNow;
        ledger.ModifiedBy = _currentUserService.UserId;

        await _ledgerRepository.UpdateAsync(ledger, cancellationToken);

        return MapToDto(ledger, group);
    }

    public async Task DeleteLedgerAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (ledger == null)
            throw new DomainException($"Ledger with ID '{id}' not found.");

        if (ledger.IsDefaultSystemLedger)
            throw new DomainException($"System predefined ledger '{ledger.Name}' cannot be deleted.");

        if (await _ledgerRepository.HasTransactionsAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete ledger '{ledger.Name}' because it has one or more recorded financial transactions.");

        if (await _ledgerRepository.HasBillReferencesAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete ledger '{ledger.Name}' because it has one or more bill-wise references.");

        await _ledgerRepository.DeleteAsync(companyId, id, cancellationToken);
    }

    public async Task<LedgerDto?> GetLedgerByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (ledger == null) return null;

        var group = await _accountGroupRepository.GetByIdAsync(companyId, ledger.GroupId, cancellationToken);
        return MapToDto(ledger, group);
    }

    public async Task<IReadOnlyList<LedgerDto>> GetAllLedgersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var ledgers = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        return ledgers.Select(l =>
        {
            groupDict.TryGetValue(l.GroupId, out var grp);
            return MapToDto(l, grp);
        }).OrderBy(l => l.Name).ToList();
    }

    public async Task<IReadOnlyList<LedgerDto>> GetLedgersByGroupAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var ledgers = await _ledgerRepository.GetByGroupIdAsync(companyId, groupId, cancellationToken);
        var group = await _accountGroupRepository.GetByIdAsync(companyId, groupId, cancellationToken);

        return ledgers.Select(l => MapToDto(l, group)).OrderBy(l => l.Name).ToList();
    }

    public async Task<IReadOnlyList<LedgerDto>> SearchLedgersAsync(Guid companyId, string searchTerm, CancellationToken cancellationToken = default)
    {
        var all = await GetAllLedgersAsync(companyId, cancellationToken);
        if (string.IsNullOrWhiteSpace(searchTerm))
            return all;

        var query = searchTerm.Trim().ToLowerInvariant();
        return all.Where(l =>
            l.Name.ToLowerInvariant().Contains(query) ||
            l.PrintName.ToLowerInvariant().Contains(query) ||
            (l.Alias != null && l.Alias.ToLowerInvariant().Contains(query)) ||
            l.GroupName.ToLowerInvariant().Contains(query) ||
            (l.GSTIN != null && l.GSTIN.ToLowerInvariant().Contains(query)) ||
            (l.City != null && l.City.ToLowerInvariant().Contains(query)) ||
            (l.ContactPerson != null && l.ContactPerson.ToLowerInvariant().Contains(query)) ||
            (l.MobileNumber != null && l.MobileNumber.Contains(query))
        ).ToList();
    }

    public async Task<IReadOnlyList<LedgerLookupDto>> GetLookupLedgersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var ledgers = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        return ledgers.Select(l =>
        {
            groupDict.TryGetValue(l.GroupId, out var grp);
            return new LedgerLookupDto(
                l.Id,
                l.Name,
                l.PrintName,
                l.GroupId,
                grp?.Name ?? "Unknown Group",
                grp?.Nature ?? GroupNature.Asset,
                l.BillWiseEnabled,
                l.IsDefaultSystemLedger);
        }).OrderBy(l => l.Name).ToList();
    }

    public async Task SeedDefaultLedgersAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default)
    {
        var existing = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        if (existing.Count > 0)
        {
            return; // Already seeded
        }

        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        if (groups.Count == 0)
        {
            return; // Groups must exist before ledgers can be seeded
        }

        var cashGroup = groups.FirstOrDefault(g => g.Name.Equals("Cash-in-hand", StringComparison.OrdinalIgnoreCase))
                     ?? groups.FirstOrDefault(g => g.Name.Equals("Current Assets", StringComparison.OrdinalIgnoreCase))
                     ?? groups.FirstOrDefault(g => g.Nature == GroupNature.Asset);

        var plGroup = groups.FirstOrDefault(g => g.Name.Equals("Capital Account", StringComparison.OrdinalIgnoreCase))
                   ?? groups.FirstOrDefault(g => g.Name.Equals("Reserves & Surplus", StringComparison.OrdinalIgnoreCase))
                   ?? groups.FirstOrDefault(g => g.Nature == GroupNature.Capital);

        var now = _dateTimeProvider.UtcNow;
        var seededLedgers = new List<Ledger>();

        if (cashGroup != null)
        {
            var cashLedger = Ledger.Create(
                companyId,
                cashGroup.Id,
                "Cash",
                printName: "Cash Account",
                openingBalance: 0m,
                openingBalanceType: DebitCreditType.Debit,
                billWiseEnabled: false,
                isDefaultSystemLedger: true);

            cashLedger.CreatedAt = now;
            cashLedger.CreatedBy = createdBy;
            seededLedgers.Add(cashLedger);
        }

        if (plGroup != null)
        {
            var plLedger = Ledger.Create(
                companyId,
                plGroup.Id,
                "Profit & Loss Account",
                printName: "Profit & Loss Account",
                openingBalance: 0m,
                openingBalanceType: DebitCreditType.Credit,
                billWiseEnabled: false,
                isDefaultSystemLedger: true);

            plLedger.CreatedAt = now;
            plLedger.CreatedBy = createdBy;
            seededLedgers.Add(plLedger);
        }

        if (seededLedgers.Count > 0)
        {
            await _ledgerRepository.AddRangeAsync(seededLedgers, cancellationToken);
        }
    }

    private static LedgerDto MapToDto(Ledger ledger, AccountGroup? group)
    {
        var groupName = group?.Name ?? "Unknown Group";
        var groupNature = group?.Nature ?? GroupNature.Asset;
        var opBalDisplay = ledger.OpeningBalance.Amount > 0
            ? $"₹{ledger.OpeningBalance.Amount:N2} {ledger.OpeningBalanceType}"
            : "₹0.00";

        return new LedgerDto(
            ledger.Id,
            ledger.CompanyId,
            ledger.GroupId,
            groupName,
            groupNature,
            groupNature.ToString(),
            ledger.Name,
            ledger.PrintName,
            ledger.Alias,
            ledger.OpeningBalance.Amount,
            ledger.OpeningBalanceType,
            opBalDisplay,
            ledger.BillWiseEnabled,
            ledger.CreditPeriodDays,
            ledger.CreditLimit?.Amount,
            ledger.AddressLine1,
            ledger.AddressLine2,
            ledger.City,
            ledger.StateName,
            ledger.StateCode,
            ledger.PinCode,
            ledger.Country,
            ledger.GSTIN,
            ledger.GSTRegistrationType,
            ledger.PAN,
            ledger.AadhaarNumber,
            ledger.ContactPerson,
            ledger.MobileNumber,
            ledger.WhatsAppNumber,
            ledger.Email,
            ledger.BankName,
            ledger.BankAccountNumber,
            ledger.BankIFSC,
            ledger.BankBranch,
            ledger.IsDefaultSystemLedger,
            ledger.CreatedAt,
            ledger.CreatedBy,
            ledger.ModifiedAt,
            ledger.ModifiedBy);
    }
}