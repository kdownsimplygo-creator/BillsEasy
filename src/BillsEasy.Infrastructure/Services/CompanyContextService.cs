using System;
using System.Linq;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Infrastructure.Services;

public class CompanyContextService : ICompanyContextService
{
    public Company? ActiveCompany { get; private set; }
    public FinancialYear? ActiveFinancialYear { get; private set; }

    public event EventHandler? ActiveCompanyChanged;

    public void SetActiveContext(Company company, FinancialYear? financialYear = null)
    {
        ActiveCompany = company ?? throw new ArgumentNullException(nameof(company));
        ActiveFinancialYear = financialYear ?? company.FinancialYears.FirstOrDefault(f => f.IsActive) ?? company.FinancialYears.FirstOrDefault();
        ActiveCompanyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetActiveFinancialYear(FinancialYear financialYear)
    {
        if (ActiveCompany == null)
            throw new InvalidOperationException("Cannot set active financial year when no company is open.");

        if (financialYear.CompanyId != ActiveCompany.Id)
            throw new InvalidOperationException("Financial year does not belong to active company.");

        ActiveFinancialYear = financialYear;
        ActiveCompany.SetActiveFinancialYear(financialYear.Id);
        ActiveCompanyChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CloseActiveCompany()
    {
        ActiveCompany = null;
        ActiveFinancialYear = null;
        ActiveCompanyChanged?.Invoke(this, EventArgs.Empty);
    }
}
