using System;
using System.Collections.Generic;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Application.DTOs;

public record BillReferenceDto(
    Guid Id,
    Guid CompanyId,
    Guid LedgerId,
    string ReferenceNumber,
    BillReferenceType ReferenceType,
    BillReferenceStatus Status,
    DateOnly BillDate,
    DateOnly? DueDate,
    decimal OriginalAmount,
    DebitCreditType EntryType,
    Guid? SourceVoucherLineId,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);

public record BillAllocationDto(
    Guid Id,
    Guid CompanyId,
    Guid VoucherLineId,
    Guid BillReferenceId,
    decimal AllocatedAmount,
    DateTime CreatedAt,
    string CreatedBy);

public record PendingBillDto(
    BillReferenceDto Bill,
    decimal TotalAllocated,
    decimal OutstandingAmount);

public record CreateBillReferenceDto(
    Guid CompanyId,
    Guid LedgerId,
    string ReferenceNumber,
    BillReferenceType ReferenceType,
    DateOnly BillDate,
    decimal OriginalAmount,
    DebitCreditType EntryType,
    DateOnly? DueDate = null,
    Guid? SourceVoucherLineId = null);

public record CreateBillAllocationDto(
    Guid CompanyId,
    Guid VoucherLineId,
    Guid BillReferenceId,
    decimal AllocatedAmount);
