using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface ILedgerRepository
{
    Task<Ledger?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<Ledger?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ledger>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionsAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default);
    Task<bool> HasBillReferencesAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default);
    Task AddAsync(Ledger ledger, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Ledger> ledgers, CancellationToken cancellationToken = default);
    Task UpdateAsync(Ledger ledger, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}