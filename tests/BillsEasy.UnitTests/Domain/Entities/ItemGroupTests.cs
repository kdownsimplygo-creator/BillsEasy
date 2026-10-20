using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class ItemGroupTests
{
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsItemGroup()
    {
        var group = ItemGroup.Create(_companyId, "Electronics", null, "All electronic items");

        group.Should().NotBeNull();
        group.Id.Should().NotBeEmpty();
        group.CompanyId.Should().Be(_companyId);
        group.Name.Should().Be("Electronics");
        group.ParentGroupId.Should().BeNull();
        group.Description.Should().Be("All electronic items");
        group.IsDefaultSystemGroup.Should().BeFalse();
    }

    [Fact]
    public void Create_WithParentGroup_SetsParentId()
    {
        var parentId = Guid.NewGuid();
        var group = ItemGroup.Create(_companyId, "Monitors", parentId);

        group.ParentGroupId.Should().Be(parentId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidName_ThrowsDomainException(string? invalidName)
    {
        Action act = () => ItemGroup.Create(_companyId, invalidName!);

        act.Should().Throw<DomainException>()
            .WithMessage("*name is required*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => ItemGroup.Create(Guid.Empty, "Electronics");

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_SelfParent_ThrowsDomainException()
    {
        var id = Guid.NewGuid();
        Action act = () => new ItemGroup(id, _companyId, "Invalid", id);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be its own parent*");
    }

    [Fact]
    public void Update_SelfParent_ThrowsDomainException()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        Action act = () => group.Update("Updated", group.Id, null);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be its own parent*");
    }

    [Fact]
    public void Update_WithValidValues_UpdatesProperties()
    {
        var group = ItemGroup.Create(_companyId, "Electronics", null, "Desc 1");
        var newParentId = Guid.NewGuid();
        group.Update("Smartphones", newParentId, "Desc 2");

        group.Name.Should().Be("Smartphones");
        group.ParentGroupId.Should().Be(newParentId);
        group.Description.Should().Be("Desc 2");
    }
}
