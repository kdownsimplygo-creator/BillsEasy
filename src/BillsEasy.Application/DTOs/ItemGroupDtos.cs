using System;
using System.Collections.Generic;

namespace BillsEasy.Application.DTOs;

public record ItemGroupDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    Guid? ParentGroupId,
    string? ParentGroupName,
    string? Description,
    bool IsDefaultSystemGroup,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);

public class ItemGroupNodeDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentGroupId { get; set; }
    public string? ParentGroupName { get; set; }
    public string? Description { get; set; }
    public bool IsDefaultSystemGroup { get; set; }
    public int Level { get; set; }
    public List<ItemGroupNodeDto> Children { get; set; } = new();
}

public record CreateItemGroupDto(
    Guid CompanyId,
    string Name,
    Guid? ParentGroupId = null,
    string? Description = null);

public record UpdateItemGroupDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    Guid? ParentGroupId = null,
    string? Description = null);

public record ItemGroupLookupDto(
    Guid Id,
    string Name,
    Guid? ParentGroupId,
    bool IsDefaultSystemGroup);
