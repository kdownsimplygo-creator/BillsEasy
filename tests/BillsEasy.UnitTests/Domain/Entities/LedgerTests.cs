using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class LedgerTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_ReturnsLedger()
    {
        var ledger = Ledger.Create(
            _companyId,
            _groupId,
            "Rohit Enterprises",
            printName: "Rohit Enterprises Pvt Ltd",
            alias: "ROHIT",
            openingBalance: 15000.50m,
            openingBalanceType: DebitCreditType.Debit,
            billWiseEnabled: true,
            creditPeriodDays: 30,
            creditLimit: 50000m,
            city: "Ahmedabad",
            stateName: "Gujarat",
            stateCode: "24",
            gstin: "24AAACR1234A1Z5");

        ledger.Should().NotBeNull();
        ledger.Id.Should().NotBeEmpty();
        ledger.CompanyId.Should().Be(_companyId);
        ledger.GroupId.Should().Be(_groupId);
        ledger.Name.Should().Be("Rohit Enterprises");
        ledger.PrintName.Should().Be("Rohit Enterprises Pvt Ltd");
        ledger.Alias.Should().Be("ROHIT");
        ledger.OpeningBalance.Should().Be(new Money(15000.50m));
        ledger.OpeningBalanceType.Should().Be(DebitCreditType.Debit);
        ledger.BillWiseEnabled.Should().BeTrue();
        ledger.CreditPeriodDays.Should().Be(30);
        ledger.CreditLimit.Should().Be(new Money(50000m));
        ledger.City.Should().Be("Ahmedabad");
        ledger.StateName.Should().Be("Gujarat");
        ledger.StateCode.Should().Be("24");
        ledger.GSTIN.Should().Be("24AAACR1234A1Z5");
        ledger.IsDefaultSystemLedger.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyOrNullName_ThrowsDomainException(string? invalidName)
    {
        Action act = () => Ledger.Create(_companyId, _groupId, invalidName!);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyCompanyId_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(Guid.Empty, _groupId, "Valid Ledger");

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyGroupId_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, Guid.Empty, "Valid Ledger");

        act.Should().Throw<DomainException>()
            .WithMessage("*GroupId cannot be empty*");
    }

    [Fact]
    public void Create_WithNegativeOpeningBalance_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: -100m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Opening balance cannot be negative*");
    }

    [Fact]
    public void Create_WithNegativeCreditPeriod_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", creditPeriodDays: -5);

        act.Should().Throw<DomainException>()
            .WithMessage("*Credit period days cannot be negative*");
    }

    [Fact]
    public void Create_WithNegativeCreditLimit_ThrowsDomainException()
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", creditLimit: -500m);

        act.Should().Throw<DomainException>()
            .WithMessage("*Credit limit cannot be negative*");
    }

    [Theory]
    [InlineData("24AAACR1234A1")]
    [InlineData("INVALIDGSTIN1234")]
    [InlineData("24AAACR1234A1Z567")]
    public void Create_WithInvalidGSTIN_ThrowsDomainException(string invalidGstin)
    {
        Action act = () => Ledger.Create(_companyId, _groupId, "Test Ledger", gstin: invalidGstin);

        act.Should().Throw<DomainException>()
            .WithMessage("*Invalid GSTIN format*");
    }

    [Fact]
    public void Create_WhenPrintNameIsEmpty_DefaultsToName()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "My Bank Account", printName: "  ");

        ledger.PrintName.Should().Be("My Bank Account");
    }

    [Fact]
    public void UpdateOpeningBalance_WhenNoTransactionHistory_UpdatesSuccessfully()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        ledger.UpdateOpeningBalance(500m, DebitCreditType.Credit, hasTransactionHistory: false);

        ledger.OpeningBalance.Should().Be(new Money(500m));
        ledger.OpeningBalanceType.Should().Be(DebitCreditType.Credit);
    }

    [Fact]
    public void UpdateOpeningBalance_WhenHasTransactionHistoryAndAmountChanged_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        Action act = () => ledger.UpdateOpeningBalance(200m, DebitCreditType.Debit, hasTransactionHistory: true);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify Opening Balance for a ledger that has recorded transaction history*");
    }

    [Fact]
    public void UpdateOpeningBalance_WhenHasTransactionHistoryAndTypeChanged_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        Action act = () => ledger.UpdateOpeningBalance(100m, DebitCreditType.Credit, hasTransactionHistory: true);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify Opening Balance for a ledger that has recorded transaction history*");
    }

    [Fact]
    public void UpdateOpeningBalance_WhenHasTransactionHistoryAndUnchanged_Succeeds()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        ledger.UpdateOpeningBalance(100m, DebitCreditType.Debit, hasTransactionHistory: true);

        ledger.OpeningBalance.Should().Be(new Money(100m));
    }

    [Fact]
    public void Update_WhenHasTransactionHistoryAndOpeningBalanceChanged_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Test Ledger", openingBalance: 100m, openingBalanceType: DebitCreditType.Debit);

        Action act = () => ledger.Update(
            _groupId,
            "Updated Name",
            hasTransactionHistory: true,
            openingBalance: 200m,
            openingBalanceType: DebitCreditType.Debit);

        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify Opening Balance for a ledger that has recorded transaction history*");
    }

    [Fact]
    public void MethodSignatures_RequireExplicitHasTransactionHistoryParameter_WithNoDefaultValue()
    {
        var updateOpBalMethod = typeof(Ledger).GetMethod(nameof(Ledger.UpdateOpeningBalance));
        updateOpBalMethod.Should().NotBeNull();
        var hasHistoryParam1 = Array.Find(updateOpBalMethod!.GetParameters(), p => p.Name == "hasTransactionHistory");
        hasHistoryParam1.Should().NotBeNull();
        hasHistoryParam1!.HasDefaultValue.Should().BeFalse("hasTransactionHistory parameter must be mandatory with no default value");

        var updateMethod = typeof(Ledger).GetMethod(nameof(Ledger.Update));
        updateMethod.Should().NotBeNull();
        var hasHistoryParam2 = Array.Find(updateMethod!.GetParameters(), p => p.Name == "hasTransactionHistory");
        hasHistoryParam2.Should().NotBeNull();
        hasHistoryParam2!.HasDefaultValue.Should().BeFalse("hasTransactionHistory parameter in Update must be mandatory with no default value");
    }

    [Fact]
    public void Create_WithoutExplicitOpeningBalance_InitializesToSemanticMoneyZero()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Default Money Ledger");

        ledger.OpeningBalance.Should().Be(Money.Zero());
        ledger.OpeningBalance.Amount.Should().Be(0m);
        ledger.OpeningBalance.Currency.Should().Be("INR");
        ledger.OpeningBalance.IsZero.Should().BeTrue();
        ledger.OpeningBalance.ToString().Should().Be("₹ 0.00");

        // Arithmetic with valid Money must not throw currency mismatch
        var added = ledger.OpeningBalance + new Money(500m);
        added.Should().Be(new Money(500m));
        added.Currency.Should().Be("INR");
    }

    [Fact]
    public void Create_WithExplicitDefaultMoney_SafelyNormalizesToSemanticMoneyZero()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Explicit Default Money Ledger", openingBalance: default);

        ledger.OpeningBalance.Should().Be(Money.Zero());
        ledger.OpeningBalance.Amount.Should().Be(0m);
        ledger.OpeningBalance.Currency.Should().Be("INR");
        ledger.OpeningBalance.IsZero.Should().BeTrue();
    }

    [Fact]
    public void Create_WithExplicitPositiveOpeningBalance_PreservesAmountAndCurrency()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Funded Ledger", openingBalance: 25000.75m);

        ledger.OpeningBalance.Should().Be(new Money(25000.75m));
        ledger.OpeningBalance.Amount.Should().Be(25000.75m);
        ledger.OpeningBalance.Currency.Should().Be("INR");
        ledger.OpeningBalance.IsPositive.Should().BeTrue();
    }

    [Fact]
    public void Update_WithoutExplicitOpeningBalance_NormalizesToSemanticMoneyZero()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Ledger For Update");
        ledger.Update(_groupId, "Updated Ledger", hasTransactionHistory: false, openingBalance: default);

        ledger.OpeningBalance.Should().Be(Money.Zero());
        ledger.OpeningBalance.Currency.Should().Be("INR");
    }

    [Fact]
    public void UpdateOpeningBalance_WithDefaultMoney_NormalizesToSemanticMoneyZero()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Ledger OpBal Update", openingBalance: 100m);
        ledger.UpdateOpeningBalance(default, DebitCreditType.Debit, hasTransactionHistory: false);

        ledger.OpeningBalance.Should().Be(Money.Zero());
        ledger.OpeningBalance.Currency.Should().Be("INR");
    }

    [Fact]
    public void MethodSignatures_OpeningBalanceParameter_UsesNullableMoneyWithNullDefault_PreventingDefaultStructLandmine()
    {
        // Verifies the fix for Issue 3: Money openingBalance = default (which silently produced Currency=null)
        // has been eliminated from public signatures in favor of Money? openingBalance = null coalescing to Money.Zero().
        var ctor = typeof(Ledger).GetConstructors()[0];
        var ctorParam = Array.Find(ctor.GetParameters(), p => p.Name == "openingBalance");
        ctorParam.Should().NotBeNull();
        ctorParam!.ParameterType.Should().Be(typeof(Money?));
        ctorParam.DefaultValue.Should().BeNull("Constructor openingBalance must default to null, not default(Money)");

        var createMethod = typeof(Ledger).GetMethod(nameof(Ledger.Create));
        createMethod.Should().NotBeNull();
        var createParam = Array.Find(createMethod!.GetParameters(), p => p.Name == "openingBalance");
        createParam.Should().NotBeNull();
        createParam!.ParameterType.Should().Be(typeof(Money?));
        createParam.DefaultValue.Should().BeNull("Create openingBalance must default to null, not default(Money)");

        var updateMethod = typeof(Ledger).GetMethod(nameof(Ledger.Update));
        updateMethod.Should().NotBeNull();
        var updateParam = Array.Find(updateMethod!.GetParameters(), p => p.Name == "openingBalance");
        updateParam.Should().NotBeNull();
        updateParam!.ParameterType.Should().Be(typeof(Money?));
        updateParam.DefaultValue.Should().BeNull("Update openingBalance must default to null, not default(Money)");

        // Runtime check: passing explicit null evaluates to Money.Zero()
        var ledger = Ledger.Create(_companyId, _groupId, "Null OpBal Ledger", openingBalance: null);
        ledger.OpeningBalance.Should().Be(Money.Zero());
        ledger.OpeningBalance.Currency.Should().Be("INR");
    }
}