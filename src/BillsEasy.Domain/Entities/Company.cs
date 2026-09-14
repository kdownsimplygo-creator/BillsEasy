using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Root organizational aggregate representing a business entity.
/// </summary>
public class Company : AuditableEntity<Guid>
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled);

    public string CompanyName { get; private set; } = string.Empty;
    public string PrintName { get; private set; } = string.Empty;
    public string? ShortName { get; private set; }
    public string Country { get; private set; } = "India";
    public string StateName { get; private set; } = string.Empty;
    public string StateCode { get; private set; } = string.Empty;
    public DateOnly FinancialYearBeginning { get; private set; }
    public DateOnly BooksBeginningFrom { get; private set; }
    public string CurrencySymbol { get; private set; } = "₹";
    public string CurrencyFormalName { get; private set; } = "INR";
    public string? GSTIN { get; private set; }
    public GSTRegistrationType GSTRegistrationType { get; private set; } = GSTRegistrationType.Regular;
    public bool IsSecurityEnabled { get; private set; }

    private readonly List<FinancialYear> _financialYears = new();
    public IReadOnlyCollection<FinancialYear> FinancialYears => _financialYears.AsReadOnly();

    private Company() : base()
    {
    }

    public Company(
        Guid id,
        string companyName,
        string printName,
        string? shortName,
        string country,
        string stateName,
        string stateCode,
        DateOnly financialYearBeginning,
        DateOnly booksBeginningFrom,
        string currencySymbol,
        string currencyFormalName,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled) : base(id)
    {
        ValidateAndSetProperties(
            companyName,
            printName,
            shortName,
            country,
            stateName,
            stateCode,
            financialYearBeginning,
            booksBeginningFrom,
            currencySymbol,
            currencyFormalName,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);
    }

    public static Company Create(
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
        bool isSecurityEnabled = false)
    {
        var company = new Company(
            Guid.NewGuid(),
            companyName,
            printName,
            shortName,
            country,
            stateName,
            stateCode,
            financialYearBeginning,
            booksBeginningFrom,
            currencySymbol,
            currencyFormalName,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);

        // Auto-create initial financial year (1 year duration from FinancialYearBeginning)
        var fyEnd = financialYearBeginning.AddYears(1).AddDays(-1);
        var initialFy = FinancialYear.Create(company.Id, financialYearBeginning, fyEnd, isActive: true);
        company.AddFinancialYear(initialFy);

        return company;
    }

    public void Update(
        string companyName,
        string printName,
        string? shortName,
        string stateName,
        string stateCode,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled)
    {
        ValidateAndSetProperties(
            companyName,
            printName,
            shortName,
            Country,
            stateName,
            stateCode,
            FinancialYearBeginning,
            BooksBeginningFrom,
            CurrencySymbol,
            CurrencyFormalName,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);
    }

    public void AddFinancialYear(FinancialYear financialYear)
    {
        if (financialYear == null)
            throw new ArgumentNullException(nameof(financialYear));

        if (financialYear.CompanyId != Id)
            throw new DomainException($"FinancialYear CompanyId ({financialYear.CompanyId}) does not match Company Id ({Id}).");

        foreach (var existing in _financialYears)
        {
            if (existing.Id != financialYear.Id && existing.OverlapsWith(financialYear))
            {
                throw new DomainException(
                    $"Financial Year '{financialYear.YearName}' ({financialYear.StartDate:yyyy-MM-dd} to {financialYear.EndDate:yyyy-MM-dd}) overlaps with existing Financial Year '{existing.YearName}' ({existing.StartDate:yyyy-MM-dd} to {existing.EndDate:yyyy-MM-dd}).");
            }
        }

        if (financialYear.IsActive)
        {
            foreach (var existing in _financialYears)
            {
                existing.SetActive(false);
            }
        }

        _financialYears.Add(financialYear);
    }

    public void SetActiveFinancialYear(Guid financialYearId)
    {
        var target = _financialYears.FirstOrDefault(f => f.Id == financialYearId);
        if (target == null)
            throw new DomainException($"Financial Year with Id '{financialYearId}' not found in Company '{CompanyName}'.");

        foreach (var fy in _financialYears)
        {
            fy.SetActive(fy.Id == financialYearId);
        }
    }

    private void ValidateAndSetProperties(
        string companyName,
        string printName,
        string? shortName,
        string country,
        string stateName,
        string stateCode,
        DateOnly financialYearBeginning,
        DateOnly booksBeginningFrom,
        string currencySymbol,
        string currencyFormalName,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new DomainException("Company name cannot be empty.");

        if (string.IsNullOrWhiteSpace(stateName))
            throw new DomainException("State name cannot be empty.");

        if (string.IsNullOrWhiteSpace(stateCode))
            throw new DomainException("State code cannot be empty.");

        if (booksBeginningFrom < financialYearBeginning)
            throw new DomainException($"Books beginning date ({booksBeginningFrom:yyyy-MM-dd}) cannot be earlier than Financial year beginning date ({financialYearBeginning:yyyy-MM-dd}).");

        var cleanGstin = string.IsNullOrWhiteSpace(gstin) ? null : gstin.Trim().ToUpperInvariant();
        if (cleanGstin != null)
        {
            if (cleanGstin.Length != 15 || !GstinRegex.IsMatch(cleanGstin))
                throw new DomainException($"Invalid GSTIN format: '{cleanGstin}'. Must be a 15-character statutory alphanumeric code.");
        }

        CompanyName = companyName.Trim();
        PrintName = string.IsNullOrWhiteSpace(printName) ? CompanyName : printName.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? "India" : country.Trim();
        StateName = stateName.Trim();
        StateCode = stateCode.Trim();
        FinancialYearBeginning = financialYearBeginning;
        BooksBeginningFrom = booksBeginningFrom;
        CurrencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? "₹" : currencySymbol.Trim();
        CurrencyFormalName = string.IsNullOrWhiteSpace(currencyFormalName) ? "INR" : currencyFormalName.Trim().ToUpperInvariant();
        GSTIN = cleanGstin;
        GSTRegistrationType = gstRegistrationType;
        IsSecurityEnabled = isSecurityEnabled;
    }
}
