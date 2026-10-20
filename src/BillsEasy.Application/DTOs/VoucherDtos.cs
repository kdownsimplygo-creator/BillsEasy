using System;
using System.Collections.Generic;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.DTOs;

public record CreateVoucherLineDto(
    Guid LedgerId,
    DebitCreditType Type,
    decimal Amount,
    string? Narration = null
);

public record CreateVoucherDto(
    Guid CompanyId,
    Guid FinancialYearId,
    string VoucherNumber,
    VoucherType Type,
    DateOnly VoucherDate,
    string? ReferenceNumber,
    string? Narration,
    IReadOnlyList<CreateVoucherLineDto> Lines
);

public record VoucherLineDto(
    Guid Id,
    Guid VoucherId,
    Guid LedgerId,
    string? LedgerName,
    int SequenceNumber,
    DebitCreditType Type,
    decimal Amount,
    string? Narration
);

public record VoucherDto(
    Guid Id,
    Guid CompanyId,
    Guid FinancialYearId,
    string VoucherNumber,
    VoucherType Type,
    VoucherStatus Status,
    DateOnly VoucherDate,
    string? ReferenceNumber,
    string? Narration,
    decimal TotalAmount,
    IReadOnlyList<VoucherLineDto> Lines,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? PostedAt,
    string? PostedBy
);

public record VoucherFilterDto(
    Guid CompanyId,
    Guid? FinancialYearId = null,
    VoucherType? Type = null,
    VoucherStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? LedgerId = null
);
