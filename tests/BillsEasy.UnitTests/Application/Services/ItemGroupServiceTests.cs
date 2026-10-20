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

public class ItemGroupServiceTests
{
    private readonly IItemGroupRepository _repo = Substitute.For<IItemGroupRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly ItemGroupService _service;
    private readonly Guid _companyId = Guid.NewGuid();

    public ItemGroupServiceTests()
    {
        _userService.UserName.Returns("test_user");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        _service = new ItemGroupService(_repo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateGroup_WithValidData_PersistsAndReturnsDto()
    {
        _repo.ExistsByNameAsync(_companyId, "Monitors", null, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateItemGroupDto(_companyId, "Monitors", null, "Display monitors");
        var result = await _service.CreateGroupAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Monitors");
        result.Description.Should().Be("Display monitors");
        result.CreatedBy.Should().Be("test_user");

        await _repo.Received(1).AddAsync(Arg.Is<ItemGroup>(g => g.Name == "Monitors"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WhenDuplicateNameUnderSameParent_ThrowsDomainException()
    {
        _repo.ExistsByNameAsync(_companyId, "Monitors", null, null, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new CreateItemGroupDto(_companyId, "Monitors");
        var act = async () => await _service.CreateGroupAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateGroup_WhenParentGroupNotFound_ThrowsDomainException()
    {
        var parentId = Guid.NewGuid();
        _repo.ExistsByNameAsync(_companyId, "LED Monitors", parentId, null, Arg.Any<CancellationToken>())
            .Returns(false);
        _repo.GetByIdAsync(_companyId, parentId, Arg.Any<CancellationToken>())
            .Returns((ItemGroup?)null);

        var dto = new CreateItemGroupDto(_companyId, "LED Monitors", parentId);
        var act = async () => await _service.CreateGroupAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteGroup_WhenGroupHasChildren_ThrowsDomainException()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        _repo.GetByIdAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(group);
        _repo.HasChildrenAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = async () => await _service.DeleteGroupAsync(_companyId, group.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*has one or more sub-groups*");
    }

    [Fact]
    public async Task DeleteGroup_WhenGroupHasReferencingItems_ThrowsDomainException()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        _repo.GetByIdAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(group);
        _repo.HasChildrenAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(false);
        _repo.HasReferencingItemsAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = async () => await _service.DeleteGroupAsync(_companyId, group.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*referenced by one or more items*");
    }

    [Fact]
    public async Task DeleteGroup_WhenNotReferenced_DeletesSuccessfully()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        _repo.GetByIdAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(group);
        _repo.HasChildrenAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(false);
        _repo.HasReferencingItemsAsync(_companyId, group.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.DeleteGroupAsync(_companyId, group.Id);

        await _repo.Received(1).DeleteAsync(_companyId, group.Id, Arg.Any<CancellationToken>());
    }
}
