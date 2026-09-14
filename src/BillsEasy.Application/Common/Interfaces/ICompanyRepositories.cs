using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string companyName, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task UpdateAsync(Company company, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IFinancialYearRepository
{
    Task<IReadOnlyList<FinancialYear>> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<FinancialYear?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FinancialYear?> GetActiveFinancialYearAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task AddAsync(FinancialYear financialYear, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid companyId, Guid financialYearId, CancellationToken cancellationToken = default);
}
