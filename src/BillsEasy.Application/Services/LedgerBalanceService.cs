using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.Services;

/// <summary>
/// Authoritative read-only application service computing general ledger running balances
/// and executive dashboard financial positions from ledger masters and posted vouchers.
/// </summary>
public class LedgerBalanceService : ILedgerBalanceService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountGroupRepository _accountGroupRepository;

    public LedgerBalanceService(
        ILedgerRepository ledgerRepository,
        IVoucherRepository voucherRepository,
        IAccountGroupRepository accountGroupRepository)
    {
        _ledgerRepository = ledgerRepository ?? throw new ArgumentNullException(nameof(ledgerRepository));
        _voucherRepository = voucherRepository ?? throw new ArgumentNullException(nameof(voucherRepository));
        _accountGroupRepository = accountGroupRepository ?? throw new ArgumentNullException(nameof(accountGroupRepository));
    }

    public async Task<decimal> GetLedgerBalanceAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(companyId, ledgerId, cancellationToken);
        if (ledger == null)
            return 0m;

        var vouchers = await _voucherRepository.GetAllAsync(companyId, ledgerId: ledgerId, cancellationToken: cancellationToken);

        decimal balance = 0m;
        if (ledger.OpeningBalance.Amount > 0)
        {
            balance = ledger.OpeningBalanceType == DebitCreditType.Debit
                ? ledger.OpeningBalance.Amount
                : -ledger.OpeningBalance.Amount;
        }

        foreach (var voucher in vouchers)
        {
            foreach (var line in voucher.Lines.Where(l => l.LedgerId == ledgerId))
            {
                if (line.Type == DebitCreditType.Debit)
                    balance += line.Amount.Amount;
                else
                    balance -= line.Amount.Amount;
            }
        }

        return balance;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetAllLedgerBalancesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var ledgers = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        var vouchers = await _voucherRepository.GetAllAsync(companyId, cancellationToken: cancellationToken);

        var balances = new Dictionary<Guid, decimal>();
        foreach (var ledger in ledgers)
        {
            decimal op = 0m;
            if (ledger.OpeningBalance.Amount > 0)
            {
                op = ledger.OpeningBalanceType == DebitCreditType.Debit
                    ? ledger.OpeningBalance.Amount
                    : -ledger.OpeningBalance.Amount;
            }
            balances[ledger.Id] = op;
        }

        foreach (var voucher in vouchers)
        {
            foreach (var line in voucher.Lines)
            {
                if (balances.ContainsKey(line.LedgerId))
                {
                    if (line.Type == DebitCreditType.Debit)
                        balances[line.LedgerId] += line.Amount.Amount;
                    else
                        balances[line.LedgerId] -= line.Amount.Amount;
                }
            }
        }

        return balances;
    }

    public async Task<DashboardFinancialSummaryDto> GetDashboardFinancialSummaryAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var ledgers = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        var balances = await GetAllLedgerBalancesAsync(companyId, cancellationToken);

        var cashGroupIds = groups
            .Where(g => g.Name.Equals("Cash-in-Hand", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Id)
            .ToHashSet();

        var bankGroupIds = groups
            .Where(g => g.Name.Equals("Bank Accounts", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Id)
            .ToHashSet();

        var debtorGroupIds = groups
            .Where(g => g.Name.Equals("Sundry Debtors", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Id)
            .ToHashSet();

        var creditorGroupIds = groups
            .Where(g => g.Name.Equals("Sundry Creditors", StringComparison.OrdinalIgnoreCase))
            .Select(g => g.Id)
            .ToHashSet();

        decimal cash = 0m;
        decimal bank = 0m;
        decimal receivables = 0m;
        decimal payables = 0m;

        foreach (var ledger in ledgers)
        {
            var bal = balances.GetValueOrDefault(ledger.Id, 0m);

            if (cashGroupIds.Contains(ledger.GroupId) || ledger.Name.Equals("Cash", StringComparison.OrdinalIgnoreCase))
            {
                cash += bal;
            }
            else if (bankGroupIds.Contains(ledger.GroupId))
            {
                bank += bal;
            }
            else if (debtorGroupIds.Contains(ledger.GroupId))
            {
                if (bal > 0)
                    receivables += bal;
                else
                    payables += Math.Abs(bal);
            }
            else if (creditorGroupIds.Contains(ledger.GroupId))
            {
                if (bal < 0)
                    payables += Math.Abs(bal);
                else
                    receivables += bal;
            }
        }

        return new DashboardFinancialSummaryDto(
            CashBalance: Math.Abs(cash),
            CashBalanceType: cash >= 0 ? "Dr" : "Cr",
            BankBalance: Math.Abs(bank),
            BankBalanceType: bank >= 0 ? "Dr" : "Cr",
            ReceivablesBalance: receivables,
            ReceivablesBalanceType: "Dr",
            PayablesBalance: payables,
            PayablesBalanceType: "Cr");
    }
}
