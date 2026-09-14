using System;
using System.Text.RegularExpressions;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Authoritative aggregate entity representing an Account or Party (Customer/Supplier) in the General Ledger.
/// </summary>
public class Ledger : AuditableEntity<Guid>
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled);

    public Guid CompanyId { get; private set; }
    public Guid GroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string PrintName { get; private set; } = string.Empty;
    public string? Alias { get; private set; }

    // Initial Master Setup Opening Balance (Not authoritative running balance)
    public Money OpeningBalance { get; private set; } = Money.Zero();
    public DebitCreditType OpeningBalanceType { get; private set; } = DebitCreditType.Debit;

    // Party & Bill-Wise Configuration
    public bool BillWiseEnabled { get; private set; }
    public int? CreditPeriodDays { get; private set; }
    public Money? CreditLimit { get; private set; }

    // Statutory & Address Details
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public string? StateName { get; private set; }
    public string? StateCode { get; private set; }
    public string? PinCode { get; private set; }
    public string Country { get; private set; } = "India";
    public string? GSTIN { get; private set; }
    public GSTRegistrationType GSTRegistrationType { get; private set; } = GSTRegistrationType.Regular;
    public string? PAN { get; private set; }
    public string? AadhaarNumber { get; private set; }

    // Contact Details
    public string? ContactPerson { get; private set; }
    public string? MobileNumber { get; private set; }
    public string? WhatsAppNumber { get; private set; }
    public string? Email { get; private set; }

    // Bank Details
    public string? BankName { get; private set; }
    public string? BankAccountNumber { get; private set; }
    public string? BankIFSC { get; private set; }
    public string? BankBranch { get; private set; }

    // System Protection
    public bool IsDefaultSystemLedger { get; private set; }

    // Parameterless constructor for persistence/ORM hydration
    private Ledger() : base()
    {
    }

    public Ledger(
        Guid id,
        Guid companyId,
        Guid groupId,
        string name,
        string? printName = null,
        string? @alias = null,
        Money? openingBalance = null,
        DebitCreditType openingBalanceType = DebitCreditType.Debit,
        bool billWiseEnabled = false,
        int? creditPeriodDays = null,
        Money? creditLimit = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? stateName = null,
        string? stateCode = null,
        string? pinCode = null,
        string country = "India",
        string? gstin = null,
        GSTRegistrationType gstRegistrationType = GSTRegistrationType.Regular,
        string? pan = null,
        string? aadhaarNumber = null,
        string? contactPerson = null,
        string? mobileNumber = null,
        string? whatsAppNumber = null,
        string? email = null,
        string? bankName = null,
        string? bankAccountNumber = null,
        string? bankIfsc = null,
        string? bankBranch = null,
        bool isDefaultSystemLedger = false) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (groupId == Guid.Empty)
            throw new DomainException("GroupId cannot be empty.");

        var normalizedOpeningBalance = openingBalance ?? Money.Zero();
        if (normalizedOpeningBalance.Amount == 0m && string.IsNullOrEmpty(normalizedOpeningBalance.Currency))
        {
            normalizedOpeningBalance = Money.Zero();
        }

        if (creditLimit.HasValue && string.IsNullOrEmpty(creditLimit.Value.Currency))
        {
            creditLimit = creditLimit.Value.Amount == 0m
                ? Money.Zero()
                : new Money(creditLimit.Value.Amount);
        }

        ValidateName(name);
        ValidateFinancials(normalizedOpeningBalance, creditPeriodDays, creditLimit);
        ValidateGstin(gstin);

        CompanyId = companyId;
        GroupId = groupId;
        Name = name.Trim();
        PrintName = string.IsNullOrWhiteSpace(printName) ? Name : printName.Trim();
        Alias = string.IsNullOrWhiteSpace(@alias) ? null : @alias.Trim();

        OpeningBalance = normalizedOpeningBalance;
        OpeningBalanceType = openingBalanceType;

        BillWiseEnabled = billWiseEnabled;
        CreditPeriodDays = creditPeriodDays;
        CreditLimit = creditLimit;

        AddressLine1 = addressLine1?.Trim();
        AddressLine2 = addressLine2?.Trim();
        City = city?.Trim();
        StateName = stateName?.Trim();
        StateCode = stateCode?.Trim();
        PinCode = pinCode?.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? "India" : country.Trim();
        GSTIN = string.IsNullOrWhiteSpace(gstin) ? null : gstin.Trim().ToUpperInvariant();
        GSTRegistrationType = gstRegistrationType;
        PAN = string.IsNullOrWhiteSpace(pan) ? null : pan.Trim().ToUpperInvariant();
        AadhaarNumber = string.IsNullOrWhiteSpace(aadhaarNumber) ? null : aadhaarNumber.Trim();

        ContactPerson = contactPerson?.Trim();
        MobileNumber = mobileNumber?.Trim();
        WhatsAppNumber = whatsAppNumber?.Trim();
        Email = email?.Trim();

        BankName = bankName?.Trim();
        BankAccountNumber = bankAccountNumber?.Trim();
        BankIFSC = bankIfsc?.Trim().ToUpperInvariant();
        BankBranch = bankBranch?.Trim();

        IsDefaultSystemLedger = isDefaultSystemLedger;
    }

    public static Ledger Create(
        Guid companyId,
        Guid groupId,
        string name,
        string? printName = null,
        string? @alias = null,
        Money? openingBalance = null,
        DebitCreditType openingBalanceType = DebitCreditType.Debit,
        bool billWiseEnabled = false,
        int? creditPeriodDays = null,
        Money? creditLimit = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? stateName = null,
        string? stateCode = null,
        string? pinCode = null,
        string country = "India",
        string? gstin = null,
        GSTRegistrationType gstRegistrationType = GSTRegistrationType.Regular,
        string? pan = null,
        string? aadhaarNumber = null,
        string? contactPerson = null,
        string? mobileNumber = null,
        string? whatsAppNumber = null,
        string? email = null,
        string? bankName = null,
        string? bankAccountNumber = null,
        string? bankIfsc = null,
        string? bankBranch = null,
        bool isDefaultSystemLedger = false)
    {
        return new Ledger(
            Guid.NewGuid(),
            companyId,
            groupId,
            name,
            printName,
            @alias,
            openingBalance,
            openingBalanceType,
            billWiseEnabled,
            creditPeriodDays,
            creditLimit,
            addressLine1,
            addressLine2,
            city,
            stateName,
            stateCode,
            pinCode,
            country,
            gstin,
            gstRegistrationType,
            pan,
            aadhaarNumber,
            contactPerson,
            mobileNumber,
            whatsAppNumber,
            email,
            bankName,
            bankAccountNumber,
            bankIfsc,
            bankBranch,
            isDefaultSystemLedger);
    }

    public void UpdateOpeningBalance(Money amount, DebitCreditType type, bool hasTransactionHistory)
    {
        if (amount == default || string.IsNullOrEmpty(amount.Currency))
        {
            amount = Money.Zero();
        }

        if (amount.IsNegative)
            throw new DomainException("Opening balance cannot be negative.");

        if (hasTransactionHistory && (amount != OpeningBalance || type != OpeningBalanceType))
        {
            throw new DomainException("Cannot modify Opening Balance for a ledger that has recorded transaction history / vouchers.");
        }

        OpeningBalance = amount;
        OpeningBalanceType = type;
    }

    public void Update(
        Guid groupId,
        string name,
        bool hasTransactionHistory,
        string? printName = null,
        string? @alias = null,
        Money? openingBalance = null,
        DebitCreditType openingBalanceType = DebitCreditType.Debit,
        bool billWiseEnabled = false,
        int? creditPeriodDays = null,
        Money? creditLimit = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? stateName = null,
        string? stateCode = null,
        string? pinCode = null,
        string country = "India",
        string? gstin = null,
        GSTRegistrationType gstRegistrationType = GSTRegistrationType.Regular,
        string? pan = null,
        string? aadhaarNumber = null,
        string? contactPerson = null,
        string? mobileNumber = null,
        string? whatsAppNumber = null,
        string? email = null,
        string? bankName = null,
        string? bankAccountNumber = null,
        string? bankIfsc = null,
        string? bankBranch = null)
    {
        if (groupId == Guid.Empty)
            throw new DomainException("GroupId cannot be empty.");

        var normalizedOpeningBalance = openingBalance ?? Money.Zero();
        if (normalizedOpeningBalance.Amount == 0m && string.IsNullOrEmpty(normalizedOpeningBalance.Currency))
        {
            normalizedOpeningBalance = Money.Zero();
        }

        if (creditLimit.HasValue && string.IsNullOrEmpty(creditLimit.Value.Currency))
        {
            creditLimit = creditLimit.Value.Amount == 0m
                ? Money.Zero()
                : new Money(creditLimit.Value.Amount);
        }

        ValidateName(name);
        ValidateFinancials(normalizedOpeningBalance, creditPeriodDays, creditLimit);
        ValidateGstin(gstin);

        if (hasTransactionHistory && (normalizedOpeningBalance != OpeningBalance || openingBalanceType != OpeningBalanceType))
        {
            throw new DomainException("Cannot modify Opening Balance for a ledger that has recorded transaction history / vouchers.");
        }

        GroupId = groupId;
        Name = name.Trim();
        PrintName = string.IsNullOrWhiteSpace(printName) ? Name : printName.Trim();
        Alias = string.IsNullOrWhiteSpace(@alias) ? null : @alias.Trim();

        OpeningBalance = normalizedOpeningBalance;
        OpeningBalanceType = openingBalanceType;

        BillWiseEnabled = billWiseEnabled;
        CreditPeriodDays = creditPeriodDays;
        CreditLimit = creditLimit;

        AddressLine1 = addressLine1?.Trim();
        AddressLine2 = addressLine2?.Trim();
        City = city?.Trim();
        StateName = stateName?.Trim();
        StateCode = stateCode?.Trim();
        PinCode = pinCode?.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? "India" : country.Trim();
        GSTIN = string.IsNullOrWhiteSpace(gstin) ? null : gstin.Trim().ToUpperInvariant();
        GSTRegistrationType = gstRegistrationType;
        PAN = string.IsNullOrWhiteSpace(pan) ? null : pan.Trim().ToUpperInvariant();
        AadhaarNumber = string.IsNullOrWhiteSpace(aadhaarNumber) ? null : aadhaarNumber.Trim();

        ContactPerson = contactPerson?.Trim();
        MobileNumber = mobileNumber?.Trim();
        WhatsAppNumber = whatsAppNumber?.Trim();
        Email = email?.Trim();

        BankName = bankName?.Trim();
        BankAccountNumber = bankAccountNumber?.Trim();
        BankIFSC = bankIfsc?.Trim().ToUpperInvariant();
        BankBranch = bankBranch?.Trim();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Ledger name cannot be empty.");

        if (name.Trim().Length > 100)
            throw new DomainException("Ledger name cannot exceed 100 characters.");
    }

    private static void ValidateFinancials(Money openingBalance, int? creditPeriodDays, Money? creditLimit)
    {
        if (openingBalance.IsNegative)
            throw new DomainException("Opening balance cannot be negative.");

        if (creditPeriodDays.HasValue && creditPeriodDays.Value < 0)
            throw new DomainException("Credit period days cannot be negative.");

        if (creditLimit.HasValue && creditLimit.Value.IsNegative)
            throw new DomainException("Credit limit cannot be negative.");
    }

    private static void ValidateGstin(string? gstin)
    {
        if (string.IsNullOrWhiteSpace(gstin))
            return;

        var trimmed = gstin.Trim();
        if (!GstinRegex.IsMatch(trimmed))
            throw new DomainException($"Invalid GSTIN format '{trimmed}'. Expected 15-character statutory alphanumeric GSTIN.");
    }
}