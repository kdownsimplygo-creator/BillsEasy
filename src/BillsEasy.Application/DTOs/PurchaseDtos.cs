using System;
using System.Collections.Generic;
using BillsEasy.Domain.Enums;

namespace BillsEasy.Application.DTOs;

public record TransportDetailsDto(
    string? TransportName,
    string? GrRrNumber,
    DateOnly? TransportDate,
    string? VehicleNumber,
    string? StationFromPlace);

public record PurchaseInvoiceItemLineDto(
    Guid Id,
    Guid PurchaseInvoiceId,
    int SequenceNumber,
    Guid ItemId,
    string ItemName,
    decimal Quantity,
    Guid UnitId,
    string UnitName,
    decimal Price,
    decimal Amount);

public record PurchaseInvoiceSundryDto(
    Guid Id,
    Guid PurchaseInvoiceId,
    int SequenceNumber,
    Guid LedgerId,
    string LedgerName,
    decimal? Percentage,
    decimal Amount);

public record PurchaseInvoiceDto(
    Guid Id,
    Guid CompanyId,
    Guid FinancialYearId,
    Guid? VoucherId,
    string? VoucherNumber,
    string Series,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    Guid SupplierId,
    string SupplierName,
    string MaterialCentre,
    string? Narration,
    PurchaseInvoiceStatus Status,
    decimal ItemSubtotal,
    decimal SundrySubtotal,
    decimal GrandTotal,
    TransportDetailsDto? Transport,
    IReadOnlyList<PurchaseInvoiceItemLineDto> Lines,
    IReadOnlyList<PurchaseInvoiceSundryDto> Sundries,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? ModifiedAt,
    string? ModifiedBy);

public record PurchaseInvoiceSummaryDto(
    Guid Id,
    string Series,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    Guid SupplierId,
    string SupplierName,
    decimal GrandTotal,
    PurchaseInvoiceStatus Status,
    Guid? VoucherId,
    string? VoucherNumber);

public record CreatePurchaseInvoiceItemLineDto(
    Guid ItemId,
    decimal Quantity,
    Guid UnitId,
    decimal Price);

public record CreatePurchaseInvoiceSundryDto(
    Guid LedgerId,
    decimal? Percentage,
    decimal Amount);

public record CreatePurchaseInvoiceDto(
    Guid CompanyId,
    Guid FinancialYearId,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    Guid SupplierId,
    string Series = "Main",
    string MaterialCentre = "Main Store",
    string? Narration = null,
    List<CreatePurchaseInvoiceItemLineDto>? Lines = null,
    List<CreatePurchaseInvoiceSundryDto>? Sundries = null,
    TransportDetailsDto? Transport = null);

public record ItemStockSummaryDto(
    Guid ItemId,
    string ItemName,
    string UnitName,
    decimal CurrentQuantity,
    string MaterialCentre);
