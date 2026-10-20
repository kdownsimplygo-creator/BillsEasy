using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class BillWiseServiceTests
{
    private readonly IBillReferenceRepository _billRefRepo = Substitute.For<IBillReferenceRepository>();
    private readonly IBillAllocationRepository _billAllocRepo = Substitute.For<IBillAllocationRepository>();
    private readonly IVoucherRepository _voucherRepo = Substitute.For<IVoucherRepository>();
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly ILedgerRepository _ledgerRepo = Substitute.For<ILedgerRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();

    private readonly BillWiseService _service;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _partyLedgerId = Guid.NewGuid();
    private readonly Guid _nonBillWiseLedgerId = Guid.NewGuid();
    private readonly Company _validCompany;
    private readonly Ledger _partyLedger;
    private readonly Ledger _nonBillWiseLedger;

    public BillWiseServiceTests()
    {
        _userService.UserName.Returns("accountant");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc));

        _validCompany = Company.Create(
            "Acme Traders",
            "Acme Traders",
            null,
            "Maharashtra",
            "27",
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 1));

        var sundryDebtorsGroup = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset, isPrimary: true);

        _partyLedger = Ledger.Create(
            _companyId,
            sundryDebtorsGroup.Id,
            "Customer A",
            billWiseEnabled: true);

        _nonBillWiseLedger = Ledger.Create(
            _companyId,
            sundryDebtorsGroup.Id,
            "General Expense",
            billWiseEnabled: false);

        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(_validCompany);

        _ledgerRepo.GetByIdAsync(_companyId, _partyLedgerId, Arg.Any<CancellationToken>())
            .Returns(_partyLedger);

        _ledgerRepo.GetByIdAsync(_companyId, _nonBillWiseLedgerId, Arg.Any<CancellationToken>())
            .Returns(_nonBillWiseLedger);

        _service = new BillWiseService(
            _billRefRepo,
            _billAllocRepo,
            _voucherRepo,
            _companyRepo,
            _ledgerRepo,
            _userService,
            _dateProvider);
    }

    [Fact]
    public async Task CreateBillReference_WithBillWiseEnabledLedger_Succeeds()
    {
        var dto = new CreateBillReferenceDto(
            _companyId,
            _partyLedgerId,
            "INV-101",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            1500m,
            DebitCreditType.Debit,
            new DateOnly(2026, 5, 31));

        var result = await _service.CreateBillReferenceAsync(dto);

        result.Should().NotBeNull();
        result.ReferenceNumber.Should().Be("INV-101");
        result.OriginalAmount.Should().Be(1500m);
        result.Status.Should().Be(BillReferenceStatus.Active);
        result.DueDate.Should().Be(new DateOnly(2026, 5, 31));
        await _billRefRepo.Received(1).AddAsync(Arg.Any<BillReference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBillReference_WhenBillWiseDisabledOnLedger_ThrowsDomainException()
    {
        var dto = new CreateBillReferenceDto(
            _companyId,
            _nonBillWiseLedgerId,
            "INV-101",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            1500m,
            DebitCreditType.Debit);

        var act = () => _service.CreateBillReferenceAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*does not have bill-by-bill tracking enabled*");
    }

    [Fact]
    public async Task CreateBillReference_WhenDuplicateReferenceNumber_ThrowsDomainException()
    {
        _billRefRepo.ExistsByReferenceNumberAsync(_companyId, _partyLedgerId, "INV-DUPE", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        var dto = new CreateBillReferenceDto(
            _companyId,
            _partyLedgerId,
            "INV-DUPE",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            1000m,
            DebitCreditType.Debit);

        var act = () => _service.CreateBillReferenceAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists for ledger*");
    }

    [Fact]
    public async Task CreateBillReference_WhenSourceVoucherLineLedgerMismatch_ThrowsDomainException()
    {
        var lineId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var differentLedgerId = Guid.NewGuid();
        var dummyLine = new VoucherLine(lineId, voucherId, differentLedgerId, 1, DebitCreditType.Debit, new Money(1000m));

        _voucherRepo.GetLineWithCompanyAsync(lineId, Arg.Any<CancellationToken>())
            .Returns((dummyLine, _companyId));

        var dto = new CreateBillReferenceDto(
            _companyId,
            _partyLedgerId,
            "INV-102",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            1000m,
            DebitCreditType.Debit,
            SourceVoucherLineId: lineId);

        var act = () => _service.CreateBillReferenceAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Source voucher line ledger does not match bill reference ledger*");
    }

    [Fact]
    public async Task AllocateBill_WhenAllocationWithinOutstandingBalance_Succeeds()
    {
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();

        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit);

        var settlementLine = new VoucherLine(lineId, voucherId, _partyLedgerId, 1, DebitCreditType.Credit, new Money(300m));

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _voucherRepo.GetLineWithCompanyAsync(lineId, Arg.Any<CancellationToken>())
            .Returns((settlementLine, _companyId));

        _billRefRepo.GetOutstandingBalanceAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(new Money(500m));

        _billAllocRepo.GetByVoucherLineIdAsync(_companyId, lineId, Arg.Any<CancellationToken>())
            .Returns(new List<BillAllocation>());

        var dto = new CreateBillAllocationDto(_companyId, lineId, billId, 300m);
        var result = await _service.AllocateBillAsync(dto);

        result.Should().NotBeNull();
        result.AllocatedAmount.Should().Be(300m);
        await _billAllocRepo.Received(1).AddAsync(Arg.Any<BillAllocation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AllocateBill_WhenAllocationExceedsOutstandingBalance_ThrowsDomainException()
    {
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();

        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit);

        var settlementLine = new VoucherLine(lineId, voucherId, _partyLedgerId, 1, DebitCreditType.Credit, new Money(1000m));

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _voucherRepo.GetLineWithCompanyAsync(lineId, Arg.Any<CancellationToken>())
            .Returns((settlementLine, _companyId));

        // Outstanding is only 200 (partially settled previously)
        _billRefRepo.GetOutstandingBalanceAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(new Money(200m));

        var dto = new CreateBillAllocationDto(_companyId, lineId, billId, 250m);
        var act = () => _service.AllocateBillAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*exceeds current outstanding balance*");
    }

    [Fact]
    public async Task AllocateBill_WhenBillIsCancelled_ThrowsDomainException()
    {
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit,
            status: BillReferenceStatus.Cancelled);

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        var dto = new CreateBillAllocationDto(_companyId, lineId, billId, 100m);
        var act = () => _service.AllocateBillAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*is in 'Cancelled' status*");
    }

    [Fact]
    public async Task AllocateBill_WhenCrossLedgerPartyMismatch_ThrowsDomainException()
    {
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var differentPartyLedgerId = Guid.NewGuid();

        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId, // Customer A
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit);

        // Voucher line is for Customer B
        var settlementLine = new VoucherLine(lineId, voucherId, differentPartyLedgerId, 1, DebitCreditType.Credit, new Money(300m));

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _voucherRepo.GetLineWithCompanyAsync(lineId, Arg.Any<CancellationToken>())
            .Returns((settlementLine, _companyId));

        var dto = new CreateBillAllocationDto(_companyId, lineId, billId, 300m);
        var act = () => _service.AllocateBillAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Cross-ledger allocation is prohibited*");
    }

    [Fact]
    public async Task AllocateBill_WhenCrossCompanyMismatch_ThrowsDomainException()
    {
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();
        var differentCompanyId = Guid.NewGuid();

        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit);

        var settlementLine = new VoucherLine(lineId, voucherId, _partyLedgerId, 1, DebitCreditType.Credit, new Money(300m));

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        // Voucher belongs to different company
        _voucherRepo.GetLineWithCompanyAsync(lineId, Arg.Any<CancellationToken>())
            .Returns((settlementLine, differentCompanyId));

        var dto = new CreateBillAllocationDto(_companyId, lineId, billId, 300m);
        var act = () => _service.AllocateBillAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Cross-company allocation is prohibited*");
    }

    [Fact]
    public async Task AllocateBill_WhenDirectionMismatch_DebitBillSettledByDebitLine_ThrowsDomainException()
    {
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();

        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit);

        // Line is Debit instead of Credit
        var invalidLine = new VoucherLine(lineId, voucherId, _partyLedgerId, 1, DebitCreditType.Debit, new Money(300m));

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _voucherRepo.GetLineWithCompanyAsync(lineId, Arg.Any<CancellationToken>())
            .Returns((invalidLine, _companyId));

        var dto = new CreateBillAllocationDto(_companyId, lineId, billId, 300m);
        var act = () => _service.AllocateBillAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Allocation direction mismatch*");
    }

    [Fact]
    public async Task AllocateBill_WhenExceedsVoucherLineAmount_ThrowsDomainException()
    {
        var billId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var voucherId = Guid.NewGuid();

        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(5000m),
            DebitCreditType.Debit);

        var settlementLine = new VoucherLine(lineId, voucherId, _partyLedgerId, 1, DebitCreditType.Credit, new Money(200m));

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _voucherRepo.GetLineWithCompanyAsync(lineId, Arg.Any<CancellationToken>())
            .Returns((settlementLine, _companyId));

        _billRefRepo.GetOutstandingBalanceAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(new Money(5000m));

        _billAllocRepo.GetByVoucherLineIdAsync(_companyId, lineId, Arg.Any<CancellationToken>())
            .Returns(new List<BillAllocation>());

        // Allocate 300 against a 200 voucher line
        var dto = new CreateBillAllocationDto(_companyId, lineId, billId, 300m);
        var act = () => _service.AllocateBillAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Total allocated amount cannot exceed the voucher line amount*");
    }

    [Fact]
    public async Task CancelBillReference_WhenAllocationsExist_ThrowsDomainException()
    {
        var billId = Guid.NewGuid();
        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit);

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _billRefRepo.GetTotalAllocatedAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(new Money(150m));

        var act = () => _service.CancelBillReferenceAsync(_companyId, billId);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*has active settlement allocations*");
    }

    [Fact]
    public async Task CancelBillReference_WhenZeroAllocations_SoftCancelsBill()
    {
        var billId = Guid.NewGuid();
        var bill = new BillReference(
            billId,
            _companyId,
            _partyLedgerId,
            "INV-201",
            BillReferenceType.NewRef,
            new DateOnly(2026, 5, 1),
            new Money(500m),
            DebitCreditType.Debit);

        _billRefRepo.GetByIdAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(bill);

        _billRefRepo.GetTotalAllocatedAsync(_companyId, billId, Arg.Any<CancellationToken>())
            .Returns(Money.Zero());

        await _service.CancelBillReferenceAsync(_companyId, billId);

        bill.Status.Should().Be(BillReferenceStatus.Cancelled);
        await _billRefRepo.Received(1).UpdateAsync(bill, Arg.Any<CancellationToken>());
    }
}
