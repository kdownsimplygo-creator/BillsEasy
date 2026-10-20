using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Domain.ValueObjects;

namespace BillsEasy.Application.Services;

public class PurchaseService : IPurchaseService
{
    private readonly ISqliteDbConnectionFactory _connectionFactory;
    private readonly IPurchaseInvoiceRepository _purchaseInvoiceRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IPostingService _postingService;
    private readonly IBillWiseService _billWiseService;
    private readonly ICompanyRepository _companyRepository;
    private readonly IFinancialYearRepository _financialYearRepository;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly IAccountGroupRepository _accountGroupRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PurchaseService(
        ISqliteDbConnectionFactory connectionFactory,
        IPurchaseInvoiceRepository purchaseInvoiceRepository,
        IStockMovementRepository stockMovementRepository,
        IVoucherRepository voucherRepository,
        IPostingService postingService,
        IBillWiseService billWiseService,
        ICompanyRepository companyRepository,
        IFinancialYearRepository financialYearRepository,
        ILedgerRepository ledgerRepository,
        IAccountGroupRepository accountGroupRepository,
        IItemRepository itemRepository,
        IUnitRepository unitRepository,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _connectionFactory = connectionFactory;
        _purchaseInvoiceRepository = purchaseInvoiceRepository;
        _stockMovementRepository = stockMovementRepository;
        _voucherRepository = voucherRepository;
        _postingService = postingService;
        _billWiseService = billWiseService;
        _companyRepository = companyRepository;
        _financialYearRepository = financialYearRepository;
        _ledgerRepository = ledgerRepository;
        _accountGroupRepository = accountGroupRepository;
        _itemRepository = itemRepository;
        _unitRepository = unitRepository;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PurchaseInvoiceDto> CreatePurchaseInvoiceAsync(CreatePurchaseInvoiceDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CompanyId == Guid.Empty)
            throw new DomainException("CompanyId cannot be empty.");

        if (dto.FinancialYearId == Guid.Empty)
            throw new DomainException("FinancialYearId cannot be empty.");

        if (dto.SupplierId == Guid.Empty)
            throw new DomainException("SupplierId cannot be empty.");

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            throw new DomainException("Invoice number cannot be empty.");

        if (dto.Lines == null || dto.Lines.Count == 0)
            throw new DomainException("Purchase invoice must contain at least one item line.");

        var company = await _companyRepository.GetByIdAsync(dto.CompanyId, cancellationToken);
        if (company == null)
            throw new DomainException($"Company with ID '{dto.CompanyId}' not found.");

        var financialYear = await _financialYearRepository.GetByIdAsync(dto.FinancialYearId, cancellationToken);
        if (financialYear == null)
            throw new DomainException($"Financial year with ID '{dto.FinancialYearId}' not found.");

        if (financialYear.CompanyId != dto.CompanyId)
            throw new DomainException("Financial year does not belong to the specified company.");

        if (financialYear.IsLocked)
            throw new DomainException($"Financial year '{financialYear.YearName}' is locked. Purchase invoice creation is prohibited.");

        if (!financialYear.Contains(dto.InvoiceDate))
            throw new DomainException($"Invoice date {dto.InvoiceDate:yyyy-MM-dd} is outside financial year '{financialYear.YearName}' ({financialYear.StartDate:yyyy-MM-dd} to {financialYear.EndDate:yyyy-MM-dd}).");

        var trimmedInvoiceNo = dto.InvoiceNumber.Trim();
        if (await _purchaseInvoiceRepository.ExistsByInvoiceNumberAsync(dto.CompanyId, dto.FinancialYearId, trimmedInvoiceNo, cancellationToken: cancellationToken))
            throw new DomainException($"Purchase invoice '{trimmedInvoiceNo}' already exists in financial year '{financialYear.YearName}'.");

        if (await _voucherRepository.ExistsByNumberAsync(dto.CompanyId, dto.FinancialYearId, trimmedInvoiceNo, cancellationToken: cancellationToken))
            throw new DomainException($"Voucher number '{trimmedInvoiceNo}' already exists in financial year '{financialYear.YearName}'.");

        var supplier = await _ledgerRepository.GetByIdAsync(dto.CompanyId, dto.SupplierId, cancellationToken);
        if (supplier == null)
            throw new DomainException($"Supplier ledger with ID '{dto.SupplierId}' not found in company.");

        if (supplier.CompanyId != dto.CompanyId)
            throw new DomainException("Supplier ledger does not belong to the specified company.");

        var allGroups = (await _accountGroupRepository.GetAllAsync(dto.CompanyId, cancellationToken))
            .ToDictionary(g => g.Id);

        if (!allGroups.TryGetValue(supplier.GroupId, out var supplierGroup))
        {
            throw new DomainException($"Account group with ID '{supplier.GroupId}' not found for supplier ledger '{supplier.Name}'.");
        }

        if (!IsSupplierEligibleGroup(supplierGroup, allGroups))
        {
            throw new DomainException(
                $"Selected party '{supplier.Name}' belongs to account group '{supplierGroup.Name}' and is not eligible as a purchase supplier. Supplier ledger must belong to 'Sundry Creditors' or an eligible trade payable group.");
        }

        // Resolve default Purchase ledger
        var companyLedgers = await _ledgerRepository.GetAllAsync(dto.CompanyId, cancellationToken);
        var purchaseLedger = companyLedgers.FirstOrDefault(l =>
            l.Name.Equals("Purchase", StringComparison.OrdinalIgnoreCase));
        if (purchaseLedger == null)
            throw new DomainException("Purchase ledger 'Purchase' not found. Please ensure default ledgers are seeded.");

        // Validate item lines
        var validatedLines = new List<(Item Item, Unit Unit, decimal Quantity, Money Price)>();
        foreach (var lineDto in dto.Lines)
        {
            if (lineDto.ItemId == Guid.Empty)
                throw new DomainException("ItemId cannot be empty.");

            if (lineDto.UnitId == Guid.Empty)
                throw new DomainException("UnitId cannot be empty.");

            if (lineDto.Quantity <= 0)
                throw new DomainException("Line quantity must be greater than zero.");

            if (lineDto.Price < 0)
                throw new DomainException("Line price cannot be negative.");

            var item = await _itemRepository.GetByIdAsync(dto.CompanyId, lineDto.ItemId, cancellationToken);
            if (item == null)
                throw new DomainException($"Item with ID '{lineDto.ItemId}' not found in company.");

            var unit = await _unitRepository.GetByIdAsync(dto.CompanyId, lineDto.UnitId, cancellationToken);
            if (unit == null)
                throw new DomainException($"Unit with ID '{lineDto.UnitId}' not found in company.");

            validatedLines.Add((item, unit, lineDto.Quantity, Money.FromDecimal(lineDto.Price)));
        }

        // Validate sundry lines if any
        var validatedSundries = new List<(Ledger Ledger, decimal? Pct, Money Amount)>();
        if (dto.Sundries != null)
        {
            foreach (var sundryDto in dto.Sundries)
            {
                if (sundryDto.LedgerId == Guid.Empty)
                    throw new DomainException("Sundry LedgerId cannot be empty.");

                if (sundryDto.Amount <= 0)
                    throw new DomainException("Sundry amount must be greater than zero.");

                var sundryLedger = await _ledgerRepository.GetByIdAsync(dto.CompanyId, sundryDto.LedgerId, cancellationToken);
                if (sundryLedger == null)
                    throw new DomainException($"Sundry ledger with ID '{sundryDto.LedgerId}' not found in company.");

                validatedSundries.Add((sundryLedger, sundryDto.Percentage, Money.FromDecimal(sundryDto.Amount)));
            }
        }

        var itemSubtotal = Money.FromDecimal(validatedLines.Sum(l => l.Quantity * l.Price.Amount));
        var sundrySubtotal = Money.FromDecimal(validatedSundries.Sum(s => s.Amount.Amount));
        var grandTotal = itemSubtotal + sundrySubtotal;

        if (grandTotal.Amount <= 0)
            throw new DomainException("Purchase invoice grand total must be greater than zero.");

        var series = string.IsNullOrWhiteSpace(dto.Series) ? "Main" : dto.Series.Trim();
        var matCentre = string.IsNullOrWhiteSpace(dto.MaterialCentre) ? "Main Store" : dto.MaterialCentre.Trim();
        var now = _dateTimeProvider.UtcNow;
        var user = _currentUserService.UserName;

        // Perform ATOMIC single-transaction save
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Post Financial Voucher
            var voucherLines = new List<CreateVoucherLineDto>();
            if (itemSubtotal.Amount > 0)
            {
                voucherLines.Add(new CreateVoucherLineDto(purchaseLedger.Id, DebitCreditType.Debit, itemSubtotal.Amount, null));
            }

            foreach (var s in validatedSundries)
            {
                voucherLines.Add(new CreateVoucherLineDto(s.Ledger.Id, DebitCreditType.Debit, s.Amount.Amount, null));
            }

            voucherLines.Add(new CreateVoucherLineDto(supplier.Id, DebitCreditType.Credit, grandTotal.Amount, null));

            var createVoucherDto = new CreateVoucherDto(
                dto.CompanyId,
                dto.FinancialYearId,
                trimmedInvoiceNo,
                VoucherType.Purchase,
                dto.InvoiceDate,
                trimmedInvoiceNo,
                dto.Narration,
                voucherLines);

            var voucherDto = await _postingService.PostVoucherAsync(createVoucherDto, transaction, cancellationToken);

            // 2. Bill-Wise NewRef (if supplier has BillWiseEnabled)
            if (supplier.BillWiseEnabled)
            {
                var supplierCreditLine = voucherDto.Lines.FirstOrDefault(l =>
                    l.LedgerId == supplier.Id && l.Type == DebitCreditType.Credit);
                if (supplierCreditLine == null)
                    throw new DomainException("Could not locate supplier credit line in posted voucher for bill reference.");

                var createBillRefDto = new CreateBillReferenceDto(
                    dto.CompanyId,
                    supplier.Id,
                    trimmedInvoiceNo,
                    BillReferenceType.NewRef,
                    dto.InvoiceDate,
                    grandTotal.Amount,
                    DebitCreditType.Credit,
                    DueDate: null,
                    SourceVoucherLineId: supplierCreditLine.Id);

                await _billWiseService.CreateBillReferenceAsync(createBillRefDto, transaction, cancellationToken);
            }

            // 3. PurchaseInvoice Aggregate
            TransportDetails? transport = null;
            if (dto.Transport != null)
            {
                transport = new TransportDetails(
                    dto.Transport.TransportName,
                    dto.Transport.GrRrNumber,
                    dto.Transport.TransportDate,
                    dto.Transport.VehicleNumber,
                    dto.Transport.StationFromPlace);
            }

            var invoice = new PurchaseInvoice(
                Guid.NewGuid(),
                dto.CompanyId,
                dto.FinancialYearId,
                trimmedInvoiceNo,
                dto.InvoiceDate,
                supplier.Id,
                series,
                matCentre,
                dto.Narration,
                PurchaseInvoiceStatus.Active,
                voucherDto.Id,
                transport)
            {
                CreatedAt = now,
                CreatedBy = user
            };

            foreach (var l in validatedLines)
            {
                invoice.AddItemLine(l.Item.Id, l.Quantity, l.Unit.Id, l.Price);
            }

            foreach (var s in validatedSundries)
            {
                invoice.AddSundryLine(s.Ledger.Id, s.Amount, s.Pct);
            }

            await _purchaseInvoiceRepository.AddAsync(invoice, transaction, cancellationToken);

            // 4. Stock Movements (+Quantity with acquisition price rate)
            var stockMovements = new List<StockMovement>();
            foreach (var line in invoice.Lines)
            {
                var sm = new StockMovement(
                    Guid.NewGuid(),
                    dto.CompanyId,
                    line.ItemId,
                    dto.InvoiceDate,
                    StockMovementType.PurchaseInward,
                    "PurchaseInvoice",
                    invoice.Id,
                    line.Quantity,
                    line.UnitId,
                    line.Price,
                    matCentre,
                    isActive: true)
                {
                    CreatedAt = now,
                    CreatedBy = user
                };
                stockMovements.Add(sm);
            }

            await _stockMovementRepository.AddRangeAsync(stockMovements, transaction, cancellationToken);

            // Commit atomic transaction
            await transaction.CommitAsync(cancellationToken);

            var lineDtos = invoice.Lines.Select((l, idx) => new PurchaseInvoiceItemLineDto(
                l.Id,
                l.PurchaseInvoiceId,
                l.SequenceNumber,
                l.ItemId,
                validatedLines[idx].Item.Name,
                l.Quantity,
                l.UnitId,
                validatedLines[idx].Unit.Name,
                l.Price.Amount,
                l.Amount.Amount)).ToList();

            var sundryDtos = invoice.Sundries.Select((s, idx) => new PurchaseInvoiceSundryDto(
                s.Id,
                s.PurchaseInvoiceId,
                s.SequenceNumber,
                s.LedgerId,
                validatedSundries[idx].Ledger.Name,
                s.Percentage,
                s.Amount.Amount)).ToList();

            return MapToDto(invoice, voucherDto.VoucherNumber, supplier.Name, lineDtos, sundryDtos);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PurchaseInvoiceDto?> GetByIdAsync(Guid companyId, Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _purchaseInvoiceRepository.GetByIdAsync(companyId, id, cancellationToken);
        if (invoice == null)
            return null;

        var supplier = await _ledgerRepository.GetByIdAsync(companyId, invoice.SupplierId, cancellationToken);
        var supplierName = supplier?.Name ?? "Unknown Supplier";

        string? voucherNumber = null;
        if (invoice.VoucherId.HasValue)
        {
            var voucher = await _voucherRepository.GetByIdAsync(companyId, invoice.VoucherId.Value, cancellationToken);
            voucherNumber = voucher?.VoucherNumber;
        }

        var lineDtos = new List<PurchaseInvoiceItemLineDto>();
        foreach (var l in invoice.Lines)
        {
            var item = await _itemRepository.GetByIdAsync(companyId, l.ItemId, cancellationToken);
            var unit = await _unitRepository.GetByIdAsync(companyId, l.UnitId, cancellationToken);
            lineDtos.Add(new PurchaseInvoiceItemLineDto(
                l.Id,
                l.PurchaseInvoiceId,
                l.SequenceNumber,
                l.ItemId,
                item?.Name ?? "Unknown Item",
                l.Quantity,
                l.UnitId,
                unit?.Name ?? "Unit",
                l.Price.Amount,
                l.Amount.Amount));
        }

        var sundryDtos = new List<PurchaseInvoiceSundryDto>();
        foreach (var s in invoice.Sundries)
        {
            var ledger = await _ledgerRepository.GetByIdAsync(companyId, s.LedgerId, cancellationToken);
            sundryDtos.Add(new PurchaseInvoiceSundryDto(
                s.Id,
                s.PurchaseInvoiceId,
                s.SequenceNumber,
                s.LedgerId,
                ledger?.Name ?? "Unknown Sundry",
                s.Percentage,
                s.Amount.Amount));
        }

        return MapToDto(invoice, voucherNumber, supplierName, lineDtos, sundryDtos);
    }

    public async Task<IReadOnlyList<PurchaseInvoiceSummaryDto>> GetSummariesAsync(
        Guid companyId,
        Guid? financialYearId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var invoices = await _purchaseInvoiceRepository.GetAllAsync(
            companyId, financialYearId, fromDate, toDate, supplierId, cancellationToken);

        var ledgers = (await _ledgerRepository.GetAllAsync(companyId, cancellationToken))
            .ToDictionary(l => l.Id, l => l.Name);

        var summaries = new List<PurchaseInvoiceSummaryDto>();
        foreach (var inv in invoices)
        {
            var suppName = ledgers.TryGetValue(inv.SupplierId, out var name) ? name : "Unknown Supplier";
            summaries.Add(new PurchaseInvoiceSummaryDto(
                inv.Id,
                inv.Series,
                inv.InvoiceNumber,
                inv.InvoiceDate,
                inv.SupplierId,
                suppName,
                inv.GrandTotal.Amount,
                inv.Status,
                inv.VoucherId,
                null));
        }

        return summaries;
    }

    public async Task<decimal> GetLiveStockQuantityAsync(
        Guid companyId,
        Guid itemId,
        string materialCentre = "Main Store",
        CancellationToken cancellationToken = default)
    {
        return await _stockMovementRepository.GetCurrentQuantityAsync(companyId, itemId, materialCentre, cancellationToken);
    }

    private static PurchaseInvoiceDto MapToDto(
        PurchaseInvoice invoice,
        string? voucherNumber,
        string supplierName,
        IReadOnlyList<PurchaseInvoiceItemLineDto> lineDtos,
        IReadOnlyList<PurchaseInvoiceSundryDto> sundryDtos)
    {
        TransportDetailsDto? transportDto = null;
        if (invoice.Transport != null)
        {
            transportDto = new TransportDetailsDto(
                invoice.Transport.TransportName,
                invoice.Transport.GrRrNumber,
                invoice.Transport.TransportDate,
                invoice.Transport.VehicleNumber,
                invoice.Transport.StationFromPlace);
        }

        return new PurchaseInvoiceDto(
            invoice.Id,
            invoice.CompanyId,
            invoice.FinancialYearId,
            invoice.VoucherId,
            voucherNumber,
            invoice.Series,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.SupplierId,
            supplierName,
            invoice.MaterialCentre,
            invoice.Narration,
            invoice.Status,
            invoice.ItemSubtotal.Amount,
            invoice.SundrySubtotal.Amount,
            invoice.GrandTotal.Amount,
            transportDto,
            lineDtos,
            sundryDtos,
            invoice.CreatedAt,
            invoice.CreatedBy,
            invoice.ModifiedAt,
            invoice.ModifiedBy);
    }

    private static bool IsSupplierEligibleGroup(AccountGroup group, IReadOnlyDictionary<Guid, AccountGroup> allGroups)
    {
        var current = group;
        var visited = new HashSet<Guid>();
        while (current != null && visited.Add(current.Id))
        {
            if (string.Equals(current.Name, "Sundry Creditors", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.ParentGroupId.HasValue && allGroups.TryGetValue(current.ParentGroupId.Value, out var parent))
            {
                current = parent;
            }
            else
            {
                break;
            }
        }
        return false;
    }
}
