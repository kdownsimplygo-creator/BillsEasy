using System;

namespace BillsEasy.Application.DTOs;

public record DashboardFinancialSummaryDto(
    decimal CashBalance,
    string CashBalanceType,
    decimal BankBalance,
    string BankBalanceType,
    decimal ReceivablesBalance,
    string ReceivablesBalanceType,
    decimal PayablesBalance,
    string PayablesBalanceType);

public record DashboardStockItemDto(
    Guid ItemId,
    string Name,
    string? Code,
    decimal Quantity,
    string UnitName,
    string Status);
