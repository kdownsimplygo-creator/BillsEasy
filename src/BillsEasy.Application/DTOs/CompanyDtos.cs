using System;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.DTOs;

public record CompanyDto(
    Guid Id,
    string CompanyName,
    string PrintName,
    string? ShortName,
    string Country,
    string StateName,
    string StateCode,
    DateOnly FinancialYearBeginning,
    DateOnly BooksBeginningFrom,
    string CurrencySymbol,
    string CurrencyFormalName,
    string? GSTIN,
    GSTRegistrationType GSTRegistrationType,
    bool IsSecurityEnabled,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy
);

public record FinancialYearDto(
    Guid Id,
    Guid CompanyId,
    string YearName,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    bool IsLocked,
    DateTime CreatedAt,
    string CreatedBy
);

public record IndianStateDto(string StateName, string StateCode);
