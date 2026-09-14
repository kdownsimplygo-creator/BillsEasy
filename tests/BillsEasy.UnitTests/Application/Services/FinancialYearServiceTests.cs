using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class FinancialYearServiceTests
{
    private readonly IFinancialYearRepository _fyRepo = Substitute.For<IFinancialYearRepository>();
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly FinancialYearService _service;

    public FinancialYearServiceTests()
    {
        _userService.UserId.Returns("admin");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));

        _service = new FinancialYearService(_fyRepo, _companyRepo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateFinancialYear_WhenCompanyDoesNotExist_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        _companyRepo.GetByIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns((Company?)null);

        var act = async () => await _service.CreateFinancialYearAsync(
            companyId, new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateFinancialYear_WhenOverlapsWithExisting_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        var company = Company.Create("Co", "Co", null, "Gujarat", "24", new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));
        _companyRepo.GetByIdAsync(companyId, Arg.Any<CancellationToken>()).Returns(company);

        var existingFy = FinancialYear.Create(companyId, new DateOnly(2024, 4, 1), new DateOnly(2025, 3, 31), "2024-2025");
        _fyRepo.GetByCompanyIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(new List<FinancialYear> { existingFy });

        // Overlapping dates
        var act = async () => await _service.CreateFinancialYearAsync(
            companyId, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*overlaps with existing Financial Year*");
    }
}
