using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

/// <summary>
/// Authoritative read-only application service providing closing ledger balances and dashboard financial summaries.
/// </summary>
public interface ILedgerBalanceService
{
    Task<decimal> GetLedgerBalanceAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, decimal>> GetAllLedgerBalancesAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<DashboardFinancialSummaryDto> GetDashboardFinancialSummaryAsync(Guid companyId, CancellationToken cancellationToken = default);
}
