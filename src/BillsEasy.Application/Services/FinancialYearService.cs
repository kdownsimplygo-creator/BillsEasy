using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Services;

public class FinancialYearService
{
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FinancialYearService(
        IFinancialYearRepository financialYearRepository,
        ICompanyRepository companyRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _financialYearRepository = financialYearRepository;
        _companyRepository = companyRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<FinancialYearDto> CreateFinancialYearAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        string? customName = null,
        bool isActive = false,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{companyId}' not found.");

        var existingFys = await _financialYearRepository.GetByCompanyIdAsync(companyId, cancellationToken);
        var newFy = FinancialYear.Create(companyId, startDate, endDate, customName, isActive);

        foreach (var existing in existingFys)
        {
            if (existing.OverlapsWith(newFy))
            {
                throw new DomainException(
                    $"Financial Year '{newFy.YearName}' ({newFy.StartDate:yyyy-MM-dd} to {newFy.EndDate:yyyy-MM-dd}) overlaps with existing Financial Year '{existing.YearName}' ({existing.StartDate:yyyy-MM-dd} to {existing.EndDate:yyyy-MM-dd}).");
            }
        }

        newFy.CreatedAt = _dateTimeProvider.UtcNow;
        newFy.CreatedBy = _currentUserService.UserId;

        await _financialYearRepository.AddAsync(newFy, cancellationToken);
        return MapToDto(newFy);
    }

    public async Task<IReadOnlyList<FinancialYearDto>> GetFinancialYearsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var list = await _financialYearRepository.GetByCompanyIdAsync(companyId, cancellationToken);
        return list.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task SelectActiveFinancialYearAsync(
        Guid companyId,
        Guid financialYearId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _financialYearRepository.GetByIdAsync(financialYearId, cancellationToken);
        if (existing == null || existing.CompanyId != companyId)
            throw new DomainException($"Financial Year with ID '{financialYearId}' not found for company '{companyId}'.");

        await _financialYearRepository.SetActiveAsync(companyId, financialYearId, cancellationToken);
    }

    private static FinancialYearDto MapToDto(FinancialYear fy) => new(
        fy.Id,
        fy.CompanyId,
        fy.YearName,
        fy.StartDate,
        fy.EndDate,
        fy.IsActive,
        fy.IsLocked,
        fy.CreatedAt,
        fy.CreatedBy
    );
}
