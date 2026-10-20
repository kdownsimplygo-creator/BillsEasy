using System;
using System.Collections.Generic;
using System.Linq;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Domain.Entities;

/// <summary>
/// Commercial Purchase Invoice aggregate root.
/// Directly implements the Class 8 Purchase Voucher workflow:
/// header details, item lines, bill sundries, grand total, and logistics/transport details.
/// </summary>
public class PurchaseInvoice : AuditableEntity<Guid>
{
    public Guid CompanyId { get; private set; }
    public Guid FinancialYearId { get; private set; }
    public Guid? VoucherId { get; private set; }
    public string Series { get; private set; }
    public string InvoiceNumber { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public Guid SupplierId { get; private set; }
    public string MaterialCentre { get; private set; }
    public string? Narration { get; private set; }
    public Money ItemSubtotal { get; private set; }
    public Money SundrySubtotal { get; private set; }
    public Money GrandTotal { get; private set; }
    public PurchaseInvoiceStatus Status { get; private set; }
    public TransportDetails? Transport { get; private set; }

    private readonly List<PurchaseInvoiceItemLine> _lines = new();
    public IReadOnlyCollection<PurchaseInvoiceItemLine> Lines => _lines.AsReadOnly();

    private readonly List<PurchaseInvoiceSundryLine> _sundries = new();
    public IReadOnlyCollection<PurchaseInvoiceSundryLine> Sundries => _sundries.AsReadOnly();

    private PurchaseInvoice() : base()
    {
        Series = "Main";
        MaterialCentre = "Main Store";
        InvoiceNumber = string.Empty;
        Status = PurchaseInvoiceStatus.Active;
        ItemSubtotal = Money.Zero();
        SundrySubtotal = Money.Zero();
        GrandTotal = Money.Zero();
    }

    public PurchaseInvoice(
        Guid id,
        Guid companyId,
        Guid financialYearId,
        string invoiceNumber,
        DateOnly invoiceDate,
        Guid supplierId,
        string series = "Main",
        string materialCentre = "Main Store",
        string? narration = null,
        PurchaseInvoiceStatus status = PurchaseInvoiceStatus.Active,
        Guid? voucherId = null,
        TransportDetails? transport = null) : base(id)
    {
        if (companyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (financialYearId == Guid.Empty)
            throw new DomainException("FinancialYearId cannot be empty.");

        if (supplierId == Guid.Empty)
            throw new DomainException("SupplierId cannot be empty.");

        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new DomainException("InvoiceNumber cannot be empty.");

        var trimmedNumber = invoiceNumber.Trim();
        if (trimmedNumber.Length > 50)
            throw new DomainException("InvoiceNumber cannot exceed 50 characters.");

        var trimmedSeries = string.IsNullOrWhiteSpace(series) ? "Main" : series.Trim();
        if (trimmedSeries.Length > 50)
            throw new DomainException("Series cannot exceed 50 characters.");

        var trimmedCentre = string.IsNullOrWhiteSpace(materialCentre) ? "Main Store" : materialCentre.Trim();
        if (trimmedCentre.Length > 100)
            throw new DomainException("MaterialCentre cannot exceed 100 characters.");

        if (narration != null && narration.Trim().Length > 1000)
            throw new DomainException("Narration cannot exceed 1000 characters.");

        CompanyId = companyId;
        FinancialYearId = financialYearId;
        InvoiceNumber = trimmedNumber;
        InvoiceDate = invoiceDate;
        SupplierId = supplierId;
        Series = trimmedSeries;
        MaterialCentre = trimmedCentre;
        Narration = narration?.Trim();
        Status = status;
        VoucherId = voucherId;
        Transport = transport;
        ItemSubtotal = Money.Zero();
        SundrySubtotal = Money.Zero();
        GrandTotal = Money.Zero();
    }

    public static PurchaseInvoice Create(
        Guid companyId,
        Guid financialYearId,
        string invoiceNumber,
        DateOnly invoiceDate,
        Guid supplierId,
        string series = "Main",
        string materialCentre = "Main Store",
        string? narration = null,
        TransportDetails? transport = null)
    {
        return new PurchaseInvoice(
            Guid.NewGuid(),
            companyId,
            financialYearId,
            invoiceNumber,
            invoiceDate,
            supplierId,
            series,
            materialCentre,
            narration,
            PurchaseInvoiceStatus.Active,
            voucherId: null,
            transport: transport);
    }

    public PurchaseInvoiceItemLine AddItemLine(Guid itemId, decimal quantity, Guid unitId, Money price)
    {
        if (Status != PurchaseInvoiceStatus.Active)
            throw new DomainException("Cannot modify lines of an inactive or cancelled invoice.");

        var sequence = _lines.Count + 1;
        var line = new PurchaseInvoiceItemLine(Guid.NewGuid(), Id, sequence, itemId, quantity, unitId, price);
        _lines.Add(line);
        RecalculateTotals();
        return line;
    }

    public void RemoveItemLine(int sequenceNumber)
    {
        if (Status != PurchaseInvoiceStatus.Active)
            throw new DomainException("Cannot modify lines of an inactive or cancelled invoice.");

        var line = _lines.FirstOrDefault(l => l.SequenceNumber == sequenceNumber);
        if (line == null)
            throw new DomainException($"Item line with sequence number {sequenceNumber} not found.");

        _lines.Remove(line);

        // Re-sequence remaining lines cleanly (1..N)
        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i].UpdateSequenceNumber(i + 1);
        }

        RecalculateTotals();
    }

    public PurchaseInvoiceSundryLine AddSundryLine(Guid ledgerId, Money amount, decimal? percentage = null)
    {
        if (Status != PurchaseInvoiceStatus.Active)
            throw new DomainException("Cannot modify sundries of an inactive or cancelled invoice.");

        var sequence = _sundries.Count + 1;
        var sundry = new PurchaseInvoiceSundryLine(Guid.NewGuid(), Id, sequence, ledgerId, amount, percentage);
        _sundries.Add(sundry);
        RecalculateTotals();
        return sundry;
    }

    public void RemoveSundryLine(int sequenceNumber)
    {
        if (Status != PurchaseInvoiceStatus.Active)
            throw new DomainException("Cannot modify sundries of an inactive or cancelled invoice.");

        var sundry = _sundries.FirstOrDefault(s => s.SequenceNumber == sequenceNumber);
        if (sundry == null)
            throw new DomainException($"Sundry line with sequence number {sequenceNumber} not found.");

        _sundries.Remove(sundry);

        for (int i = 0; i < _sundries.Count; i++)
        {
            _sundries[i].UpdateSequenceNumber(i + 1);
        }

        RecalculateTotals();
    }

    public void SetVoucherId(Guid voucherId)
    {
        if (voucherId == Guid.Empty)
            throw new DomainException("VoucherId cannot be empty.");

        VoucherId = voucherId;
    }

    public void SetTransportDetails(TransportDetails? transport)
    {
        Transport = transport;
    }

    public void Cancel(string cancelledBy, DateTime cancelledAtUtc)
    {
        if (Status == PurchaseInvoiceStatus.Cancelled)
            throw new DomainException("Purchase invoice is already cancelled.");

        Status = PurchaseInvoiceStatus.Cancelled;
        ModifiedBy = cancelledBy;
        ModifiedAt = cancelledAtUtc;
    }

    public void LoadLines(IEnumerable<PurchaseInvoiceItemLine> lines)
    {
        _lines.Clear();
        _lines.AddRange(lines.OrderBy(l => l.SequenceNumber));
        RecalculateTotals();
    }

    public void LoadSundries(IEnumerable<PurchaseInvoiceSundryLine> sundries)
    {
        _sundries.Clear();
        _sundries.AddRange(sundries.OrderBy(s => s.SequenceNumber));
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        var itemTotal = Money.Zero();
        foreach (var line in _lines)
        {
            itemTotal += line.Amount;
        }

        var sundryTotal = Money.Zero();
        foreach (var sundry in _sundries)
        {
            sundryTotal += sundry.Amount;
        }

        ItemSubtotal = itemTotal;
        SundrySubtotal = sundryTotal;
        GrandTotal = itemTotal + sundryTotal;
    }
}
