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