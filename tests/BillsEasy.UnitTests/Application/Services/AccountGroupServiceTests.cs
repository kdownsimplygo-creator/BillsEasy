using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class AccountGroupServiceTests
{
    private readonly IAccountGroupRepository _repo = Substitute.For<IAccountGroupRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly AccountGroupService _service;
    private readonly Guid _companyId = Guid.NewGuid();

    public AccountGroupServiceTests()
    {
        _userService.UserId.Returns("test_user");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        _service = new AccountGroupService(_repo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateGroup_WithValidData_PersistsAndReturnsDto()
    {
        _repo.ExistsByNameAsync(_companyId, "Custom Debtors", null, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateAccountGroupDto(_companyId, "Custom Debtors", null, GroupNature.Asset);
        var result = await _service.CreateGroupAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Custom Debtors");
        result.Nature.Should().Be(GroupNature.Asset);
        result.CreatedBy.Should().Be("test_user");

        await _repo.Received(1).AddAsync(Arg.Is<AccountGroup>(g => g.Name == "Custom Debtors"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WhenDuplicateSiblingNameExists_ThrowsDomainException()
    {
        _repo.ExistsByNameAsync(_companyId, "Duplicate Group", null, null, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new CreateAccountGroupDto(_companyId, "Duplicate Group", null, GroupNature.Asset);
        var act = async () => await _service.CreateGroupAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists under the selected parent group*");

        await _repo.DidNotReceive().AddAsync(Arg.Any<AccountGroup>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WhenSameNameUnderDifferentParent_IsAllowed()
    {
        var parent1 = Guid.NewGuid();
        var parentGroup1 = AccountGroup.Create(_companyId, "Current Liabilities", GroupNature.Liability, isPrimary: true);

        _repo.GetByIdAsync(_companyId, parent1, Arg.Any<CancellationToken>())
            .Returns(parentGroup1);
        _repo.ExistsByNameAsync(_companyId, "Outstanding Expenses", parent1, null, Arg.Any<CancellationToken>())
            .Returns(false); // Sibling check passes

        var dto = new CreateAccountGroupDto(_companyId, "Outstanding Expenses", parent1);
        var result = await _service.CreateGroupAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Outstanding Expenses");
        result.Nature.Should().Be(GroupNature.Liability);
        await _repo.Received(1).AddAsync(Arg.Is<AccountGroup>(g => g.Name == "Outstanding Expenses"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateGroup_WithParentGroup_InheritsParentNature()
    {
        var parentId = Guid.NewGuid();
        var parentGroup = AccountGroup.Create(_companyId, "Fixed Assets", GroupNature.Asset, isPrimary: true);

        _repo.GetByIdAsync(_companyId, parentId, Arg.Any<CancellationToken>())
            .Returns(parentGroup);
        _repo.ExistsByNameAsync(_companyId, "Computers", parentId, null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateAccountGroupDto(_companyId, "Computers", parentId, GroupNature.Expense);
        var result = await _service.CreateGroupAsync(dto);

        result.Nature.Should().Be(GroupNature.Asset); // Inherited from parent Fixed Assets
    }

    [Fact]
    public async Task CreateGroup_WhenParentBelongsToDifferentCompany_ThrowsDomainException()
    {
        var parentId = Guid.NewGuid();
        var parentGroup = AccountGroup.Create(Guid.NewGuid(), "Other Co Group", GroupNature.Asset);

        _repo.GetByIdAsync(_companyId, parentId, Arg.Any<CancellationToken>())
            .Returns(parentGroup);

        var dto = new CreateAccountGroupDto(_companyId, "Invalid Sub Group", parentId);
        var act = async () => await _service.CreateGroupAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*must belong to the same company*");
    }

    [Fact]
    public async Task UpdateGroup_WhenCircularHierarchyDetected_ThrowsDomainException()
    {
        // Hierarchy: Root(A) -> Child(B) -> SubChild(C)
        var rootA = AccountGroup.Create(_companyId, "Group A", GroupNature.Asset, isPrimary: true);
        var childB = AccountGroup.Create(_companyId, "Group B", GroupNature.Asset, parentGroupId: rootA.Id);
        var subChildC = AccountGroup.Create(_companyId, "Group C", GroupNature.Asset, parentGroupId: childB.Id);

        _repo.GetByIdAsync(_companyId, rootA.Id, Arg.Any<CancellationToken>()).Returns(rootA);
        _repo.GetByIdAsync(_companyId, subChildC.Id, Arg.Any<CancellationToken>()).Returns(subChildC);
        _repo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup> { rootA, childB, subChildC });

        // Attempt to make Root(A)'s parent SubChild(C) -> creating cycle A -> B -> C -> A
        var updateDto = new UpdateAccountGroupDto(rootA.Id, _companyId, "Group A", subChildC.Id);
        var act = async () => await _service.UpdateGroupAsync(updateDto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*circular hierarchy detected*");
    }

    [Fact]
    public async Task DeleteGroup_WhenProtectedSystemGroup_ThrowsDomainException()
    {
        var sysGroup = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset, isDefaultSystemGroup: true);
        _repo.GetByIdAsync(_companyId, sysGroup.Id, Arg.Any<CancellationToken>()).Returns(sysGroup);

        var act = async () => await _service.DeleteGroupAsync(_companyId, sysGroup.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*System predefined account group*cannot be deleted*");

        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGroup_WhenHasChildren_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Parent Group", GroupNature.Asset);
        _repo.GetByIdAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(group);
        _repo.HasChildrenAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteGroupAsync(_companyId, group.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more child sub-groups*");

        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteGroup_WhenHasLedgers_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Active Group", GroupNature.Asset);
        _repo.GetByIdAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(group);
        _repo.HasChildrenAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(false);
        _repo.HasLedgersAsync(_companyId, group.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteGroupAsync(_companyId, group.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more ledgers assigned*");

        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedDefaultGroups_WhenNoGroupsExist_SeedsTaxonomy()
    {
        _repo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup>());

        await _service.SeedDefaultGroupsAsync(_companyId, "admin");

        await _repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<AccountGroup>>(list => list.Any(g => g.Name == "Sundry Debtors") &&
                                                      list.Any(g => g.Name == "Sundry Creditors") &&
                                                      list.Any(g => g.Name == "Bank Accounts") &&
                                                      list.Any(g => g.Name == "Fixed Assets") &&
                                                      list.Any(g => g.Name == "Direct Expenses") &&
                                                      list.Any(g => g.Name == "Sales Accounts")),
            Arg.Any<CancellationToken>());
    }
}