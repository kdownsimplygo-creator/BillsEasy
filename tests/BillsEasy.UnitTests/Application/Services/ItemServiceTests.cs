using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class ItemServiceTests
{
    private readonly IItemRepository _itemRepo = Substitute.For<IItemRepository>();
    private readonly IItemGroupRepository _groupRepo = Substitute.For<IItemGroupRepository>();
    private readonly IUnitRepository _unitRepo = Substitute.For<IUnitRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly ItemService _service;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _unitId = Guid.NewGuid();

    public ItemServiceTests()
    {
        _userService.UserName.Returns("test_user");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        _service = new ItemService(_itemRepo, _groupRepo, _unitRepo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateItem_WithValidData_PersistsAndReturnsDto()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        var unit = Unit.Create(_companyId, "Pcs");

        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>())
            .Returns(group);
        _unitRepo.GetByIdAsync(_companyId, _unitId, Arg.Any<CancellationToken>())
            .Returns(unit);
        _itemRepo.ExistsByNameAsync(_companyId, "Samsung M25", null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateItemDto(
            _companyId,
            _groupId,
            _unitId,
            "Samsung M25",
            OpeningStockQuantity: 10,
            OpeningStockRate: 18000,
            SalesPrice: 20000,
            PurchasePrice: 17500,
            MRP: 22000,
            MinLevel: 2,
            ReorderLevel: 5);

        var result = await _service.CreateItemAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Samsung M25");
        result.OpeningStockQuantity.Should().Be(10);
        result.OpeningStockRate.Amount.Should().Be(18000);
        result.OpeningStockValue.Amount.Should().Be(180000); // auto-calculated when value is 0
        result.SalesPrice!.Value.Amount.Should().Be(20000);
        result.CreatedBy.Should().Be("test_user");

        await _itemRepo.Received(1).AddAsync(Arg.Is<Item>(i => i.Name == "Samsung M25"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateItem_WithExplicitOpeningStockValue_PreservesValue()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        var unit = Unit.Create(_companyId, "Pcs");

        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>())
            .Returns(group);
        _unitRepo.GetByIdAsync(_companyId, _unitId, Arg.Any<CancellationToken>())
            .Returns(unit);
        _itemRepo.ExistsByNameAsync(_companyId, "Samsung M25", null, Arg.Any<CancellationToken>())
            .Returns(false);

        var dto = new CreateItemDto(
            _companyId,
            _groupId,
            _unitId,
            "Samsung M25",
            OpeningStockQuantity: 10,
            OpeningStockRate: 18000,
            OpeningStockValue: 179500);

        var result = await _service.CreateItemAsync(dto);

        result.OpeningStockValue.Amount.Should().Be(179500);
    }

    [Fact]
    public async Task CreateItem_WhenGroupNotFound_ThrowsDomainException()
    {
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>())
            .Returns((ItemGroup?)null);

        var dto = new CreateItemDto(_companyId, _groupId, _unitId, "Item 1");
        var act = async () => await _service.CreateItemAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Item group with ID*not found*");
    }

    [Fact]
    public async Task CreateItem_WhenUnitNotFound_ThrowsDomainException()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>())
            .Returns(group);
        _unitRepo.GetByIdAsync(_companyId, _unitId, Arg.Any<CancellationToken>())
            .Returns((Unit?)null);

        var dto = new CreateItemDto(_companyId, _groupId, _unitId, "Item 1");
        var act = async () => await _service.CreateItemAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Unit with ID*not found*");
    }

    [Fact]
    public async Task CreateItem_WhenDuplicateName_ThrowsDomainException()
    {
        var group = ItemGroup.Create(_companyId, "Electronics");
        var unit = Unit.Create(_companyId, "Pcs");

        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>())
            .Returns(group);
        _unitRepo.GetByIdAsync(_companyId, _unitId, Arg.Any<CancellationToken>())
            .Returns(unit);
        _itemRepo.ExistsByNameAsync(_companyId, "Duplicate Item", null, Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new CreateItemDto(_companyId, _groupId, _unitId, "Duplicate Item");
        var act = async () => await _service.CreateItemAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists*");
    }
}
