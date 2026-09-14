# BILLSEASY — CLAUDE FORENSIC REVIEW: APPLICATION LAYER SOURCE
**Target Directory**: W:\BillsEasy\src\BillsEasy.Application
**Date**: 2026-09-13

---
## FILE: src\BillsEasy.Application\BillsEasy.Application.csproj
`xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\BillsEasy.Domain\BillsEasy.Domain.csproj" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>

`

---
## FILE: src\BillsEasy.Application\Common\IndianStates.cs
`csharp
using System.Collections.Generic;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common;

public static class IndianStates
{
    public static readonly IReadOnlyList<IndianStateDto> All = new List<IndianStateDto>
    {
        new("Jammu and Kashmir", "01"),
        new("Himachal Pradesh", "02"),
        new("Punjab", "03"),
        new("Chandigarh", "04"),
        new("Uttarakhand", "05"),
        new("Haryana", "06"),
        new("Delhi", "07"),
        new("Rajasthan", "08"),
        new("Uttar Pradesh", "09"),
        new("Bihar", "10"),
        new("Sikkim", "11"),
        new("Arunachal Pradesh", "12"),
        new("Nagaland", "13"),
        new("Manipur", "14"),
        new("Mizoram", "15"),
        new("Tripura", "16"),
        new("Meghalaya", "17"),
        new("Assam", "18"),
        new("West Bengal", "19"),
        new("Jharkhand", "20"),
        new("Odisha", "21"),
        new("Chhattisgarh", "22"),
        new("Madhya Pradesh", "23"),
        new("Gujarat", "24"),
        new("Dadra and Nagar Haveli and Daman and Diu", "26"),
        new("Maharashtra", "27"),
        new("Andhra Pradesh", "28"),
        new("Karnataka", "29"),
        new("Goa", "30"),
        new("Lakshadweep", "31"),
        new("Kerala", "32"),
        new("Tamil Nadu", "33"),
        new("Puducherry", "34"),
        new("Andaman and Nicobar Islands", "35"),
        new("Telangana", "36"),
        new("Andhra Pradesh (New)", "37"),
        new("Ladakh", "38"),
        new("Other Territory", "97"),
        new("Centre Jurisdiction", "99")
    }.AsReadOnly();
}

`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\IAccountGroupRepository.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface IAccountGroupRepository
{
    Task<AccountGroup?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<AccountGroup?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroup>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroup>> GetByParentIdAsync(Guid companyId, Guid parentId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? parentGroupId = null, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task<bool> HasLedgersAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task AddAsync(AccountGroup group, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<AccountGroup> groups, CancellationToken cancellationToken = default);
    Task UpdateAsync(AccountGroup group, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\IAccountGroupService.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface IAccountGroupService
{
    Task<AccountGroupDto> CreateGroupAsync(CreateAccountGroupDto dto, CancellationToken cancellationToken = default);
    Task<AccountGroupDto> UpdateGroupAsync(UpdateAccountGroupDto dto, CancellationToken cancellationToken = default);
    Task DeleteGroupAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<AccountGroupDto?> GetGroupByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroupDto>> GetAllGroupsAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroupNodeDto>> GetGroupTreeAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountGroupLookupDto>> GetLookupGroupsAsync(Guid companyId, Guid? excludeSubtreeOfGroupId = null, CancellationToken cancellationToken = default);
    Task SeedDefaultGroupsAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default);
}
`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\ICompanyContextService.cs
`csharp
using System;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface ICompanyContextService
{
    Company? ActiveCompany { get; }
    FinancialYear? ActiveFinancialYear { get; }
    bool HasActiveCompany => ActiveCompany != null;

    event EventHandler? ActiveCompanyChanged;

    void SetActiveContext(Company company, FinancialYear? financialYear = null);
    void SetActiveFinancialYear(FinancialYear financialYear);
    void CloseActiveCompany();
}

`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\ICompanyRepositories.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string companyName, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task UpdateAsync(Company company, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IFinancialYearRepository
{
    Task<IReadOnlyList<FinancialYear>> GetByCompanyIdAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<FinancialYear?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FinancialYear?> GetActiveFinancialYearAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task AddAsync(FinancialYear financialYear, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid companyId, Guid financialYearId, CancellationToken cancellationToken = default);
}

`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\ICurrentUserService.cs
`csharp
namespace BillsEasy.Application.Common.Interfaces;

/// <summary>
/// Service contract to supply user attribution and workstation identity context.
/// </summary>
public interface ICurrentUserService
{
    string UserId { get; }
    string UserName { get; }
    string WorkstationName { get; }
}

`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\IDateTimeProvider.cs
`csharp
using System;

namespace BillsEasy.Application.Common.Interfaces;

/// <summary>
/// Provider abstraction for system date and time to guarantee testability.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}

`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\ILedgerRepository.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Common.Interfaces;

public interface ILedgerRepository
{
    Task<Ledger?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<Ledger?> GetByNameAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ledger>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid companyId, string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionsAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default);
    Task<bool> HasBillReferencesAsync(Guid companyId, Guid ledgerId, CancellationToken cancellationToken = default);
    Task AddAsync(Ledger ledger, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Ledger> ledgers, CancellationToken cancellationToken = default);
    Task UpdateAsync(Ledger ledger, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
}
`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\ILedgerService.cs
`csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.DTOs;

namespace BillsEasy.Application.Common.Interfaces;

public interface ILedgerService
{
    Task<LedgerDto> CreateLedgerAsync(CreateLedgerDto dto, CancellationToken cancellationToken = default);
    Task<LedgerDto> UpdateLedgerAsync(UpdateLedgerDto dto, CancellationToken cancellationToken = default);
    Task DeleteLedgerAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<LedgerDto?> GetLedgerByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerDto>> GetAllLedgersAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerDto>> GetLedgersByGroupAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerDto>> SearchLedgersAsync(Guid companyId, string searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LedgerLookupDto>> GetLookupLedgersAsync(Guid companyId, CancellationToken cancellationToken = default);
    Task SeedDefaultLedgersAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default);
}
`

---
## FILE: src\BillsEasy.Application\Common\Interfaces\ISqliteDbConnectionFactory.cs
`csharp
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace BillsEasy.Application.Common.Interfaces;

/// <summary>
/// Factory interface for obtaining configured SQLite database connections.
/// </summary>
public interface ISqliteDbConnectionFactory
{
    /// <summary>
    /// Creates and opens a SQLite connection configured with WAL mode and foreign keys enabled.
    /// </summary>
    DbConnection CreateConnection();

    /// <summary>
    /// Asynchronously creates and opens a SQLite connection configured with WAL mode and foreign keys enabled.
    /// </summary>
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

`

---
## FILE: src\BillsEasy.Application\DTOs\AccountGroupDtos.cs
`csharp
using System;
using System.Collections.Generic;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.DTOs;

public record AccountGroupDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    Guid? ParentGroupId,
    string? ParentGroupName,
    GroupNature Nature,
    string NatureName,
    bool IsPrimary,
    bool IsDefaultSystemGroup,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);

public class AccountGroupNodeDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentGroupId { get; set; }
    public string? ParentGroupName { get; set; }
    public GroupNature Nature { get; set; }
    public string NatureName { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool IsDefaultSystemGroup { get; set; }
    public int Level { get; set; }
    public List<AccountGroupNodeDto> Children { get; set; } = new();
}

public record CreateAccountGroupDto(
    Guid CompanyId,
    string Name,
    Guid? ParentGroupId = null,
    GroupNature? Nature = null);

public record UpdateAccountGroupDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    Guid? ParentGroupId = null,
    GroupNature? Nature = null);

public record AccountGroupLookupDto(
    Guid Id,
    string Name,
    GroupNature Nature,
    string NatureName,
    Guid? ParentGroupId,
    bool IsDefaultSystemGroup);
`

---
## FILE: src\BillsEasy.Application\DTOs\CompanyDtos.cs
`csharp
using System;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.DTOs;

public record CompanyDto(
    Guid Id,
    string CompanyName,
    string PrintName,
    string? ShortName,
    string Country,
    string StateName,
    string StateCode,
    DateOnly FinancialYearBeginning,
    DateOnly BooksBeginningFrom,
    string CurrencySymbol,
    string CurrencyFormalName,
    string? GSTIN,
    GSTRegistrationType GSTRegistrationType,
    bool IsSecurityEnabled,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy
);

public record FinancialYearDto(
    Guid Id,
    Guid CompanyId,
    string YearName,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    bool IsLocked,
    DateTime CreatedAt,
    string CreatedBy
);

public record IndianStateDto(string StateName, string StateCode);

`

---
## FILE: src\BillsEasy.Application\DTOs\LedgerDtos.cs
`csharp
using System;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.DTOs;

public record LedgerDto(
    Guid Id,
    Guid CompanyId,
    Guid GroupId,
    string GroupName,
    GroupNature GroupNature,
    string GroupNatureName,
    string Name,
    string PrintName,
    string? Alias,
    decimal OpeningBalance,
    DebitCreditType OpeningBalanceType,
    string OpeningBalanceDisplay,
    bool BillWiseEnabled,
    int? CreditPeriodDays,
    decimal? CreditLimit,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateName,
    string? StateCode,
    string? PinCode,
    string Country,
    string? GSTIN,
    GSTRegistrationType GSTRegistrationType,
    string? PAN,
    string? AadhaarNumber,
    string? ContactPerson,
    string? MobileNumber,
    string? WhatsAppNumber,
    string? Email,
    string? BankName,
    string? BankAccountNumber,
    string? BankIFSC,
    string? BankBranch,
    bool IsDefaultSystemLedger,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);

public record CreateLedgerDto(
    Guid CompanyId,
    Guid GroupId,
    string Name,
    string? PrintName = null,
    string? Alias = null,
    decimal OpeningBalance = 0m,
    DebitCreditType OpeningBalanceType = DebitCreditType.Debit,
    bool BillWiseEnabled = false,
    int? CreditPeriodDays = null,
    decimal? CreditLimit = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? StateName = null,
    string? StateCode = null,
    string? PinCode = null,
    string Country = "India",
    string? GSTIN = null,
    GSTRegistrationType GSTRegistrationType = GSTRegistrationType.Regular,
    string? PAN = null,
    string? AadhaarNumber = null,
    string? ContactPerson = null,
    string? MobileNumber = null,
    string? WhatsAppNumber = null,
    string? Email = null,
    string? BankName = null,
    string? BankAccountNumber = null,
    string? BankIFSC = null,
    string? BankBranch = null);

public record UpdateLedgerDto(
    Guid Id,
    Guid CompanyId,
    Guid GroupId,
    string Name,
    string? PrintName = null,
    string? Alias = null,
    decimal OpeningBalance = 0m,
    DebitCreditType OpeningBalanceType = DebitCreditType.Debit,
    bool BillWiseEnabled = false,
    int? CreditPeriodDays = null,
    decimal? CreditLimit = null,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? StateName = null,
    string? StateCode = null,
    string? PinCode = null,
    string Country = "India",
    string? GSTIN = null,
    GSTRegistrationType GSTRegistrationType = GSTRegistrationType.Regular,
    string? PAN = null,
    string? AadhaarNumber = null,
    string? ContactPerson = null,
    string? MobileNumber = null,
    string? WhatsAppNumber = null,
    string? Email = null,
    string? BankName = null,
    string? BankAccountNumber = null,
    string? BankIFSC = null,
    string? BankBranch = null);

public record LedgerLookupDto(
    Guid Id,
    string Name,
    string PrintName,
    Guid GroupId,
    string GroupName,
    GroupNature GroupNature,
    bool BillWiseEnabled,
    bool IsDefaultSystemLedger);
`

---
## FILE: src\BillsEasy.Application\Services\AccountGroupService.cs
`csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.Services;

public class AccountGroupService : IAccountGroupService
{
    private readonly IAccountGroupRepository _accountGroupRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AccountGroupService(
        IAccountGroupRepository accountGroupRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _accountGroupRepository = accountGroupRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AccountGroupDto> CreateGroupAsync(CreateAccountGroupDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Account group name cannot be empty.");

        var trimmedName = dto.Name.Trim();
        if (await _accountGroupRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.ParentGroupId, null, cancellationToken))
            throw new DomainException($"An account group with name '{trimmedName}' already exists under the selected parent group.");

        GroupNature nature;
        bool isPrimary = false;

        if (dto.ParentGroupId.HasValue)
        {
            var parent = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.ParentGroupId.Value, cancellationToken);
            if (parent == null)
                throw new DomainException($"Parent account group with ID '{dto.ParentGroupId.Value}' not found.");

            if (parent.CompanyId != dto.CompanyId)
                throw new DomainException("Parent account group must belong to the same company.");

            nature = parent.Nature;
        }
        else
        {
            if (!dto.Nature.HasValue)
                throw new DomainException("Group Nature is required when creating a primary root group.");

            nature = dto.Nature.Value;
            isPrimary = true;
        }

        var group = AccountGroup.Create(
            dto.CompanyId,
            trimmedName,
            nature,
            dto.ParentGroupId,
            isPrimary,
            isDefaultSystemGroup: false);

        group.CreatedAt = _dateTimeProvider.UtcNow;
        group.CreatedBy = _currentUserService.UserId;

        await _accountGroupRepository.AddAsync(group, cancellationToken);

        return await MapToDtoAsync(group, cancellationToken);
    }

    public async Task<AccountGroupDto> UpdateGroupAsync(UpdateAccountGroupDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Account group name cannot be empty.");

        var group = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.Id, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{dto.Id}' not found.");

        var trimmedName = dto.Name.Trim();
        if (await _accountGroupRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.ParentGroupId, dto.Id, cancellationToken))
            throw new DomainException($"Another account group with name '{trimmedName}' already exists under the selected parent group.");

        var allGroups = await _accountGroupRepository.GetAllAsync(dto.CompanyId, cancellationToken);
        AccountGroup? parentGroup = null;
        if (dto.ParentGroupId.HasValue)
        {
            parentGroup = allGroups.FirstOrDefault(g => g.Id == dto.ParentGroupId.Value)
                       ?? await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.ParentGroupId.Value, cancellationToken);
            if (parentGroup == null)
                throw new DomainException($"Parent account group with ID '{dto.ParentGroupId.Value}' not found.");
        }

        group.Update(trimmedName, parentGroup, allGroups, dto.Nature);
        group.ModifiedAt = _dateTimeProvider.UtcNow;
        group.ModifiedBy = _currentUserService.UserId;

        await _accountGroupRepository.UpdateAsync(group, cancellationToken);

        return await MapToDtoAsync(group, cancellationToken);
    }

    public async Task DeleteGroupAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _accountGroupRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{id}' not found.");

        if (group.IsDefaultSystemGroup)
            throw new DomainException($"System predefined account group '{group.Name}' cannot be deleted.");

        if (await _accountGroupRepository.HasChildrenAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete account group '{group.Name}' because it has one or more child sub-groups.");

        if (await _accountGroupRepository.HasLedgersAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete account group '{group.Name}' because it has one or more ledgers assigned to it.");

        await _accountGroupRepository.DeleteAsync(companyId, id, cancellationToken);
    }

    public async Task<AccountGroupDto?> GetGroupByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var group = await _accountGroupRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (group == null) return null;

        return await MapToDtoAsync(group, cancellationToken);
    }

    public async Task<IReadOnlyList<AccountGroupDto>> GetAllGroupsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        return groups.Select(g => MapToDto(g, groupDict)).OrderBy(g => g.Nature).ThenBy(g => g.Name).ToList();
    }

    public async Task<IReadOnlyList<AccountGroupNodeDto>> GetGroupTreeAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        var lookup = groups.ToLookup(g => g.ParentGroupId);

        List<AccountGroupNodeDto> BuildNodes(Guid? parentId, int level)
        {
            var list = new List<AccountGroupNodeDto>();
            foreach (var g in lookup[parentId].OrderBy(x => x.Name))
            {
                var node = new AccountGroupNodeDto
                {
                    Id = g.Id,
                    CompanyId = g.CompanyId,
                    Name = g.Name,
                    ParentGroupId = g.ParentGroupId,
                    ParentGroupName = g.ParentGroupId.HasValue && groupDict.TryGetValue(g.ParentGroupId.Value, out var parent) ? parent.Name : null,
                    Nature = g.Nature,
                    NatureName = g.Nature.ToString(),
                    IsPrimary = g.IsPrimary,
                    IsDefaultSystemGroup = g.IsDefaultSystemGroup,
                    Level = level,
                    Children = BuildNodes(g.Id, level + 1)
                };
                list.Add(node);
            }
            return list;
        }

        return BuildNodes(null, 0);
    }

    public async Task<IReadOnlyList<AccountGroupLookupDto>> GetLookupGroupsAsync(Guid companyId, Guid? excludeSubtreeOfGroupId = null, CancellationToken cancellationToken = default)
    {
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);

        HashSet<Guid> excludedIds = new();
        if (excludeSubtreeOfGroupId.HasValue)
        {
            excludedIds.Add(excludeSubtreeOfGroupId.Value);
            var lookup = groups.ToLookup(g => g.ParentGroupId);

            void CollectDescendants(Guid parentId)
            {
                foreach (var child in lookup[parentId])
                {
                    if (excludedIds.Add(child.Id))
                    {
                        CollectDescendants(child.Id);
                    }
                }
            }

            CollectDescendants(excludeSubtreeOfGroupId.Value);
        }

        return groups
            .Where(g => !excludedIds.Contains(g.Id))
            .OrderBy(g => g.Nature)
            .ThenBy(g => g.Name)
            .Select(g => new AccountGroupLookupDto(
                g.Id,
                g.Name,
                g.Nature,
                g.Nature.ToString(),
                g.ParentGroupId,
                g.IsDefaultSystemGroup))
            .ToList();
    }

    public async Task SeedDefaultGroupsAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default)
    {
        var existing = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        if (existing.Count > 0)
        {
            return; // Already seeded
        }

        var now = _dateTimeProvider.UtcNow;
        var seededGroups = new List<AccountGroup>();

        AccountGroup CreateSystemGroup(string name, GroupNature nature, Guid? parentId = null, bool isPrimary = false)
        {
            var grp = AccountGroup.Create(
                companyId,
                name,
                nature,
                parentId,
                isPrimary: isPrimary,
                isDefaultSystemGroup: true);

            grp.CreatedAt = now;
            grp.CreatedBy = createdBy;
            seededGroups.Add(grp);
            return grp;
        }

        // 1. Primary Roots
        var capPrimary = CreateSystemGroup("Capital Account", GroupNature.Capital, isPrimary: true);
        var liabPrimary = CreateSystemGroup("Liabilities", GroupNature.Liability, isPrimary: true);
        var assetPrimary = CreateSystemGroup("Assets", GroupNature.Asset, isPrimary: true);
        var incPrimary = CreateSystemGroup("Income", GroupNature.Income, isPrimary: true);
        var expPrimary = CreateSystemGroup("Expenses", GroupNature.Expense, isPrimary: true);

        // 2. Capital Subgroups
        CreateSystemGroup("Reserves & Surplus", GroupNature.Capital, capPrimary.Id);

        // 3. Liabilities Subgroups
        var currLiab = CreateSystemGroup("Current Liabilities", GroupNature.Liability, liabPrimary.Id);
        CreateSystemGroup("Sundry Creditors", GroupNature.Liability, currLiab.Id);
        CreateSystemGroup("Duties & Taxes", GroupNature.Liability, currLiab.Id);
        CreateSystemGroup("Outstanding Expenses", GroupNature.Liability, currLiab.Id);
        CreateSystemGroup("Provisions", GroupNature.Liability, currLiab.Id);

        var loansLiab = CreateSystemGroup("Loans (Liability)", GroupNature.Liability, liabPrimary.Id);
        CreateSystemGroup("Bank OD Accounts", GroupNature.Liability, loansLiab.Id);
        CreateSystemGroup("Secured Loans", GroupNature.Liability, loansLiab.Id);
        CreateSystemGroup("Unsecured Loans", GroupNature.Liability, loansLiab.Id);

        // 4. Assets Subgroups
        var currAssets = CreateSystemGroup("Current Assets", GroupNature.Asset, assetPrimary.Id);
        CreateSystemGroup("Sundry Debtors", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Cash-in-hand", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Bank Accounts", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Stock-in-hand", GroupNature.Asset, currAssets.Id);
        CreateSystemGroup("Loans & Advances (Asset)", GroupNature.Asset, currAssets.Id);

        CreateSystemGroup("Fixed Assets", GroupNature.Asset, assetPrimary.Id);
        CreateSystemGroup("Investments", GroupNature.Asset, assetPrimary.Id);

        // 5. Income Subgroups
        var dirIncome = CreateSystemGroup("Direct Income", GroupNature.Income, incPrimary.Id);
        CreateSystemGroup("Sales Accounts", GroupNature.Income, dirIncome.Id);
        CreateSystemGroup("Indirect Income", GroupNature.Income, incPrimary.Id);

        // 6. Expense Subgroups
        var dirExp = CreateSystemGroup("Direct Expenses", GroupNature.Expense, expPrimary.Id);
        CreateSystemGroup("Purchase Accounts", GroupNature.Expense, dirExp.Id);
        CreateSystemGroup("Indirect Expenses", GroupNature.Expense, expPrimary.Id);

        await _accountGroupRepository.AddRangeAsync(seededGroups, cancellationToken);
    }

    private async Task<AccountGroupDto> MapToDtoAsync(AccountGroup group, CancellationToken cancellationToken)
    {
        string? parentName = null;
        if (group.ParentGroupId.HasValue)
        {
            var parent = await _accountGroupRepository.GetByIdAsync(group.CompanyId, group.ParentGroupId.Value, cancellationToken);
            parentName = parent?.Name;
        }

        return new AccountGroupDto(
            group.Id,
            group.CompanyId,
            group.Name,
            group.ParentGroupId,
            parentName,
            group.Nature,
            group.Nature.ToString(),
            group.IsPrimary,
            group.IsDefaultSystemGroup,
            group.CreatedAt,
            group.CreatedBy,
            group.ModifiedAt,
            group.ModifiedBy);
    }

    private static AccountGroupDto MapToDto(AccountGroup group, Dictionary<Guid, AccountGroup> allGroups)
    {
        string? parentName = null;
        if (group.ParentGroupId.HasValue && allGroups.TryGetValue(group.ParentGroupId.Value, out var parent))
        {
            parentName = parent.Name;
        }

        return new AccountGroupDto(
            group.Id,
            group.CompanyId,
            group.Name,
            group.ParentGroupId,
            parentName,
            group.Nature,
            group.Nature.ToString(),
            group.IsPrimary,
            group.IsDefaultSystemGroup,
            group.CreatedAt,
            group.CreatedBy,
            group.ModifiedAt,
            group.ModifiedBy);
    }
}
`

---
## FILE: src\BillsEasy.Application\Services\CompanyService.cs
`csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.Services;

public class CompanyService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAccountGroupService? _accountGroupService;
    private readonly ILedgerService? _ledgerService;

    public CompanyService(
        ICompanyRepository companyRepository,
        IFinancialYearRepository financialYearRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        IAccountGroupService? accountGroupService = null,
        ILedgerService? ledgerService = null)
    {
        _companyRepository = companyRepository;
        _financialYearRepository = financialYearRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _accountGroupService = accountGroupService;
        _ledgerService = ledgerService;
    }

    public async Task<CompanyDto> CreateCompanyAsync(
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
        bool isSecurityEnabled = false,
        CancellationToken cancellationToken = default)
    {
        if (await _companyRepository.ExistsByNameAsync(companyName, null, cancellationToken))
            throw new DomainException($"A company with name '{companyName}' already exists.");

        var company = Company.Create(
            companyName,
            printName,
            shortName,
            stateName,
            stateCode,
            financialYearBeginning,
            booksBeginningFrom,
            gstin,
            gstRegistrationType,
            country,
            currencySymbol,
            currencyFormalName,
            isSecurityEnabled);

        company.CreatedAt = _dateTimeProvider.UtcNow;
        company.CreatedBy = _currentUserService.UserId;

        foreach (var fy in company.FinancialYears)
        {
            fy.CreatedAt = _dateTimeProvider.UtcNow;
            fy.CreatedBy = _currentUserService.UserId;
        }

        await _companyRepository.AddAsync(company, cancellationToken);

        if (_accountGroupService != null)
        {
            await _accountGroupService.SeedDefaultGroupsAsync(company.Id, _currentUserService.UserId, cancellationToken);
        }

        if (_ledgerService != null)
        {
            await _ledgerService.SeedDefaultLedgersAsync(company.Id, _currentUserService.UserId, cancellationToken);
        }

        return MapToDto(company);
    }

    public async Task<CompanyDto> UpdateCompanyAsync(
        Guid companyId,
        string companyName,
        string printName,
        string? shortName,
        string stateName,
        string stateCode,
        string? gstin,
        GSTRegistrationType gstRegistrationType,
        bool isSecurityEnabled,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{companyId}' not found.");

        if (await _companyRepository.ExistsByNameAsync(companyName, companyId, cancellationToken))
            throw new DomainException($"Another company with name '{companyName}' already exists.");

        company.Update(
            companyName,
            printName,
            shortName,
            stateName,
            stateCode,
            gstin,
            gstRegistrationType,
            isSecurityEnabled);

        company.ModifiedAt = _dateTimeProvider.UtcNow;
        company.ModifiedBy = _currentUserService.UserId;

        await _companyRepository.UpdateAsync(company, cancellationToken);
        return MapToDto(company);
    }

    public async Task<CompanyDto?> GetCompanyByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(id, cancellationToken);
        return company == null ? null : MapToDto(company);
    }

    public async Task<IReadOnlyList<CompanyDto>> GetAllCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var list = await _companyRepository.GetAllAsync(cancellationToken);
        return list.Select(MapToDto).ToList();
    }

    public async Task DeleteCompanyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(id, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{id}' not found.");

        await _companyRepository.DeleteAsync(id, cancellationToken);
    }

    private static CompanyDto MapToDto(Company company)
    {
        return new CompanyDto(
            company.Id,
            company.CompanyName,
            company.PrintName,
            company.ShortName,
            company.Country,
            company.StateName,
            company.StateCode,
            company.FinancialYearBeginning,
            company.BooksBeginningFrom,
            company.CurrencySymbol,
            company.CurrencyFormalName,
            company.GSTIN,
            company.GSTRegistrationType,
            company.IsSecurityEnabled,
            company.CreatedAt,
            company.CreatedBy,
            company.ModifiedAt,
            company.ModifiedBy);
    }
}
`

---
## FILE: src\BillsEasy.Application\Services\FinancialYearService.cs
`csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;

namespace BillsEasy.Application.Services;

public class FinancialYearService
{
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public FinancialYearService(
        IFinancialYearRepository financialYearRepository,
        ICompanyRepository companyRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _financialYearRepository = financialYearRepository;
        _companyRepository = companyRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<FinancialYearDto> CreateFinancialYearAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        string? customName = null,
        bool isActive = false,
        CancellationToken cancellationToken = default)
    {
        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{companyId}' not found.");

        var existingFys = await _financialYearRepository.GetByCompanyIdAsync(companyId, cancellationToken);
        var newFy = FinancialYear.Create(companyId, startDate, endDate, customName, isActive);

        foreach (var existing in existingFys)
        {
            if (existing.OverlapsWith(newFy))
            {
                throw new DomainException(
                    $"Financial Year '{newFy.YearName}' ({newFy.StartDate:yyyy-MM-dd} to {newFy.EndDate:yyyy-MM-dd}) overlaps with existing Financial Year '{existing.YearName}' ({existing.StartDate:yyyy-MM-dd} to {existing.EndDate:yyyy-MM-dd}).");
            }
        }

        newFy.CreatedAt = _dateTimeProvider.UtcNow;
        newFy.CreatedBy = _currentUserService.UserId;

        await _financialYearRepository.AddAsync(newFy, cancellationToken);
        return MapToDto(newFy);
    }

    public async Task<IReadOnlyList<FinancialYearDto>> GetFinancialYearsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var list = await _financialYearRepository.GetByCompanyIdAsync(companyId, cancellationToken);
        return list.Select(MapToDto).ToList().AsReadOnly();
    }

    public async Task SelectActiveFinancialYearAsync(
        Guid companyId,
        Guid financialYearId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _financialYearRepository.GetByIdAsync(financialYearId, cancellationToken);
        if (existing == null || existing.CompanyId != companyId)
            throw new DomainException($"Financial Year with ID '{financialYearId}' not found for company '{companyId}'.");

        await _financialYearRepository.SetActiveAsync(companyId, financialYearId, cancellationToken);
    }

    private static FinancialYearDto MapToDto(FinancialYear fy) => new(
        fy.Id,
        fy.CompanyId,
        fy.YearName,
        fy.StartDate,
        fy.EndDate,
        fy.IsActive,
        fy.IsLocked,
        fy.CreatedAt,
        fy.CreatedBy
    );
}

`

---
## FILE: src\BillsEasy.Application\Services\LedgerService.cs
`csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.Services;

public class LedgerService : ILedgerService
{
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IAccountGroupRepository _accountGroupRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public LedgerService(
        ILedgerRepository ledgerRepository,
        IAccountGroupRepository accountGroupRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _ledgerRepository = ledgerRepository;
        _accountGroupRepository = accountGroupRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<LedgerDto> CreateLedgerAsync(CreateLedgerDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.GroupId == Guid.Empty)
            throw new DomainException("GroupId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Ledger name cannot be empty.");

        var group = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.GroupId, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{dto.GroupId}' not found.");

        if (group.CompanyId != dto.CompanyId)
            throw new DomainException("Account group must belong to the same company.");

        var trimmedName = dto.Name.Trim();
        if (await _ledgerRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, null, cancellationToken))
            throw new DomainException($"A ledger with name '{trimmedName}' already exists in this company.");

        var ledger = Ledger.Create(
            dto.CompanyId,
            dto.GroupId,
            trimmedName,
            dto.PrintName,
            dto.Alias,
            dto.OpeningBalance,
            dto.OpeningBalanceType,
            dto.BillWiseEnabled,
            dto.CreditPeriodDays,
            dto.CreditLimit,
            dto.AddressLine1,
            dto.AddressLine2,
            dto.City,
            dto.StateName,
            dto.StateCode,
            dto.PinCode,
            dto.Country,
            dto.GSTIN,
            dto.GSTRegistrationType,
            dto.PAN,
            dto.AadhaarNumber,
            dto.ContactPerson,
            dto.MobileNumber,
            dto.WhatsAppNumber,
            dto.Email,
            dto.BankName,
            dto.BankAccountNumber,
            dto.BankIFSC,
            dto.BankBranch,
            isDefaultSystemLedger: false);

        ledger.CreatedAt = _dateTimeProvider.UtcNow;
        ledger.CreatedBy = _currentUserService.UserId;

        await _ledgerRepository.AddAsync(ledger, cancellationToken);

        return MapToDto(ledger, group);
    }

    public async Task<LedgerDto> UpdateLedgerAsync(UpdateLedgerDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.GroupId == Guid.Empty)
            throw new DomainException("GroupId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new DomainException("Ledger name cannot be empty.");

        var ledger = await _ledgerRepository.GetByIdAsync(dto.CompanyId, dto.Id, cancellationToken);
        if (ledger == null)
            throw new DomainException($"Ledger with ID '{dto.Id}' not found.");

        var group = await _accountGroupRepository.GetByIdAsync(dto.CompanyId, dto.GroupId, cancellationToken);
        if (group == null)
            throw new DomainException($"Account group with ID '{dto.GroupId}' not found.");

        if (group.CompanyId != dto.CompanyId)
            throw new DomainException("Account group must belong to the same company.");

        var trimmedName = dto.Name.Trim();
        if (await _ledgerRepository.ExistsByNameAsync(dto.CompanyId, trimmedName, dto.Id, cancellationToken))
            throw new DomainException($"Another ledger with name '{trimmedName}' already exists in this company.");

        var hasTransactions = await _ledgerRepository.HasTransactionsAsync(dto.CompanyId, dto.Id, cancellationToken);

        ledger.Update(
            dto.GroupId,
            trimmedName,
            hasTransactions,
            dto.PrintName,
            dto.Alias,
            dto.OpeningBalance,
            dto.OpeningBalanceType,
            dto.BillWiseEnabled,
            dto.CreditPeriodDays,
            dto.CreditLimit,
            dto.AddressLine1,
            dto.AddressLine2,
            dto.City,
            dto.StateName,
            dto.StateCode,
            dto.PinCode,
            dto.Country,
            dto.GSTIN,
            dto.GSTRegistrationType,
            dto.PAN,
            dto.AadhaarNumber,
            dto.ContactPerson,
            dto.MobileNumber,
            dto.WhatsAppNumber,
            dto.Email,
            dto.BankName,
            dto.BankAccountNumber,
            dto.BankIFSC,
            dto.BankBranch);

        ledger.ModifiedAt = _dateTimeProvider.UtcNow;
        ledger.ModifiedBy = _currentUserService.UserId;

        await _ledgerRepository.UpdateAsync(ledger, cancellationToken);

        return MapToDto(ledger, group);
    }

    public async Task DeleteLedgerAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (ledger == null)
            throw new DomainException($"Ledger with ID '{id}' not found.");

        if (ledger.IsDefaultSystemLedger)
            throw new DomainException($"System predefined ledger '{ledger.Name}' cannot be deleted.");

        if (await _ledgerRepository.HasTransactionsAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete ledger '{ledger.Name}' because it has one or more recorded financial transactions.");

        if (await _ledgerRepository.HasBillReferencesAsync(companyId, id, cancellationToken))
            throw new DomainException($"Cannot delete ledger '{ledger.Name}' because it has one or more bill-wise references.");

        await _ledgerRepository.DeleteAsync(companyId, id, cancellationToken);
    }

    public async Task<LedgerDto?> GetLedgerByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var ledger = await _ledgerRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (ledger == null) return null;

        var group = await _accountGroupRepository.GetByIdAsync(companyId, ledger.GroupId, cancellationToken);
        return MapToDto(ledger, group);
    }

    public async Task<IReadOnlyList<LedgerDto>> GetAllLedgersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var ledgers = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        return ledgers.Select(l =>
        {
            groupDict.TryGetValue(l.GroupId, out var grp);
            return MapToDto(l, grp);
        }).OrderBy(l => l.Name).ToList();
    }

    public async Task<IReadOnlyList<LedgerDto>> GetLedgersByGroupAsync(Guid companyId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var ledgers = await _ledgerRepository.GetByGroupIdAsync(companyId, groupId, cancellationToken);
        var group = await _accountGroupRepository.GetByIdAsync(companyId, groupId, cancellationToken);

        return ledgers.Select(l => MapToDto(l, group)).OrderBy(l => l.Name).ToList();
    }

    public async Task<IReadOnlyList<LedgerDto>> SearchLedgersAsync(Guid companyId, string searchTerm, CancellationToken cancellationToken = default)
    {
        var all = await GetAllLedgersAsync(companyId, cancellationToken);
        if (string.IsNullOrWhiteSpace(searchTerm))
            return all;

        var query = searchTerm.Trim().ToLowerInvariant();
        return all.Where(l =>
            l.Name.ToLowerInvariant().Contains(query) ||
            l.PrintName.ToLowerInvariant().Contains(query) ||
            (l.Alias != null && l.Alias.ToLowerInvariant().Contains(query)) ||
            l.GroupName.ToLowerInvariant().Contains(query) ||
            (l.GSTIN != null && l.GSTIN.ToLowerInvariant().Contains(query)) ||
            (l.City != null && l.City.ToLowerInvariant().Contains(query)) ||
            (l.ContactPerson != null && l.ContactPerson.ToLowerInvariant().Contains(query)) ||
            (l.MobileNumber != null && l.MobileNumber.Contains(query))
        ).ToList();
    }

    public async Task<IReadOnlyList<LedgerLookupDto>> GetLookupLedgersAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var ledgers = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        var groupDict = groups.ToDictionary(g => g.Id);

        return ledgers.Select(l =>
        {
            groupDict.TryGetValue(l.GroupId, out var grp);
            return new LedgerLookupDto(
                l.Id,
                l.Name,
                l.PrintName,
                l.GroupId,
                grp?.Name ?? "Unknown Group",
                grp?.Nature ?? GroupNature.Asset,
                l.BillWiseEnabled,
                l.IsDefaultSystemLedger);
        }).OrderBy(l => l.Name).ToList();
    }

    public async Task SeedDefaultLedgersAsync(Guid companyId, string createdBy, CancellationToken cancellationToken = default)
    {
        var existing = await _ledgerRepository.GetAllAsync(companyId, cancellationToken);
        if (existing.Count > 0)
        {
            return; // Already seeded
        }

        var groups = await _accountGroupRepository.GetAllAsync(companyId, cancellationToken);
        if (groups.Count == 0)
        {
            return; // Groups must exist before ledgers can be seeded
        }

        var cashGroup = groups.FirstOrDefault(g => g.Name.Equals("Cash-in-hand", StringComparison.OrdinalIgnoreCase))
                     ?? groups.FirstOrDefault(g => g.Name.Equals("Current Assets", StringComparison.OrdinalIgnoreCase))
                     ?? groups.FirstOrDefault(g => g.Nature == GroupNature.Asset);

        var plGroup = groups.FirstOrDefault(g => g.Name.Equals("Capital Account", StringComparison.OrdinalIgnoreCase))
                   ?? groups.FirstOrDefault(g => g.Name.Equals("Reserves & Surplus", StringComparison.OrdinalIgnoreCase))
                   ?? groups.FirstOrDefault(g => g.Nature == GroupNature.Capital);

        var now = _dateTimeProvider.UtcNow;
        var seededLedgers = new List<Ledger>();

        if (cashGroup != null)
        {
            var cashLedger = Ledger.Create(
                companyId,
                cashGroup.Id,
                "Cash",
                printName: "Cash Account",
                openingBalance: 0m,
                openingBalanceType: DebitCreditType.Debit,
                billWiseEnabled: false,
                isDefaultSystemLedger: true);

            cashLedger.CreatedAt = now;
            cashLedger.CreatedBy = createdBy;
            seededLedgers.Add(cashLedger);
        }

        if (plGroup != null)
        {
            var plLedger = Ledger.Create(
                companyId,
                plGroup.Id,
                "Profit & Loss Account",
                printName: "Profit & Loss Account",
                openingBalance: 0m,
                openingBalanceType: DebitCreditType.Credit,
                billWiseEnabled: false,
                isDefaultSystemLedger: true);

            plLedger.CreatedAt = now;
            plLedger.CreatedBy = createdBy;
            seededLedgers.Add(plLedger);
        }

        if (seededLedgers.Count > 0)
        {
            await _ledgerRepository.AddRangeAsync(seededLedgers, cancellationToken);
        }
    }

    private static LedgerDto MapToDto(Ledger ledger, AccountGroup? group)
    {
        var groupName = group?.Name ?? "Unknown Group";
        var groupNature = group?.Nature ?? GroupNature.Asset;
        var opBalDisplay = ledger.OpeningBalance.Amount > 0
            ? $"₹{ledger.OpeningBalance.Amount:N2} {ledger.OpeningBalanceType}"
            : "₹0.00";

        return new LedgerDto(
            ledger.Id,
            ledger.CompanyId,
            ledger.GroupId,
            groupName,
            groupNature,
            groupNature.ToString(),
            ledger.Name,
            ledger.PrintName,
            ledger.Alias,
            ledger.OpeningBalance.Amount,
            ledger.OpeningBalanceType,
            opBalDisplay,
            ledger.BillWiseEnabled,
            ledger.CreditPeriodDays,
            ledger.CreditLimit?.Amount,
            ledger.AddressLine1,
            ledger.AddressLine2,
            ledger.City,
            ledger.StateName,
            ledger.StateCode,
            ledger.PinCode,
            ledger.Country,
            ledger.GSTIN,
            ledger.GSTRegistrationType,
            ledger.PAN,
            ledger.AadhaarNumber,
            ledger.ContactPerson,
            ledger.MobileNumber,
            ledger.WhatsAppNumber,
            ledger.Email,
            ledger.BankName,
            ledger.BankAccountNumber,
            ledger.BankIFSC,
            ledger.BankBranch,
            ledger.IsDefaultSystemLedger,
            ledger.CreatedAt,
            ledger.CreatedBy,
            ledger.ModifiedAt,
            ledger.ModifiedBy);
    }
}
`

