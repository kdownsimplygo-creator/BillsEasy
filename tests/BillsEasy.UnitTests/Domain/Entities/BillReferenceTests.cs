using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class BillReferenceTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _ledgerId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_InitializesActiveBillReference()
    {
        var billDate = new DateOnly(2026, 4, 15);
        var dueDate = new DateOnly(2026, 5, 15);
        var originalAmount = new Money(50000); // 500.00 in minor units

        var bill = BillReference.Create(
            _companyId,
            _ledgerId,
            "INV-2026-001",
            BillReferenceType.NewRef,
            billDate,
            originalAmount,
            DebitCreditType.Debit,
            dueDate);

        bill.Id.Should().NotBeEmpty();
        bill.CompanyId.Should().Be(_companyId);
        bill.LedgerId.Should().Be(_ledgerId);
        bill.ReferenceNumber.Should().Be("INV-2026-001");
        bill.ReferenceType.Should().Be(BillReferenceType.NewRef);
        bill.Status.Should().Be(BillReferenceStatus.Active);
        bill.BillDate.Should().Be(billDate);
        bill.DueDate.Should().Be(dueDate);
        bill.OriginalAmount.Should().Be(originalAmount);
        bill.EntryType.Should().Be(DebitCreditType.Debit);
        bill.SourceVoucherLineId.Should().BeNull();
    }

    [Fact]
    public void Create_WithoutDueDate_AllowsNullDueDate()
    {
        var bill = BillReference.Create(
            _companyId,
            _ledgerId,
            "OP-BILL-01",
            BillReferenceType.OpeningRef,
            new DateOnly(2026, 4, 1),
            new Money(10000),
            DebitCreditType.Credit);

        bill.DueDate.Should().BeNull();
        bill.ReferenceType.Should().Be(BillReferenceType.OpeningRef);
        bill.EntryType.Should().Be(DebitCreditType.Credit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyReferenceNumber_ThrowsDomainException(string? invalidRefNo)
    {
        Action act = () => BillReference.Create(
            _companyId,
            _ledgerId,
            invalidRefNo!,
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*ReferenceNumber cannot be empty*");
    }

    [Fact]
    public void Create_WithReferenceNumberExceeding50Characters_ThrowsDomainException()
    {
        var longRef = new string('A', 51);
        Action act = () => BillReference.Create(
            _companyId,
            _ledgerId,
            longRef,
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*ReferenceNumber cannot exceed 50 characters*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5000)]
    public void Create_WithZeroOrNegativeAmount_ThrowsDomainException(long minorUnits)
    {
        Action act = () => BillReference.Create(
            _companyId,
            _ledgerId,
            "INV-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(minorUnits),
            DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*OriginalAmount must be greater than zero*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => BillReference.Create(
            Guid.Empty,
            _ledgerId,
            "INV-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyLedgerId_ThrowsDomainException()
    {
        Action act = () => BillReference.Create(
            _companyId,
            Guid.Empty,
            "INV-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*LedgerId cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyId_ThrowsDomainException()
    {
        Action act = () => new BillReference(
            Guid.Empty,
            _companyId,
            _ledgerId,
            "INV-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*Id cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptySourceVoucherLineId_ThrowsDomainException()
    {
        Action act = () => new BillReference(
            Guid.NewGuid(),
            _companyId,
            _ledgerId,
            "INV-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit,
            sourceVoucherLineId: Guid.Empty);

        act.Should().Throw<DomainException>()
            .WithMessage("*SourceVoucherLineId cannot be empty*");
    }

    [Fact]
    public void Cancel_WhenActive_TransitionsToCancelledStatus()
    {
        var bill = BillReference.Create(
            _companyId,
            _ledgerId,
            "INV-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit);

        var cancelTime = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        bill.Cancel("auditor", cancelTime);

        bill.Status.Should().Be(BillReferenceStatus.Cancelled);
        bill.ModifiedBy.Should().Be("auditor");
        bill.ModifiedAt.Should().Be(cancelTime);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsDomainException()
    {
        var bill = BillReference.Create(
            _companyId,
            _ledgerId,
            "INV-01",
            BillReferenceType.NewRef,
            new DateOnly(2026, 4, 15),
            new Money(1000),
            DebitCreditType.Debit);

        bill.Cancel("user1");

        Action act = () => bill.Cancel("user2");
        act.Should().Throw<DomainException>()
            .WithMessage("*already cancelled*");
    }
}
