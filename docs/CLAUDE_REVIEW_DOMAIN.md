# BILLSEASY — CLAUDE FORENSIC REVIEW: DOMAIN LAYER SOURCE
**Target Directory**: W:\BillsEasy\src\BillsEasy.Domain
**Date**: 2026-09-13

---
## FILE: src\BillsEasy.Domain\BillsEasy.Domain.csproj
`xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

`

---
## FILE: src\BillsEasy.Domain\Common\AuditableEntity.cs
`csharp
using System;

namespace BillsEasy.Domain.Common;

/// <summary>
/// Base entity that encapsulates identity and standard audit metadata.
/// </summary>
/// <typeparam name=""TId"">The identifier type.</typeparam>
public abstract class AuditableEntity<TId> : Entity<TId>, IAuditableEntity
    where TId : notnull
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    protected AuditableEntity() : base()
    {
    }

    protected AuditableEntity(TId id) : base(id)
    {
    }
}

`

---
## FILE: src\BillsEasy.Domain\Common\DomainException.cs
`csharp
using System;

namespace BillsEasy.Domain.Common;

/// <summary>
/// Base exception for domain invariant violations in BillsEasy.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

`

---
## FILE: src\BillsEasy.Domain\Common\Entity.cs
`csharp
using System;

namespace BillsEasy.Domain.Common;

/// <summary>
/// Base class for all domain entities with identity equality semantics.
/// </summary>
/// <typeparam name=""TId"">The type of the entity identifier.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity()
    {
    }

    protected Entity(TId id)
    {
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        Id = id;
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id.Equals(other.Id);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj is not Entity<TId> other)
            return false;

        return Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !(left == right);
    }
}

`

---
## FILE: src\BillsEasy.Domain\Common\IAuditableEntity.cs
`csharp
using System;

namespace BillsEasy.Domain.Common;

/// <summary>
/// Foundational contract for entities that capture user attribution and audit timestamps.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string CreatedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
    string? ModifiedBy { get; set; }
}

`

---
## FILE: src\BillsEasy.Domain\Entities\AccountGroup.cs
`csharp
using System;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Master Data aggregate entity representing an Account Group in the Chart of Accounts hierarchy.
/// </summary>
public class AccountGroup : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentGroupId { get; private set; }
    public GroupNature Nature { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsDefaultSystemGroup { get; private set; }

    // Parameterless constructor for persistence/ORM hydration
    private AccountGroup() : base()
    {
    }

    public AccountGroup(
        Guid id,
        Guid companyId,
        string name,
        GroupNature nature,
        Guid? parentGroupId = null,
        bool isPrimary = false,
        bool isDefaultSystemGroup = false) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        ValidateAndSetName(name);

        if (!Enum.IsDefined(typeof(GroupNature), nature))
            throw new DomainException($"Invalid group nature '{nature}'.");

        if (parentGroupId.HasValue && parentGroupId.Value == id)
            throw new DomainException("An account group cannot be its own parent.");

        CompanyId = companyId;
        Name = name.Trim();
        Nature = nature;
        ParentGroupId = parentGroupId;
        IsPrimary = isPrimary && !parentGroupId.HasValue;
        IsDefaultSystemGroup = isDefaultSystemGroup;
    }

    public static AccountGroup Create(
        Guid companyId,
        string name,
        GroupNature nature,
        Guid? parentGroupId = null,
        bool isPrimary = false,
        bool isDefaultSystemGroup = false)
    {
        return new AccountGroup(
            Guid.NewGuid(),
            companyId,
            name,
            nature,
            parentGroupId,
            isPrimary,
            isDefaultSystemGroup);
    }

    public void Reparent(AccountGroup? newParent, IEnumerable<AccountGroup> companyGroups)
    {
        if (companyGroups == null)
            throw new ArgumentNullException(nameof(companyGroups), "Company groups collection is required for hierarchy verification.");

        if (newParent != null)
        {
            if (newParent.CompanyId != CompanyId)
                throw new DomainException("Parent account group must belong to the same company.");

            if (newParent.Id == Id)
                throw new DomainException("An account group cannot be its own parent.");

            if (IsDefaultSystemGroup && newParent.Nature != Nature)
                throw new DomainException($"Cannot change the nature of protected system group '{Name}'.");

            if (newParent.Nature != Nature && companyGroups.Any(g => g.ParentGroupId == Id))
                throw new DomainException($"Cannot change the nature of account group '{Name}' to '{newParent.Nature}' because it has child sub-groups. Move child groups first.");

            // Cycle detection traversal using the required companyGroups hierarchy
            var groupDict = new Dictionary<Guid, AccountGroup>();
            foreach (var g in companyGroups)
            {
                groupDict[g.Id] = g;
            }

            var currentCheckId = (Guid?)newParent.Id;
            while (currentCheckId.HasValue)
            {
                if (currentCheckId.Value == Id)
                {
                    throw new DomainException($"Cannot set parent: circular hierarchy detected. Group '{Name}' is an ancestor of the target parent group '{newParent.Name}'.");
                }

                if (groupDict.TryGetValue(currentCheckId.Value, out var ancestor))
                {
                    currentCheckId = ancestor.ParentGroupId;
                }
                else
                {
                    throw new DomainException($"Incomplete account group hierarchy supplied. Ancestor group with ID '{currentCheckId.Value}' was not found in the company groups collection.");
                }
            }

            ParentGroupId = newParent.Id;
            Nature = newParent.Nature;
            IsPrimary = false;
        }
        else
        {
            ParentGroupId = null;
            IsPrimary = true;
        }
    }

    public void Update(string name, AccountGroup? newParent, IEnumerable<AccountGroup> companyGroups, GroupNature? rootNature = null)
    {
        ValidateAndSetName(name);

        if (newParent == null && rootNature.HasValue && rootNature.Value != Nature)
        {
            if (IsDefaultSystemGroup)
                throw new DomainException($"Cannot change the nature of protected system group '{Name}'.");

            if (!Enum.IsDefined(typeof(GroupNature), rootNature.Value))
                throw new DomainException($"Invalid group nature '{rootNature.Value}'.");

            if (companyGroups.Any(g => g.ParentGroupId == Id))
                throw new DomainException($"Cannot change the nature of account group '{Name}' because it has child sub-groups. Modify or move child groups first.");

            Nature = rootNature.Value;
        }

        Reparent(newParent, companyGroups);

        Name = name.Trim();
    }

    public void SetPrimary(bool isPrimary)
    {
        if (isPrimary && ParentGroupId.HasValue)
            throw new DomainException("A sub-group with a parent cannot be designated as a Primary Root group.");

        IsPrimary = isPrimary;
    }

    private static void ValidateAndSetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Account group name cannot be empty.");

        if (name.Trim().Length > 100)
            throw new DomainException("Account group name cannot exceed 100 characters.");
    }
}
`

---
## FILE: src\BillsEasy.Domain\Entities\Company.cs
`csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Root organizational aggregate representing a business entity.
/// </summary>
public class Company : AuditableEntity<Guid>
{
    private static readonly Regex GstinRegex = new(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", RegexOptions.Compiled);

    public string CompanyName { get; private set; } = string.Empty;
    public string PrintName { get; private set; } = string.Empty;
    public string? ShortName { get; private set; }
    public string Country { get; private set; } = "India";
    public string StateName { get; private set; } = string.Empty;
    public string StateCode { get; private set; } = string.Empty;
    public DateOnly FinancialYearBeginning { get; private set; }
    public DateOnly BooksBeginningFrom { get; private set; }
    public string CurrencySymbol { get; private set; } = "₹";
    public string CurrencyFormalName { get; private set; } = "INR";
    public string? GSTIN { get; private set; }
    public GSTRegistrationType GSTRegistrationType { get; private set; } = GSTRegistrationType.Regular;
    public bool IsSecurityEnabled { get; private set; }

    private readonly List<FinancialYear> _financialYears = new();
    public IReadOnlyCollection<FinancialYear> FinancialYears => _financialYears.AsReadOnly();

    private Company() : base()
    {
    }

    public Company(
        Guid id,
        string companyName,
        string printName,
        string? shortName,
        string country,
        string stateName,
        string stateCode,
        DateOnly financialYearBeginning,
        DateOnly booksBeginningFrom,
        string currencySymbol,
        string currencyFormalName,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled) : base(id)
    {
        ValidateAndSetProperties(
            companyName,
            printName,
            shortName,
            country,
            stateName,
            stateCode,
            financialYearBeginning,
            booksBeginningFrom,
            currencySymbol,
            currencyFormalName,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);
    }

    public static Company Create(
        string companyName,
        string printName,
        string? shortName,
        string stateName,
        string stateCode,
        DateOnly financialYearBeginning,
        DateOnly booksBeginningFrom,
        string? gstin = null,
        GSTRegistrationType gstRegistrationType = GSTRegistrationType.Regular,
        string country = "India",
        string currencySymbol = "₹",
        string currencyFormalName = "INR",
        bool isSecurityEnabled = false)
    {
        var company = new Company(
            Guid.NewGuid(),
            companyName,
            printName,
            shortName,
            country,
            stateName,
            stateCode,
            financialYearBeginning,
            booksBeginningFrom,
            currencySymbol,
            currencyFormalName,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);

        // Auto-create initial financial year (1 year duration from FinancialYearBeginning)
        var fyEnd = financialYearBeginning.AddYears(1).AddDays(-1);
        var initialFy = FinancialYear.Create(company.Id, financialYearBeginning, fyEnd, isActive: true);
        company.AddFinancialYear(initialFy);

        return company;
    }

    public void Update(
        string companyName,
        string printName,
        string? shortName,
        string stateName,
        string stateCode,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled)
    {
        ValidateAndSetProperties(
            companyName,
            printName,
            shortName,
            Country,
            stateName,
            stateCode,
            FinancialYearBeginning,
            BooksBeginningFrom,
            CurrencySymbol,
            CurrencyFormalName,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);
    }

    public void AddFinancialYear(FinancialYear financialYear)
    {
        if (financialYear == null)
            throw new ArgumentNullException(nameof(financialYear));

        if (financialYear.CompanyId != Id)
            throw new DomainException($"FinancialYear CompanyId ({financialYear.CompanyId}) does not match Company Id ({Id}).");

        foreach (var existing in _financialYears)
        {
            if (existing.Id != financialYear.Id && existing.OverlapsWith(financialYear))
            {
                throw new DomainException(
                    $"Financial Year '{financialYear.YearName}' ({financialYear.StartDate:yyyy-MM-dd} to {financialYear.EndDate:yyyy-MM-dd}) overlaps with existing Financial Year '{existing.YearName}' ({existing.StartDate:yyyy-MM-dd} to {existing.EndDate:yyyy-MM-dd}).");
            }
        }

        if (financialYear.IsActive)
        {
            foreach (var existing in _financialYears)
            {
                existing.SetActive(false);
            }
        }

        _financialYears.Add(financialYear);
    }

    public void SetActiveFinancialYear(Guid financialYearId)
    {
        var target = _financialYears.FirstOrDefault(f => f.Id == financialYearId);
        if (target == null)
            throw new DomainException($"Financial Year with Id '{financialYearId}' not found in Company '{CompanyName}'.");

        foreach (var fy in _financialYears)
        {
            fy.SetActive(fy.Id == financialYearId);
        }
    }

    private void ValidateAndSetProperties(
        string companyName,
        string printName,
        string? shortName,
        string country,
        string stateName,
        string stateCode,
        DateOnly financialYearBeginning,
        DateOnly booksBeginningFrom,
        string currencySymbol,
        string currencyFormalName,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new DomainException("Company name cannot be empty.");

        if (string.IsNullOrWhiteSpace(stateName))
            throw new DomainException("State name cannot be empty.");

        if (string.IsNullOrWhiteSpace(stateCode))
            throw new DomainException("State code cannot be empty.");

        if (booksBeginningFrom < financialYearBeginning)
            throw new DomainException($"Books beginning date ({booksBeginningFrom:yyyy-MM-dd}) cannot be earlier than Financial year beginning date ({financialYearBeginning:yyyy-MM-dd}).");

        var cleanGstin = string.IsNullOrWhiteSpace(gstin) ? null : gstin.Trim().ToUpperInvariant();
        if (cleanGstin != null)
        {
            if (cleanGstin.Length != 15 || !GstinRegex.IsMatch(cleanGstin))
                throw new DomainException($"Invalid GSTIN format: '{cleanGstin}'. Must be a 15-character statutory alphanumeric code.");
        }

        CompanyName = companyName.Trim();
        PrintName = string.IsNullOrWhiteSpace(printName) ? CompanyName : printName.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? null : shortName.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? "India" : country.Trim();
        StateName = stateName.Trim();
        StateCode = stateCode.Trim();
        FinancialYearBeginning = financialYearBeginning;
        BooksBeginningFrom = booksBeginningFrom;
        CurrencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? "₹" : currencySymbol.Trim();
        CurrencyFormalName = string.IsNullOrWhiteSpace(currencyFormalName) ? "INR" : currencyFormalName.Trim().ToUpperInvariant();
        GSTIN = cleanGstin;
        GSTRegistrationType = gstRegistrationType;
        IsSecurityEnabled = isSecurityEnabled;
    }
}

`

---
## FILE: src\BillsEasy.Domain\Entities\FinancialYear.cs
`csharp
using System;
using BillsEasy.Domain.Common;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Represents an accounting period boundary for a Company.
/// </summary>
public class FinancialYear : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public string YearName { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsLocked { get; private set; }

    // Parameterless constructor for persistence/serialization
    private FinancialYear() : base()
    {
    }

    public FinancialYear(
        Guid id,
        Guid companyId,
        string yearName,
        DateOnly startDate,
        DateOnly endDate,
        bool isActive = false,
        bool isLocked = false) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(yearName))
            throw new DomainException("Financial year name cannot be empty.");

        if (startDate >= endDate)
            throw new DomainException($"Financial year StartDate ({startDate:yyyy-MM-dd}) must precede EndDate ({endDate:yyyy-MM-dd}).");

        CompanyId = companyId;
        YearName = yearName.Trim();
        StartDate = startDate;
        EndDate = endDate;
        IsActive = isActive;
        IsLocked = isLocked;
    }

    public static FinancialYear Create(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        string? customName = null,
        bool isActive = false)
    {
        var name = customName ?? $"{startDate.Year}-{endDate.Year}";
        return new FinancialYear(Guid.NewGuid(), companyId, name, startDate, endDate, isActive);
    }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    public bool OverlapsWith(DateOnly start, DateOnly end)
    {
        return StartDate <= end && end >= start && start <= EndDate;
    }

    public bool OverlapsWith(FinancialYear other)
    {
        if (other == null) throw new ArgumentNullException(nameof(other));
        return OverlapsWith(other.StartDate, other.EndDate);
    }

    public void SetActive(bool isActive) => IsActive = isActive;
    public void SetLocked(bool isLocked) => IsLocked = isLocked;
}

`

---
## FILE: src\BillsEasy.Domain\Entities\Ledger.cs
`csharp
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
        Money openingBalance = default,
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

        ValidateName(name);
        ValidateFinancials(openingBalance, creditPeriodDays, creditLimit);
        ValidateGstin(gstin);

        CompanyId = companyId;
        GroupId = groupId;
        Name = name.Trim();
        PrintName = string.IsNullOrWhiteSpace(printName) ? Name : printName.Trim();
        Alias = string.IsNullOrWhiteSpace(@alias) ? null : @alias.Trim();

        OpeningBalance = openingBalance;
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
        Money openingBalance = default,
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
        Money openingBalance = default,
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

        ValidateName(name);
        ValidateFinancials(openingBalance, creditPeriodDays, creditLimit);
        ValidateGstin(gstin);

        if (hasTransactionHistory && (openingBalance != OpeningBalance || openingBalanceType != OpeningBalanceType))
        {
            throw new DomainException("Cannot modify Opening Balance for a ledger that has recorded transaction history / vouchers.");
        }

        GroupId = groupId;
        Name = name.Trim();
        PrintName = string.IsNullOrWhiteSpace(printName) ? Name : printName.Trim();
        Alias = string.IsNullOrWhiteSpace(@alias) ? null : @alias.Trim();

        OpeningBalance = openingBalance;
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
`

---
## FILE: src\BillsEasy.Domain\Enums\DebitCreditType.cs
`csharp
namespace BillsEasy.Domain.Enums;

/// <summary>
/// Indicates Debit or Credit balance type.
/// </summary>
public enum DebitCreditType
{
    Debit = 1,
    Credit = 2
}
`

---
## FILE: src\BillsEasy.Domain\Enums\GroupNature.cs
`csharp
namespace BillsEasy.Domain.Enums;

/// <summary>
/// Defines the fundamental accounting nature/classification of an Account Group.
/// </summary>
public enum GroupNature
{
    Asset = 1,
    Liability = 2,
    Capital = 3,
    Income = 4,
    Expense = 5
}
`

---
## FILE: src\BillsEasy.Domain\Enums\GSTRegistrationType.cs
`csharp
namespace BillsEasy.Domain.Enums;

/// <summary>
/// Statutory GST registration classifications in India.
/// </summary>
public enum GSTRegistrationType
{
    Regular = 1,
    Composition = 2,
    Unregistered = 3,
    OverseasOrSEZ = 4
}

`

---
## FILE: src\BillsEasy.Domain\ValueObjects\Money.cs
`csharp
using System;
using System.Globalization;
using BillsEasy.Domain.Common;

namespace BillsEasy.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a monetary amount in a specific currency.
/// Uses decimal internally to guarantee exact arithmetic with zero floating-point drift.
/// </summary>
public readonly struct Money : IEquatable<Money>, IComparable<Money>, IComparable
{
    public const string DefaultCurrency = "INR";
    public const string DefaultCurrencySymbol = "₹";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("Currency code cannot be null or empty.");

        // Normalize decimal to standard 2 decimal places with banker's / mid-point rounding
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money FromDecimal(decimal amount, string currency = DefaultCurrency) => new(amount, currency);

    public static implicit operator Money(decimal amount) => new(amount);

    /// <summary>
    /// Creates a Money instance from minor units (e.g. Paise for INR, Cents for USD).
    /// </summary>
    public static Money FromMinorUnits(long minorUnits, string currency = DefaultCurrency)
    {
        return new Money(minorUnits / 100m, currency);
    }

    /// <summary>
    /// Converts the monetary amount to minor units (e.g. Paise: ₹100.50 -> 10050).
    /// </summary>
    public long ToMinorUnits()
    {
        return (long)Math.Round(Amount * 100m, MidpointRounding.AwayFromZero);
    }

    public bool IsZero => Amount == 0m;
    public bool IsPositive => Amount > 0m;
    public bool IsNegative => Amount < 0m;

    public Money Abs() => new(Math.Abs(Amount), Currency);

    public static Money operator +(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator -(Money value)
    {
        return new Money(-value.Amount, value.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator *(decimal multiplier, Money money)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0m)
            throw new DivideByZeroException("Cannot divide Money by zero.");

        return new Money(money.Amount / divisor, money.Currency);
    }

    public static bool operator ==(Money left, Money right) => left.Equals(right);
    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    public static bool operator <(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    public static bool operator >(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        AssertSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public bool Equals(Money other)
    {
        return Amount == other.Amount &&
               string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return obj is Money other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Amount, Currency?.ToUpperInvariant());
    }

    public int CompareTo(Money other)
    {
        AssertSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public int CompareTo(object? obj)
    {
        if (obj is null)
            return 1;

        if (obj is not Money other)
            throw new ArgumentException($"Object must be of type {nameof(Money)}.", nameof(obj));

        return CompareTo(other);
    }

    public override string ToString()
    {
        var symbol = Currency == DefaultCurrency ? DefaultCurrencySymbol : Currency;
        return $"{symbol} {Amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    public string ToString(string format, IFormatProvider? formatProvider = null)
    {
        return Amount.ToString(format, formatProvider ?? CultureInfo.InvariantCulture);
    }

    private static void AssertSameCurrency(Money left, Money right)
    {
        if (!string.Equals(left.Currency, right.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"Cannot perform currency operation between '{left.Currency}' and '{right.Currency}'.");
        }
    }
}

`

