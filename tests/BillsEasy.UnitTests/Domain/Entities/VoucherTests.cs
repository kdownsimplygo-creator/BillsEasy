using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class VoucherTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _financialYearId = Guid.NewGuid();
    private readonly FinancialYear _validFinancialYear;

    public VoucherTests()
    {
        _validFinancialYear = new FinancialYear(
            _financialYearId,
            _companyId,
            "FY 2026-27",
            new DateOnly(2026, 4, 1),
            new DateOnly(2027, 3, 31),
            isActive: true,
            isLocked: false);
    }

    [Fact]
    public void Create_WithValidParameters_InitializesVoucherInDraftStatus()
    {
        var voucherDate = new DateOnly(2026, 5, 10);
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            voucherDate,
            "REF-99",
            "General opening journal");

        voucher.CompanyId.Should().Be(_companyId);
        voucher.FinancialYearId.Should().Be(_financialYearId);
        voucher.VoucherNumber.Should().Be("JV-001");
        voucher.Type.Should().Be(VoucherType.Journal);
        voucher.Status.Should().Be(VoucherStatus.Draft);
        voucher.VoucherDate.Should().Be(voucherDate);
        voucher.ReferenceNumber.Should().Be("REF-99");
        voucher.Narration.Should().Be("General opening journal");
        voucher.Lines.Should().BeEmpty();
        voucher.TotalDebit.Amount.Should().Be(0m);
        voucher.TotalCredit.Amount.Should().Be(0m);
        voucher.IsBalanced.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidVoucherNumber_ThrowsDomainException(string? invalidNumber)
    {
        var act = () => Voucher.Create(
            _companyId,
            _financialYearId,
            invalidNumber!,
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        act.Should().Throw<DomainException>()
            .WithMessage("*VoucherNumber cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        var act = () => Voucher.Create(
            Guid.Empty,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyFinancialYearId_ThrowsDomainException()
    {
        var act = () => Voucher.Create(
            _companyId,
            Guid.Empty,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        act.Should().Throw<DomainException>()
            .WithMessage("*FinancialYearId cannot be empty*");
    }

    [Fact]
    public void AddLine_IncrementsSequenceNumberAndCalculatesTotals()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        var ledger1 = Guid.NewGuid();
        var ledger2 = Guid.NewGuid();

        var line1 = voucher.AddLine(ledger1, DebitCreditType.Debit, new Money(5000.50m), "Debit line");
        var line2 = voucher.AddLine(ledger2, DebitCreditType.Credit, new Money(5000.50m), "Credit line");

        voucher.Lines.Should().HaveCount(2);
        line1.SequenceNumber.Should().Be(1);
        line2.SequenceNumber.Should().Be(2);
        voucher.TotalDebit.Amount.Should().Be(5000.50m);
        voucher.TotalCredit.Amount.Should().Be(5000.50m);
        voucher.IsBalanced.Should().BeTrue();
        voucher.TotalAmount.Amount.Should().Be(5000.50m);
    }

    [Fact]
    public void AddLine_WithZeroOrNegativeAmount_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        var actZero = () => voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, Money.Zero());
        actZero.Should().Throw<DomainException>()
            .WithMessage("*amount must be greater than zero*");

        var actNegative = () => voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(-100m));
        actNegative.Should().Throw<DomainException>()
            .WithMessage("*amount must be greater than zero*");
    }

    [Fact]
    public void RemoveLine_AdjustsSequenceNumbersOfRemainingLines()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(100m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(200m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(300m));

        voucher.Lines.Should().HaveCount(3);
        voucher.RemoveLine(2);

        voucher.Lines.Should().HaveCount(2);
        voucher.Lines[0].SequenceNumber.Should().Be(1);
        voucher.Lines[0].Amount.Amount.Should().Be(100m);
        voucher.Lines[1].SequenceNumber.Should().Be(2);
        voucher.Lines[1].Amount.Amount.Should().Be(300m);
    }

    [Fact]
    public void RemoveLine_WhenLineDoesNotExist_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(100m));

        var act = () => voucher.RemoveLine(99);
        act.Should().Throw<DomainException>()
            .WithMessage("*sequence number 99 was not found*");
    }

    [Fact]
    public void ClearLines_RemovesAllLinesAndResetsTotals()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(100m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(100m));

        voucher.ClearLines();

        voucher.Lines.Should().BeEmpty();
        voucher.TotalDebit.Amount.Should().Be(0m);
        voucher.TotalCredit.Amount.Should().Be(0m);
        voucher.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void Post_WhenVoucherIsBalanced_TransitionsToPosted()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(2500m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(2500m));

        var postTime = new DateTime(2026, 5, 10, 14, 30, 0, DateTimeKind.Utc);
        voucher.Post(_validFinancialYear, postedBy: "admin", postedAt: postTime);

        voucher.Status.Should().Be(VoucherStatus.Posted);
        voucher.PostedBy.Should().Be("admin");
        voucher.PostedAt.Should().Be(postTime);
        voucher.TotalAmount.Amount.Should().Be(2500m);
    }

    [Fact]
    public void Post_WhenUnbalanced_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(2500m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(2400m));

        var act = () => voucher.Post(_validFinancialYear);
        act.Should().Throw<DomainException>()
            .WithMessage("*Total Debit (2500.00) must equal Total Credit (2400.00)*");
    }

    [Fact]
    public void Post_WhenLessThanTwoLines_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(2500m));

        var act = () => voucher.Post(_validFinancialYear);
        act.Should().Throw<DomainException>()
            .WithMessage("*must have at least 2 lines*");
    }

    [Fact]
    public void Post_WhenOnlyDebits_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));

        var act = () => voucher.Post(_validFinancialYear);
        act.Should().Throw<DomainException>()
            .WithMessage("*must contain at least one Credit line*");
    }

    [Fact]
    public void Post_WhenOnlyCredits_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));

        var act = () => voucher.Post(_validFinancialYear);
        act.Should().Throw<DomainException>()
            .WithMessage("*must contain at least one Debit line*");
    }

    [Fact]
    public void Post_WhenFinancialYearIsLocked_ThrowsDomainException()
    {
        var lockedFy = new FinancialYear(
            _financialYearId,
            _companyId,
            "FY 2026-27",
            new DateOnly(2026, 4, 1),
            new DateOnly(2027, 3, 31),
            isActive: true,
            isLocked: true);

        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));

        var act = () => voucher.Post(lockedFy);
        act.Should().Throw<DomainException>()
            .WithMessage("*is locked. Posting is prohibited*");
    }

    [Fact]
    public void Post_WhenVoucherDateOutsideFinancialYear_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2025, 3, 31)); // outside 2026-04-01 to 2027-03-31

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));

        var act = () => voucher.Post(_validFinancialYear);
        act.Should().Throw<DomainException>()
            .WithMessage("*outside financial year*");
    }

    [Fact]
    public void Post_WhenCompanyIdDoesNotMatchFinancialYearCompanyId_ThrowsDomainException()
    {
        var otherCompanyFy = new FinancialYear(
            Guid.NewGuid(),
            Guid.NewGuid(), // Foreign CompanyId
            "FY 2026-27",
            new DateOnly(2026, 4, 1),
            new DateOnly(2027, 3, 31));

        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));

        var act = () => voucher.Post(otherCompanyFy);
        act.Should().Throw<DomainException>()
            .WithMessage("*Voucher CompanyId does not match FinancialYear CompanyId*");
    }

    [Fact]
    public void Post_WhenAlreadyPosted_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));

        voucher.Post(_validFinancialYear);

        var act = () => voucher.Post(_validFinancialYear);
        act.Should().Throw<DomainException>()
            .WithMessage("*already posted*");
    }

    [Fact]
    public void Post_WhenAlreadyCancelled_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));

        voucher.Cancel();

        var act = () => voucher.Post(_validFinancialYear);
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot post a cancelled voucher*");
    }

    [Fact]
    public void ModifyLines_WhenVoucherIsPosted_ThrowsDomainException()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(1000m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(1000m));

        voucher.Post(_validFinancialYear);

        var addAct = () => voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(100m));
        addAct.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify lines of a voucher that is not in Draft status*");

        var removeAct = () => voucher.RemoveLine(1);
        removeAct.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify lines of a voucher that is not in Draft status*");

        var clearAct = () => voucher.ClearLines();
        clearAct.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify lines of a voucher that is not in Draft status*");
    }

    [Fact]
    public void Cancel_WhenDraftOrPosted_TransitionsToCancelled()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        voucher.Cancel("supervisor");
        voucher.Status.Should().Be(VoucherStatus.Cancelled);
        voucher.ModifiedBy.Should().Be("supervisor");

        var actDoubleCancel = () => voucher.Cancel();
        actDoubleCancel.Should().Throw<DomainException>()
            .WithMessage("*already cancelled*");
    }

    [Fact]
    public void MonetaryPrecision_WithExactPaiseAmounts_MaintainsAbsoluteBalance()
    {
        var voucher = Voucher.Create(
            _companyId,
            _financialYearId,
            "JV-001",
            VoucherType.Journal,
            new DateOnly(2026, 5, 10));

        // 100.33 + 200.67 = 301.00
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(100.33m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Debit, new Money(200.67m));
        voucher.AddLine(Guid.NewGuid(), DebitCreditType.Credit, new Money(301.00m));

        voucher.IsBalanced.Should().BeTrue();
        voucher.TotalDebit.Amount.Should().Be(301.00m);
        voucher.TotalCredit.Amount.Should().Be(301.00m);
        voucher.TotalAmount.Amount.Should().Be(301.00m);

        voucher.Post(_validFinancialYear);
        voucher.Status.Should().Be(VoucherStatus.Posted);
    }
}
