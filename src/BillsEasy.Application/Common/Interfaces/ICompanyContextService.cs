using System;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface ICompanyContextService
{
    Company? ActiveCompany { get; }
    FinancialYear? ActiveFinancialYear { get; }
    bool HasActiveCompany => ActiveCompany != null;

    event EventHandler? ActiveCompanyChanged;

    void SetActiveContext(Company company, FinancialYear? financialYear = null);
    void SetActiveFinancialYear(FinancialYear financialYear);
    void CloseActiveCompany();
}
