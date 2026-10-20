using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class UnitServiceTests
{
    private readonly IUnitRepository _repo = Substitute.For<IUnitRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly UnitService _service;
    private readonly Guid _companyId = Guid.NewGuid();

    public UnitServiceTests()
    {
        _userService.UserName.Returns("test_user");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        _service = new UnitService(_repo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateUnit_WithValidData_PersistsAndReturnsDto()
    {
        _repo.ExistsByNameAsync(_companyId, "Pcs", null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateUnitDto(_companyId, "Pcs", "Pieces", 0);
        var result = await _service.CreateUnitAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Pcs");
        result.PrintName.Should().Be("Pieces");
        result.DecimalPlaces.Should().Be(0);
        result.CreatedBy.Should().Be("test_user");

        await _repo.Received(1).AddAsync(Arg.Is<Unit>(u => u.Name == "Pcs"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUnit_WhenDuplicateNameExists_ThrowsDomainException()
    {
        _repo.ExistsByNameAsync(_companyId, "Pcs", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new CreateUnitDto(_companyId, "Pcs");
        var act = async () => await _service.CreateUnitAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateUnit_WithValidData_UpdatesAndReturnsDto()
    {
        var existingUnit = Unit.Create(_companyId, "Pcs", "Pieces", 0);
        _repo.GetByIdAsync(_companyId, existingUnit.Id, Arg.Any<CancellationToken>())
            .Returns(existingUnit);
        _repo.ExistsByNameAsync(_companyId, "Box", existingUnit.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new UpdateUnitDto(existingUnit.Id, _companyId, "Box", "Boxes", 2);
        var result = await _service.UpdateUnitAsync(dto);

        result.Name.Should().Be("Box");
        result.PrintName.Should().Be("Boxes");
        result.DecimalPlaces.Should().Be(2);

        await _repo.Received(1).UpdateAsync(Arg.Is<Unit>(u => u.Name == "Box"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUnit_WhenReferencedByItems_ThrowsDomainException()
    {
        var existingUnit = Unit.Create(_companyId, "Pcs", "Pieces", 0);
        _repo.GetByIdAsync(_companyId, existingUnit.Id, Arg.Any<CancellationToken>())
            .Returns(existingUnit);
        _repo.HasReferencingItemsAsync(_companyId, existingUnit.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = async () => await _service.DeleteUnitAsync(_companyId, existingUnit.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*referenced by one or more items*");
    }

    [Fact]
    public async Task DeleteUnit_WhenNotReferenced_DeletesSuccessfully()
    {
        var existingUnit = Unit.Create(_companyId, "Pcs", "Pieces", 0);
        _repo.GetByIdAsync(_companyId, existingUnit.Id, Arg.Any<CancellationToken>())
            .Returns(existingUnit);
        _repo.HasReferencingItemsAsync(_companyId, existingUnit.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.DeleteUnitAsync(_companyId, existingUnit.Id);

        await _repo.Received(1).DeleteAsync(_companyId, existingUnit.Id, Arg.Any<CancellationToken>());
    }
}
