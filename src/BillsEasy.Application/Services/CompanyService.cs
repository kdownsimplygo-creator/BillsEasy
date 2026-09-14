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

public class CompanyService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAccountGroupService? _accountGroupService;
    private readonly ILedgerService? _ledgerService;

    public CompanyService(
        ICompanyRepository companyRepository,
        IFinancialYearRepository financialYearRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IAccountGroupService? accountGroupService = null,
        ILedgerService? ledgerService = null)
    {
        _companyRepository = companyRepository;
        _financialYearRepository = financialYearRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _accountGroupService = accountGroupService;
        _ledgerService = ledgerService;
    }

    public async Task<CompanyDto> CreateCompanyAsync(
        string companyName,
        string printName,
        string? shortName,
        string stateName,
        string stateCode,
        DateOnly financialYearBeginning,
        DateOnly booksBeginningFrom,
        string? gstin = null,
        GSTRegistrationType gstRegistrationType = GSTRegistrationType.Regular,
        string country = "India",
        string currencySymbol = "₹",
        string currencyFormalName = "INR",
        bool isSecurityEnabled = false,
        CancellationToken cancellationToken = default)
    {
        if (await _companyRepository.ExistsByNameAsync(companyName, null, cancellationToken))
            throw new DomainException($"A company with name '{companyName}' already exists.");

        var company = Company.Create(
            companyName,
            printName,
            shortName,
            stateName,
            stateCode,
            financialYearBeginning,
            booksBeginningFrom,
            gstin,
            gstRegistrationType,
            country,
            currencySymbol,
            currencyFormalName,
            isSecurityEnabled);

        company.CreatedAt = _dateTimeProvider.UtcNow;
        company.CreatedBy = _currentUserService.UserId;

        foreach (var fy in company.FinancialYears)
        {
            fy.CreatedAt = _dateTimeProvider.UtcNow;
            fy.CreatedBy = _currentUserService.UserId;
        }

        await _companyRepository.AddAsync(company, cancellationToken);

        if (_accountGroupService != null)
        {
            await _accountGroupService.SeedDefaultGroupsAsync(company.Id, _currentUserService.UserId, cancellationToken);
        }

        if (_ledgerService != null)
        {
            await _ledgerService.SeedDefaultLedgersAsync(company.Id, _currentUserService.UserId, cancellationToken);
        }

        return MapToDto(company);
    }

    public async Task<CompanyDto> UpdateCompanyAsync(
        Guid companyId,
        string companyName,
        string printName,
        string? shortName,
        string stateName,
        string stateCode,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{companyId}' not found.");

        if (await _companyRepository.ExistsByNameAsync(companyName, companyId, cancellationToken))
            throw new DomainException($"Another company with name '{companyName}' already exists.");

        company.Update(
            companyName,
            printName,
            shortName,
            stateName,
            stateCode,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);

        company.ModifiedAt = _dateTimeProvider.UtcNow;
        company.ModifiedBy = _currentUserService.UserId;

        await _companyRepository.UpdateAsync(company, cancellationToken);
        return MapToDto(company);
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(id, cancellationToken);
        return company == null ? null : MapToDto(company);
    }

    public async Task<IReadOnlyList<CompanyDto>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _companyRepository.GetAllAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task DeleteCompanyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(id, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{id}' not found.");

        if (await _companyRepository.HasTransactionsAsync(id, cancellationToken))
            throw new DomainException($"Cannot delete company '{company.CompanyName}' because it has one or more recorded financial transactions / vouchers.");

        await _companyRepository.DeleteAsync(id, cancellationToken);
    }

    private static CompanyDto MapToDto(Company company)
    {
        return new CompanyDto(
            company.Id,
            company.CompanyName,
            company.PrintName,
            company.ShortName,
            company.Country,
            company.StateName,
            company.StateCode,
            company.FinancialYearBeginning,
            company.BooksBeginningFrom,
            company.CurrencySymbol,
            company.CurrencyFormalName,
            company.GSTIN,
            company.GSTRegistrationType,
            company.IsSecurityEnabled,
            company.CreatedAt,
            company.CreatedBy,
            company.ModifiedAt,
            company.ModifiedBy);
    }
}