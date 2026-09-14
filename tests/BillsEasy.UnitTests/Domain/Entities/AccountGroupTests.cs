using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class AccountGroupTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsAccountGroup()
    {
        var group = AccountGroup.Create(
            _companyId,
            "Sundry Debtors",
            GroupNature.Asset,
            parentGroupId: null,
            isPrimary: true,
            isDefaultSystemGroup: false);

        group.Should().NotBeNull();
        group.Id.Should().NotBeEmpty();
        group.CompanyId.Should().Be(_companyId);
        group.Name.Should().Be("Sundry Debtors");
        group.Nature.Should().Be(GroupNature.Asset);
        group.ParentGroupId.Should().BeNull();
        group.IsPrimary.Should().BeTrue();
        group.IsDefaultSystemGroup.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNullName_ThrowsDomainException(string? invalidName)
    {
        Action act = () => AccountGroup.Create(
            _companyId,
            invalidName!,
            GroupNature.Asset);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => AccountGroup.Create(
            Guid.Empty,
            "Valid Name",
            GroupNature.Asset);

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Constructor_WhenParentGroupIdEqualsId_ThrowsDomainException()
    {
        var id = Guid.NewGuid();
        Action act = () => new AccountGroup(
            id,
            _companyId,
            "Self Parent Group",
            GroupNature.Expense,
            parentGroupId: id);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be its own parent*");
    }

    [Fact]
    public void Reparent_WithValidParent_InheritsNatureAndSetsParentId()
    {
        var parent = AccountGroup.Create(_companyId, "Current Assets", GroupNature.Asset, isPrimary: true);
        var group = AccountGroup.Create(_companyId, "Bank Accounts", GroupNature.Liability, isPrimary: true);
        var hierarchy = new[] { parent, group };

        group.Reparent(parent, hierarchy);

        group.ParentGroupId.Should().Be(parent.Id);
        group.Nature.Should().Be(GroupNature.Asset);
        group.IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void Reparent_WhenParentFromDifferentCompany_ThrowsDomainException()
    {
        var parent = AccountGroup.Create(Guid.NewGuid(), "Other Company Group", GroupNature.Asset);
        var group = AccountGroup.Create(_companyId, "My Group", GroupNature.Asset);
        var hierarchy = new[] { parent, group };

        Action act = () => group.Reparent(parent, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*Parent account group must belong to the same company*");
    }

    [Fact]
    public void Reparent_WhenSelfParenting_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "My Group", GroupNature.Asset);
        var hierarchy = new[] { group };

        Action act = () => group.Reparent(group, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be its own parent*");
    }

    [Fact]
    public void Reparent_WhenCircularHierarchyDetected_ThrowsDomainException()
    {
        // A (Root) -> B (Child of A) -> C (Child of B)
        // Attempting to make A a child of C must throw!
        var groupA = AccountGroup.Create(_companyId, "Group A", GroupNature.Asset, isPrimary: true);
        var groupB = AccountGroup.Create(_companyId, "Group B", GroupNature.Asset, parentGroupId: groupA.Id);
        var groupC = AccountGroup.Create(_companyId, "Group C", GroupNature.Asset, parentGroupId: groupB.Id);
        var hierarchy = new[] { groupA, groupB, groupC };

        Action act = () => groupA.Reparent(groupC, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*circular hierarchy detected*");
    }

    [Fact]
    public void Reparent_WhenNullParent_SetsRootAndPrimary()
    {
        var parentId = Guid.NewGuid();
        var group = AccountGroup.Create(_companyId, "Sub Group", GroupNature.Asset, parentGroupId: parentId);
        var hierarchy = new[] { group };

        group.Reparent(null, hierarchy);

        group.ParentGroupId.Should().BeNull();
        group.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void Update_WhenModifyingProtectedSystemGroupNature_ThrowsDomainException()
    {
        var sysGroup = AccountGroup.Create(_companyId, "Fixed Assets", GroupNature.Asset, isDefaultSystemGroup: true);
        var hierarchy = new[] { sysGroup };

        Action act = () => sysGroup.Update("Fixed Assets", null, hierarchy, GroupNature.Liability);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot change the nature of protected system group*");
    }

    [Fact]
    public void MethodSignatures_RequireExplicitCompanyGroupsParameter_WithNoDefaultValue()
    {
        var reparentMethod = typeof(AccountGroup).GetMethod(nameof(AccountGroup.Reparent));
        reparentMethod.Should().NotBeNull();
        var companyGroupsParam1 = Array.Find(reparentMethod!.GetParameters(), p => p.Name == "companyGroups");
        companyGroupsParam1.Should().NotBeNull();
        companyGroupsParam1!.HasDefaultValue.Should().BeFalse("companyGroups parameter in Reparent must be mandatory with no default value");

        var updateMethod = typeof(AccountGroup).GetMethod(nameof(AccountGroup.Update));
        updateMethod.Should().NotBeNull();
        var companyGroupsParam2 = Array.Find(updateMethod!.GetParameters(), p => p.Name == "companyGroups");
        companyGroupsParam2.Should().NotBeNull();
        companyGroupsParam2!.HasDefaultValue.Should().BeFalse("companyGroups parameter in Update must be mandatory with no default value");
    }

    [Fact]
    public void SetPrimary_WhenHasParent_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Sub Group", GroupNature.Asset, parentGroupId: Guid.NewGuid());

        Action act = () => group.SetPrimary(true);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be designated as a Primary Root group*");
    }

    [Fact]
    public void Reparent_WhenAncestorNotFoundInCompanyGroups_ThrowsDomainException()
    {
        var groupA = AccountGroup.Create(_companyId, "Group A", GroupNature.Asset, isPrimary: true);
        var missingParentId = Guid.NewGuid();
        var groupB = AccountGroup.Create(_companyId, "Group B", GroupNature.Asset, parentGroupId: missingParentId);
        
        // Incomplete collection missing missingParentId
        var incompleteHierarchy = new[] { groupA, groupB };

        Action act = () => groupA.Reparent(groupB, incompleteHierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*Incomplete account group hierarchy supplied*");
    }

    [Fact]
    public void Reparent_WhenTargetParentHasDifferentNatureAndGroupHasDescendants_ThrowsDomainException()
    {
        var assetRoot = AccountGroup.Create(_companyId, "Assets", GroupNature.Asset, isPrimary: true);
        var subAsset = AccountGroup.Create(_companyId, "Sub Assets", GroupNature.Asset, parentGroupId: assetRoot.Id);
        var childAsset = AccountGroup.Create(_companyId, "Child Assets", GroupNature.Asset, parentGroupId: subAsset.Id);
        var liabilityRoot = AccountGroup.Create(_companyId, "Liabilities", GroupNature.Liability, isPrimary: true);

        var hierarchy = new[] { assetRoot, subAsset, childAsset, liabilityRoot };

        // Attempting to reparent subAsset (which has childAsset descendant) under liabilityRoot must fail!
        Action act = () => subAsset.Reparent(liabilityRoot, hierarchy);

        act.Should().Throw<DomainException>()
            .WithMessage("*because it has child sub-groups*");
    }

    [Fact]
    public void Reparent_WhenLeafGroupReparentedToDifferentNature_SucceedsAndDerivesParentNature()
    {
        var assetRoot = AccountGroup.Create(_companyId, "Assets", GroupNature.Asset, isPrimary: true);
        var leaf = AccountGroup.Create(_companyId, "Misc Group", GroupNature.Asset, parentGroupId: assetRoot.Id);
        var liabilityRoot = AccountGroup.Create(_companyId, "Liabilities", GroupNature.Liability, isPrimary: true);

        var hierarchy = new[] { assetRoot, leaf, liabilityRoot };

        // Leaf group has no descendants, reparenting to Liabilities should succeed and change nature to Liability
        leaf.Reparent(liabilityRoot, hierarchy);

        leaf.ParentGroupId.Should().Be(liabilityRoot.Id);
        leaf.Nature.Should().Be(GroupNature.Liability);
    }

    [Fact]
    public void Update_WhenRootGroupChangesNatureWithDescendants_ThrowsDomainException()
    {
        var root = AccountGroup.Create(_companyId, "Custom Root", GroupNature.Asset, isPrimary: true);
        var child = AccountGroup.Create(_companyId, "Custom Child", GroupNature.Asset, parentGroupId: root.Id);

        var hierarchy = new[] { root, child };

        Action act = () => root.Update("Custom Root", null, hierarchy, GroupNature.Expense);

        act.Should().Throw<DomainException>()
            .WithMessage("*because it has child sub-groups*");
    }

    [Fact]
    public void Update_WhenLeafRootGroupChangesNature_Succeeds()
    {
        var root = AccountGroup.Create(_companyId, "Custom Root", GroupNature.Asset, isPrimary: true);
        var hierarchy = new[] { root };

        root.Update("Custom Root Renamed", null, hierarchy, GroupNature.Income);

        root.Nature.Should().Be(GroupNature.Income);
        root.Name.Should().Be("Custom Root Renamed");
    }
}