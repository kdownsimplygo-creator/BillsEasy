using System;
using System.Linq;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Entities;

public class PurchaseInvoiceTests
{
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _financialYearId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly DateOnly _invoiceDate = new(2023, 4, 6);

    [Fact]
    public void Create_ValidParameters_InstantiatesWithActiveStatusAndZeroTotals()
    {
        var invoice = PurchaseInvoice.Create(
            _companyId,
            _financialYearId,
            "Cust/123",
            _invoiceDate,
            _supplierId,
            series: "Main",
            materialCentre: "Main Store",
            narration: "Good purchase from yashika");

        invoice.CompanyId.Should().Be(_companyId);
        invoice.FinancialYearId.Should().Be(_financialYearId);
        invoice.SupplierId.Should().Be(_supplierId);
        invoice.InvoiceNumber.Should().Be("Cust/123");
        invoice.Series.Should().Be("Main");
        invoice.MaterialCentre.Should().Be("Main Store");
        invoice.Narration.Should().Be("Good purchase from yashika");
        invoice.Status.Should().Be(PurchaseInvoiceStatus.Active);
        invoice.ItemSubtotal.Amount.Should().Be(0m);
        invoice.SundrySubtotal.Amount.Should().Be(0m);
        invoice.GrandTotal.Amount.Should().Be(0m);
        invoice.Lines.Should().BeEmpty();
        invoice.Sundries.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyInvoiceNumber_ThrowsDomainException(string? invalidNumber)
    {
        var act = () => PurchaseInvoice.Create(
            _companyId,
            _financialYearId,
            invalidNumber!,
            _invoiceDate,
            _supplierId);

        act.Should().Throw<DomainException>()
            .WithMessage("*InvoiceNumber cannot be empty*");
    }

    [Fact]
    public void Create_EmptyCompanyId_ThrowsDomainException()
    {
        var act = () => PurchaseInvoice.Create(
            Guid.Empty,
            _financialYearId,
            "Cust/123",
            _invoiceDate,
            _supplierId);

        act.Should().Throw<DomainException>()
            .WithMessage("*CompanyId cannot be empty*");
    }

    [Fact]
    public void Create_EmptySupplierId_ThrowsDomainException()
    {
        var act = () => PurchaseInvoice.Create(
            _companyId,
            _financialYearId,
            "Cust/123",
            _invoiceDate,
            Guid.Empty);

        act.Should().Throw<DomainException>()
            .WithMessage("*SupplierId cannot be empty*");
    }

    [Fact]
    public void AddItemLine_RecalculatesItemSubtotalAndGrandTotal()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);

        var line1 = invoice.AddItemLine(Guid.NewGuid(), 5m, Guid.NewGuid(), 5500m);
        line1.SequenceNumber.Should().Be(1);
        invoice.ItemSubtotal.Amount.Should().Be(27500m);
        invoice.GrandTotal.Amount.Should().Be(27500m);

        var line2 = invoice.AddItemLine(Guid.NewGuid(), 1m, Guid.NewGuid(), 40000m);
        line2.SequenceNumber.Should().Be(2);
        invoice.ItemSubtotal.Amount.Should().Be(67500m);
        invoice.GrandTotal.Amount.Should().Be(67500m);
    }

    [Fact]
    public void AddSundryLine_RecalculatesSundrySubtotalAndGrandTotal()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);
        invoice.AddItemLine(Guid.NewGuid(), 5m, Guid.NewGuid(), 5500m); // 27,500
        invoice.AddItemLine(Guid.NewGuid(), 1m, Guid.NewGuid(), 40000m); // 40,000

        var sundry = invoice.AddSundryLine(Guid.NewGuid(), 500m);

        sundry.SequenceNumber.Should().Be(1);
        invoice.ItemSubtotal.Amount.Should().Be(67500m);
        invoice.SundrySubtotal.Amount.Should().Be(500m);
        invoice.GrandTotal.Amount.Should().Be(68000m);
    }

    [Fact]
    public void RemoveItemLine_F9Behavior_ReSequencesLinesAndRecalculatesTotals()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);
        var item1 = Guid.NewGuid();
        var item2 = Guid.NewGuid();
        var item3 = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        invoice.AddItemLine(item1, 5m, unitId, 5500m); // seq 1: 27,500
        invoice.AddItemLine(item2, 2m, unitId, 6000m); // seq 2: 12,000 (unwanted)
        invoice.AddItemLine(item3, 1m, unitId, 40000m); // seq 3: 40,000

        invoice.Lines.Should().HaveCount(3);
        invoice.ItemSubtotal.Amount.Should().Be(79500m);

        // Delete line 2 (F9 line deletion)
        invoice.RemoveItemLine(sequenceNumber: 2);

        invoice.Lines.Should().HaveCount(2);
        invoice.Lines.ElementAt(0).ItemId.Should().Be(item1);
        invoice.Lines.ElementAt(0).SequenceNumber.Should().Be(1);
        invoice.Lines.ElementAt(1).ItemId.Should().Be(item3);
        invoice.Lines.ElementAt(1).SequenceNumber.Should().Be(2);

        invoice.ItemSubtotal.Amount.Should().Be(67500m);
        invoice.GrandTotal.Amount.Should().Be(67500m);
    }

    [Fact]
    public void SetVoucherId_ValidId_SetsProperty()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);
        var voucherId = Guid.NewGuid();

        invoice.SetVoucherId(voucherId);
        invoice.VoucherId.Should().Be(voucherId);
    }

    [Fact]
    public void SetTransportDetails_SetsProperty()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);
        var transport = new TransportDetails(
            transportName: "Fast Freight",
            grRrNumber: "GR-1234",
            transportDate: _invoiceDate,
            vehicleNumber: "DL-01-AB-1234",
            stationFromPlace: "Delhi");

        invoice.SetTransportDetails(transport);
        invoice.Transport.Should().NotBeNull();
        invoice.Transport!.TransportName.Should().Be("Fast Freight");
        invoice.Transport.VehicleNumber.Should().Be("DL-01-AB-1234");
    }

    [Fact]
    public void Cancel_WhenActive_TransitionsToCancelled()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);
        var now = DateTime.UtcNow;

        invoice.Cancel("admin", now);

        invoice.Status.Should().Be(PurchaseInvoiceStatus.Cancelled);
        invoice.ModifiedBy.Should().Be("admin");
        invoice.ModifiedAt.Should().Be(now);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsDomainException()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);
        invoice.Cancel("admin", DateTime.UtcNow);

        var act = () => invoice.Cancel("admin", DateTime.UtcNow);
        act.Should().Throw<DomainException>()
            .WithMessage("*already cancelled*");
    }

    [Fact]
    public void AddItemLine_WhenCancelled_ThrowsDomainException()
    {
        var invoice = PurchaseInvoice.Create(_companyId, _financialYearId, "Cust/123", _invoiceDate, _supplierId);
        invoice.Cancel("admin", DateTime.UtcNow);

        var act = () => invoice.AddItemLine(Guid.NewGuid(), 1m, Guid.NewGuid(), 100m);
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot modify lines of an inactive or cancelled invoice*");
    }
}
