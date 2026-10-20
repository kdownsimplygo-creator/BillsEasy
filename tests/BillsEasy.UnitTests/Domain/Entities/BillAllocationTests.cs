using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class BillAllocationTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _voucherLineId = Guid.NewGuid();
    private readonly Guid _billReferenceId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_InitializesBillAllocation()
    {
        var amount = new Money(25000); // 250.00
        var createdAt = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);

        var allocation = BillAllocation.Create(
            _companyId,
            _voucherLineId,
            _billReferenceId,
            amount,
            "cashier",
            createdAt);

        allocation.Id.Should().NotBeEmpty();
        allocation.CompanyId.Should().Be(_companyId);
        allocation.VoucherLineId.Should().Be(_voucherLineId);
        allocation.BillReferenceId.Should().Be(_billReferenceId);
        allocation.AllocatedAmount.Should().Be(amount);
        allocation.CreatedAt.Should().Be(createdAt);
        allocation.CreatedBy.Should().Be("cashier");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Create_WithZeroOrNegativeAllocatedAmount_ThrowsDomainException(long minorUnits)
    {
        Action act = () => BillAllocation.Create(
            _companyId,
            _voucherLineId,
            _billReferenceId,
            new Money(minorUnits),
            "user");

        act.Should().Throw<DomainException>()
            .WithMessage("*AllocatedAmount must be greater than zero*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => BillAllocation.Create(
            Guid.Empty,
            _voucherLineId,
            _billReferenceId,
            new Money(5000),
            "user");

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyVoucherLineId_ThrowsDomainException()
    {
        Action act = () => BillAllocation.Create(
            _companyId,
            Guid.Empty,
            _billReferenceId,
            new Money(5000),
            "user");

        act.Should().Throw<DomainException>()
            .WithMessage("*VoucherLineId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyBillReferenceId_ThrowsDomainException()
    {
        Action act = () => BillAllocation.Create(
            _companyId,
            _voucherLineId,
            Guid.Empty,
            new Money(5000),
            "user");

        act.Should().Throw<DomainException>()
            .WithMessage("*BillReferenceId cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsDomainException()
    {
        Action act = () => new BillAllocation(
            Guid.Empty,
            _companyId,
            _voucherLineId,
            _billReferenceId,
            new Money(5000),
            DateTime.UtcNow,
            "user");

        act.Should().Throw<DomainException>()
            .WithMessage("*Id cannot be empty*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyCreatedBy_ThrowsDomainException(string? invalidCreatedBy)
    {
        Action act = () => new BillAllocation(
            Guid.NewGuid(),
            _companyId,
            _voucherLineId,
            _billReferenceId,
            new Money(5000),
            DateTime.UtcNow,
            invalidCreatedBy!);

        act.Should().Throw<DomainException>()
            .WithMessage("*CreatedBy cannot be empty*");
    }
}
