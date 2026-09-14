using System;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class CompanyServiceTests
{
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly IFinancialYearRepository _fyRepo = Substitute.For<IFinancialYearRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly CompanyService _service;

    public CompanyServiceTests()
    {
        _userService.UserId.Returns("admin");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));

        _service = new CompanyService(_companyRepo, _fyRepo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateCompany_WhenNameAlreadyExists_ThrowsDomainException()
    {
        _companyRepo.ExistsByNameAsync("Existing Co", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = async () => await _service.CreateCompanyAsync(
            "Existing Co", "Existing Co", null, "Gujarat", "24",
            new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1));

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");

        await _companyRepo.DidNotReceive().AddAsync(Arg.Any<Company>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCompany_WithValidData_SetsAuditFieldsAndPersists()
    {
        _companyRepo.ExistsByNameAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _service.CreateCompanyAsync(
            "New Enterprise", "New Enterprise", "NE", "Gujarat", "24",
            new DateOnly(2024, 4, 1), new DateOnly(2024, 4, 1),
            gstin: "24AAACN1234A1Z5");

        result.Should().NotBeNull();
        result.CompanyName.Should().Be("New Enterprise");
        result.CreatedBy.Should().Be("admin");
        result.CreatedAt.Should().Be(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));

        await _companyRepo.Received(1).AddAsync(
            Arg.Is<Company>(c => c.CompanyName == "New Enterprise" && c.CreatedBy == "admin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCompanyAsync_WhenCompanyHasTransactions_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        var company = Company.Create(
            "Tx Co", "Tx Co", "TC", "Gujarat", "24",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 1));

        _companyRepo.GetByIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(company);
        _companyRepo.HasTransactionsAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = async () => await _service.DeleteCompanyAsync(companyId);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Cannot delete company 'Tx Co' because it has one or more recorded financial transactions*");

        await _companyRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCompanyAsync_WhenCompanyHasNoTransactions_DeletesSuccessfully()
    {
        var companyId = Guid.NewGuid();
        var company = Company.Create(
            "Empty Co", "Empty Co", "EC", "Gujarat", "24",
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 1));

        _companyRepo.GetByIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(company);
        _companyRepo.HasTransactionsAsync(companyId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.DeleteCompanyAsync(companyId);

        await _companyRepo.Received(1).DeleteAsync(companyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCompanyAsync_WhenCompanyNotFound_ThrowsDomainException()
    {
        var companyId = Guid.NewGuid();
        _companyRepo.GetByIdAsync(companyId, Arg.Any<CancellationToken>())
            .Returns((Company?)null);

        var act = async () => await _service.DeleteCompanyAsync(companyId);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");

        await _companyRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
