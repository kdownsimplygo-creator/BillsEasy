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

public class PostingServiceTests
{
    private readonly IVoucherRepository _voucherRepo = Substitute.For<IVoucherRepository>();
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly IFinancialYearRepository _financialYearRepo = Substitute.For<IFinancialYearRepository>();
    private readonly ILedgerRepository _ledgerRepo = Substitute.For<ILedgerRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly PostingService _service;

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _financialYearId = Guid.NewGuid();
    private readonly Guid _cashLedgerId = Guid.NewGuid();
    private readonly Guid _salesLedgerId = Guid.NewGuid();
    private readonly FinancialYear _validFinancialYear;
    private readonly Company _validCompany;
    private readonly Ledger _cashLedger;
    private readonly Ledger _salesLedger;

    public PostingServiceTests()
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

        _validFinancialYear = new FinancialYear(
            _financialYearId,
            _companyId,
            "FY 2026-27",
            new DateOnly(2026, 4, 1),
            new DateOnly(2027, 3, 31),
            isActive: true,
            isLocked: false);

        var assetGroup = AccountGroup.Create(_companyId, "Cash-in-hand", GroupNature.Asset, isPrimary: true);
        var incomeGroup = AccountGroup.Create(_companyId, "Sales Accounts", GroupNature.Income, isPrimary: true);

        _cashLedger = Ledger.Create(_companyId, assetGroup.Id, "Cash A/c");
        _salesLedger = Ledger.Create(_companyId, incomeGroup.Id, "Sales A/c");

        _service = new PostingService(
            _voucherRepo,
            _companyRepo,
            _financialYearRepo,
            _ledgerRepo,
            _userService,
            _dateProvider);
    }

    [Fact]
    public async Task PostVoucherAsync_WithValidBalancedVoucher_SuccessfullyPostsAndPersists()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(_validFinancialYear);
        _voucherRepo.ExistsByNumberAsync(_companyId, _financialYearId, "RC-001", null, Arg.Any<CancellationToken>()).Returns(false);
        _ledgerRepo.GetByIdAsync(_companyId, _cashLedgerId, Arg.Any<CancellationToken>()).Returns(_cashLedger);
        _ledgerRepo.GetByIdAsync(_companyId, _salesLedgerId, Arg.Any<CancellationToken>()).Returns(_salesLedger);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            "BILL-101",
            "Cash sales receipt",
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1500m, "Debit Cash"),
                new(_salesLedgerId, DebitCreditType.Credit, 1500m, "Credit Sales")
            });

        var result = await _service.PostVoucherAsync(dto);

        result.Should().NotBeNull();
        result.VoucherNumber.Should().Be("RC-001");
        result.Type.Should().Be(VoucherType.Receipt);
        result.Status.Should().Be(VoucherStatus.Posted);
        result.TotalAmount.Should().Be(1500m);
        result.PostedBy.Should().Be("accountant");
        result.Lines.Should().HaveCount(2);

        await _voucherRepo.Received(1).AddAsync(
            Arg.Is<Voucher>(v => v.VoucherNumber == "RC-001" && v.Status == VoucherStatus.Posted && v.IsBalanced),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostVoucherAsync_WhenCompanyNotFound_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns((Company?)null);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Company with ID*not found*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenFinancialYearNotFound_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns((FinancialYear?)null);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Financial year with ID*not found*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenCompanyIdMismatchWithFinancialYear_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);

        var foreignCompanyFy = new FinancialYear(
            _financialYearId,
            Guid.NewGuid(), // Foreign CompanyId
            "FY 2026-27",
            new DateOnly(2026, 4, 1),
            new DateOnly(2027, 3, 31));

        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(foreignCompanyFy);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Voucher CompanyId does not match FinancialYear CompanyId*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenFinancialYearIsLocked_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);

        var lockedFy = new FinancialYear(
            _financialYearId,
            _companyId,
            "FY 2026-27",
            new DateOnly(2026, 4, 1),
            new DateOnly(2027, 3, 31),
            isActive: true,
            isLocked: true);

        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(lockedFy);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*is locked. Posting is prohibited*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenVoucherDateOutsideFinancialYear_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(_validFinancialYear);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2025, 3, 31), // Prior to 2026-04-01
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*outside financial year*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenDuplicateVoucherNumberInFinancialYear_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(_validFinancialYear);
        _voucherRepo.ExistsByNumberAsync(_companyId, _financialYearId, "RC-001", null, Arg.Any<CancellationToken>()).Returns(true);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists in financial year*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenLedgerNotFound_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(_validFinancialYear);
        _voucherRepo.ExistsByNumberAsync(_companyId, _financialYearId, "RC-001", null, Arg.Any<CancellationToken>()).Returns(false);
        _ledgerRepo.GetByIdAsync(_companyId, _cashLedgerId, Arg.Any<CancellationToken>()).Returns((Ledger?)null);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found in company*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenLedgerBelongsToDifferentCompany_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(_validFinancialYear);
        _voucherRepo.ExistsByNumberAsync(_companyId, _financialYearId, "RC-001", null, Arg.Any<CancellationToken>()).Returns(false);

        var foreignCompanyLedger = Ledger.Create(Guid.NewGuid(), Guid.NewGuid(), "Other Co Ledger");
        _ledgerRepo.GetByIdAsync(_companyId, _cashLedgerId, Arg.Any<CancellationToken>()).Returns(foreignCompanyLedger);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1000m),
                new(_salesLedgerId, DebitCreditType.Credit, 1000m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*All ledgers must belong to the same company*");
    }

    [Fact]
    public async Task PostVoucherAsync_WhenUnbalanced_ThrowsDomainException()
    {
        _companyRepo.GetByIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(_validCompany);
        _financialYearRepo.GetByIdAsync(_financialYearId, Arg.Any<CancellationToken>()).Returns(_validFinancialYear);
        _voucherRepo.ExistsByNumberAsync(_companyId, _financialYearId, "RC-001", null, Arg.Any<CancellationToken>()).Returns(false);
        _ledgerRepo.GetByIdAsync(_companyId, _cashLedgerId, Arg.Any<CancellationToken>()).Returns(_cashLedger);
        _ledgerRepo.GetByIdAsync(_companyId, _salesLedgerId, Arg.Any<CancellationToken>()).Returns(_salesLedger);

        var dto = new CreateVoucherDto(
            _companyId,
            _financialYearId,
            "RC-001",
            VoucherType.Receipt,
            new DateOnly(2026, 5, 2),
            null,
            null,
            new List<CreateVoucherLineDto>
            {
                new(_cashLedgerId, DebitCreditType.Debit, 1500m),
                new(_salesLedgerId, DebitCreditType.Credit, 1400m)
            });

        var act = () => _service.PostVoucherAsync(dto);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*Total Debit (1500.00) must equal Total Credit (1400.00)*");
    }

    [Fact]
    public async Task CancelVoucherAsync_WhenVoucherExists_CancelsSuccessfully()
    {
        var voucherId = Guid.NewGuid();
        var voucher = Voucher.Create(_companyId, _financialYearId, "JV-100", VoucherType.Journal, new DateOnly(2026, 5, 2));
        voucher.AddLine(_cashLedgerId, DebitCreditType.Debit, new Money(500m));
        voucher.AddLine(_salesLedgerId, DebitCreditType.Credit, new Money(500m));
        voucher.Post(_validFinancialYear);

        _voucherRepo.GetByIdAsync(_companyId, voucherId, Arg.Any<CancellationToken>()).Returns(voucher);

        var result = await _service.CancelVoucherAsync(_companyId, voucherId);

        result.Status.Should().Be(VoucherStatus.Cancelled);
        await _voucherRepo.Received(1).UpdateAsync(Arg.Is<Voucher>(v => v.Status == VoucherStatus.Cancelled), Arg.Any<CancellationToken>());
    }
}
