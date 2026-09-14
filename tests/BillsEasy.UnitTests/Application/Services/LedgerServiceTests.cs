using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace BillsEasy.UnitTests.Application.Services;

public class LedgerServiceTests
{
    private readonly ILedgerRepository _ledgerRepo = Substitute.For<ILedgerRepository>();
    private readonly IAccountGroupRepository _groupRepo = Substitute.For<IAccountGroupRepository>();
    private readonly ICurrentUserService _userService = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _dateProvider = Substitute.For<IDateTimeProvider>();
    private readonly LedgerService _service;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public LedgerServiceTests()
    {
        _userService.UserId.Returns("test_user");
        _dateProvider.UtcNow.Returns(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        _service = new LedgerService(_ledgerRepo, _groupRepo, _userService, _dateProvider);
    }

    [Fact]
    public async Task CreateLedger_WithValidData_PersistsAndReturnsDto()
    {
        var group = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset, isPrimary: false);
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns(group);
        _ledgerRepo.ExistsByNameAsync(_companyId, "Ramesh Traders", null, Arg.Any<CancellationToken>()).Returns(false);

        var dto = new CreateLedgerDto(
            _companyId,
            _groupId,
            "Ramesh Traders",
            PrintName: "Ramesh Traders Pvt Ltd",
            OpeningBalance: 12500m,
            OpeningBalanceType: DebitCreditType.Debit,
            BillWiseEnabled: true,
            GSTIN: "24AAACR1234A1Z5");

        var result = await _service.CreateLedgerAsync(dto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Ramesh Traders");
        result.PrintName.Should().Be("Ramesh Traders Pvt Ltd");
        result.GroupName.Should().Be("Sundry Debtors");
        result.OpeningBalance.Should().Be(12500m);
        result.CreatedBy.Should().Be("test_user");

        await _ledgerRepo.Received(1).AddAsync(Arg.Is<Ledger>(l => l.Name == "Ramesh Traders"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLedger_WhenAccountGroupDoesNotExist_ThrowsDomainException()
    {
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns((AccountGroup?)null);

        var dto = new CreateLedgerDto(_companyId, _groupId, "Invalid Group Ledger");
        var act = async () => await _service.CreateLedgerAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*not found*");

        await _ledgerRepo.DidNotReceive().AddAsync(Arg.Any<Ledger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLedger_WhenAccountGroupBelongsToDifferentCompany_ThrowsDomainException()
    {
        var foreignGroup = AccountGroup.Create(Guid.NewGuid(), "Other Co Group", GroupNature.Asset);
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns(foreignGroup);

        var dto = new CreateLedgerDto(_companyId, _groupId, "Cross Co Ledger");
        var act = async () => await _service.CreateLedgerAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*must belong to the same company*");

        await _ledgerRepo.DidNotReceive().AddAsync(Arg.Any<Ledger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateLedger_WhenDuplicateNameExistsInSameCompany_ThrowsDomainException()
    {
        var group = AccountGroup.Create(_companyId, "Sundry Debtors", GroupNature.Asset);
        _groupRepo.GetByIdAsync(_companyId, _groupId, Arg.Any<CancellationToken>()).Returns(group);
        _ledgerRepo.ExistsByNameAsync(_companyId, "Duplicate Ledger", null, Arg.Any<CancellationToken>()).Returns(true);

        var dto = new CreateLedgerDto(_companyId, _groupId, "Duplicate Ledger");
        var act = async () => await _service.CreateLedgerAsync(dto);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already exists in this company*");

        await _ledgerRepo.DidNotReceive().AddAsync(Arg.Any<Ledger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenProtectedSystemLedger_ThrowsDomainException()
    {
        var sysLedger = Ledger.Create(_companyId, _groupId, "Cash", isDefaultSystemLedger: true);
        _ledgerRepo.GetByIdAsync(_companyId, sysLedger.Id, Arg.Any<CancellationToken>()).Returns(sysLedger);

        var act = async () => await _service.DeleteLedgerAsync(_companyId, sysLedger.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*System predefined ledger*cannot be deleted*");

        await _ledgerRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenHasTransactions_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Active Customer");
        _ledgerRepo.GetByIdAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        _ledgerRepo.HasTransactionsAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteLedgerAsync(_companyId, ledger.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more recorded financial transactions*");

        await _ledgerRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenHasBillReferences_ThrowsDomainException()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Party With Bills");
        _ledgerRepo.GetByIdAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        _ledgerRepo.HasTransactionsAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(false);
        _ledgerRepo.HasBillReferencesAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.DeleteLedgerAsync(_companyId, ledger.Id);

        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*because it has one or more bill-wise references*");

        await _ledgerRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLedger_WhenUnusedUserCreatedLedger_DeletesSuccessfully()
    {
        var ledger = Ledger.Create(_companyId, _groupId, "Unused Supplier", isDefaultSystemLedger: false);
        _ledgerRepo.GetByIdAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        _ledgerRepo.HasTransactionsAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(false);
        _ledgerRepo.HasBillReferencesAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>()).Returns(false);

        await _service.DeleteLedgerAsync(_companyId, ledger.Id);

        await _ledgerRepo.Received(1).DeleteAsync(_companyId, ledger.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedDefaultLedgers_WhenNoLedgersExist_SeedsCashAndProfitLoss()
    {
        _ledgerRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<Ledger>());

        var cashGroup = AccountGroup.Create(_companyId, "Cash-in-hand", GroupNature.Asset, isDefaultSystemGroup: true);
        var plGroup = AccountGroup.Create(_companyId, "Capital Account", GroupNature.Capital, isDefaultSystemGroup: true);

        _groupRepo.GetAllAsync(_companyId, Arg.Any<CancellationToken>()).Returns(new List<AccountGroup> { cashGroup, plGroup });

        await _service.SeedDefaultLedgersAsync(_companyId, "admin");

        await _ledgerRepo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<Ledger>>(list => list.Any(l => l.Name == "Cash" && l.IsDefaultSystemLedger) &&
                                                list.Any(l => l.Name == "Profit & Loss Account" && l.IsDefaultSystemLedger)),
            Arg.Any<CancellationToken>());
    }
}