using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Entities;
using BillsEasy.Domain.Enums;
using BillsEasy.Presentation.Wpf.Views.Masters;
using BillsEasy.Presentation.Wpf.Views.Purchase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class ItemLineRowViewModel : ObservableObject
{
    private readonly Action _onTotalsChanged;
    private readonly Func<IEnumerable<ItemDto>>? _getAvailableItems;

    [ObservableProperty]
    private int _sequenceNumber;

    [ObservableProperty]
    private ItemDto? _selectedItem;

    [ObservableProperty]
    private string? _itemSearchText;

    [ObservableProperty]
    private string _unitName = "NOS";

    [ObservableProperty]
    private Guid _unitId;

    [ObservableProperty]
    private decimal _quantity = 1;

    [ObservableProperty]
    private decimal _price = 0;

    [ObservableProperty]
    private decimal _amount = 0;

    public ItemLineRowViewModel(int sequenceNumber, Action onTotalsChanged, Func<IEnumerable<ItemDto>>? getAvailableItems = null)
    {
        _sequenceNumber = sequenceNumber;
        _onTotalsChanged = onTotalsChanged;
        _getAvailableItems = getAvailableItems;
    }

    partial void OnSelectedItemChanged(ItemDto? value)
    {
        if (value != null)
        {
            if (ItemSearchText != value.Name)
            {
                ItemSearchText = value.Name;
            }
            UnitName = value.UnitName;
            UnitId = value.UnitId;
            // Note: Per Class 8, price is explicitly entered by user.
            Recalculate();
        }
        else
        {
            if (ItemSearchText != null)
            {
                ItemSearchText = null;
            }
            UnitName = "NOS";
            UnitId = Guid.Empty;
            Recalculate();
        }
    }

    partial void OnItemSearchTextChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (SelectedItem != null)
            {
                SelectedItem = null;
            }
            return;
        }

        if (SelectedItem != null && string.Equals(SelectedItem.Name, value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var available = _getAvailableItems?.Invoke();
        if (available != null)
        {
            var match = available.FirstOrDefault(i =>
                string.Equals(i.Name, value, StringComparison.OrdinalIgnoreCase) ||
                i.Name.StartsWith(value, StringComparison.OrdinalIgnoreCase));

            if (match != null && match != SelectedItem)
            {
                SelectedItem = match;
            }
        }
    }

    partial void OnQuantityChanged(decimal value) => Recalculate();
    partial void OnPriceChanged(decimal value) => Recalculate();

    private void Recalculate()
    {
        Amount = Math.Round(Quantity * Price, 2);
        _onTotalsChanged();
    }
}

public partial class SundryLineRowViewModel : ObservableObject
{
    private readonly Action _onTotalsChanged;
    private readonly Func<IEnumerable<LedgerLookupDto>>? _getAvailableLedgers;

    [ObservableProperty]
    private int _sequenceNumber;

    [ObservableProperty]
    private LedgerLookupDto? _selectedLedger;

    [ObservableProperty]
    private string? _ledgerSearchText;

    [ObservableProperty]
    private decimal? _percentage;

    [ObservableProperty]
    private decimal _amount = 0;

    public SundryLineRowViewModel(int sequenceNumber, Action onTotalsChanged, Func<IEnumerable<LedgerLookupDto>>? getAvailableLedgers = null)
    {
        _sequenceNumber = sequenceNumber;
        _onTotalsChanged = onTotalsChanged;
        _getAvailableLedgers = getAvailableLedgers;
    }

    partial void OnSelectedLedgerChanged(LedgerLookupDto? value)
    {
        if (value != null && LedgerSearchText != value.Name)
        {
            LedgerSearchText = value.Name;
        }
        _onTotalsChanged();
    }

    partial void OnLedgerSearchTextChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (SelectedLedger != null)
            {
                SelectedLedger = null;
            }
            return;
        }

        if (SelectedLedger != null && string.Equals(SelectedLedger.Name, value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var available = _getAvailableLedgers?.Invoke();
        if (available != null)
        {
            var match = available.FirstOrDefault(l =>
                string.Equals(l.Name, value, StringComparison.OrdinalIgnoreCase) ||
                l.Name.StartsWith(value, StringComparison.OrdinalIgnoreCase));

            if (match != null && match != SelectedLedger)
            {
                SelectedLedger = match;
            }
        }
    }

    partial void OnAmountChanged(decimal value) => _onTotalsChanged();
}

public partial class PurchaseInvoiceEditViewModel : ViewModelBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ILedgerService _ledgerService;
    private readonly IAccountGroupService _accountGroupService;
    private readonly IItemService _itemService;
    private readonly IUnitService _unitService;
    private readonly IItemRepository _itemRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private string _dialogTitle = "Purchase Invoice (BUSY Class 8)";

    [ObservableProperty]
    private string _series = "Main";

    [ObservableProperty]
    private DateTime _invoiceDate = DateTime.Today;

    [ObservableProperty]
    private string _invoiceNumber = string.Empty;

    [ObservableProperty]
    private string _materialCentre = "Main Store";

    [ObservableProperty]
    private ObservableCollection<LedgerDto> _supplierOptions = new();

    [ObservableProperty]
    private LedgerDto? _selectedSupplier;

    [ObservableProperty]
    private string _supplierLiveBalanceText = "Balance: ₹0.00";

    [ObservableProperty]
    private string? _narration;

    [ObservableProperty]
    private ObservableCollection<ItemDto> _availableItems = new();

    [ObservableProperty]
    private ObservableCollection<LedgerLookupDto> _availableSundryLedgers = new();

    [ObservableProperty]
    private ObservableCollection<ItemLineRowViewModel> _itemLines = new();

    [ObservableProperty]
    private ItemLineRowViewModel? _selectedLine;

    [ObservableProperty]
    private ObservableCollection<SundryLineRowViewModel> _sundryLines = new();

    [ObservableProperty]
    private SundryLineRowViewModel? _selectedSundry;

    [ObservableProperty]
    private decimal _itemSubtotal = 0;

    [ObservableProperty]
    private decimal _sundrySubtotal = 0;

    [ObservableProperty]
    private decimal _grandTotal = 0;

    [ObservableProperty]
    private string? _errorMessage;

    public PurchaseInvoiceDto? SavedInvoice { get; private set; }

    public PurchaseInvoiceEditViewModel(
        IPurchaseService purchaseService,
        ILedgerService ledgerService,
        IAccountGroupService accountGroupService,
        IItemService itemService,
        IUnitService unitService,
        IItemRepository itemRepository,
        IVoucherRepository voucherRepository,
        ICompanyContextService contextService)
    {
        _purchaseService = purchaseService;
        _ledgerService = ledgerService;
        _accountGroupService = accountGroupService;
        _itemService = itemService;
        _unitService = unitService;
        _itemRepository = itemRepository;
        _voucherRepository = voucherRepository;
        _contextService = contextService;

        // Auto-add first line by default
        AddItemLine();
    }

    public async Task InitializeAsync()
    {
        if (_contextService.ActiveCompany == null)
            return;

        var companyId = _contextService.ActiveCompany.Id;

        // Constrain invoice date to active FY if available
        if (_contextService.ActiveFinancialYear != null)
        {
            var fyStart = _contextService.ActiveFinancialYear.StartDate.ToDateTime(TimeOnly.MinValue);
            var fyEnd = _contextService.ActiveFinancialYear.EndDate.ToDateTime(TimeOnly.MaxValue);
            if (DateTime.Today >= fyStart && DateTime.Today <= fyEnd)
            {
                InvoiceDate = DateTime.Today;
            }
            else
            {
                InvoiceDate = fyStart;
            }
        }

        await LoadSuppliersAsync(companyId);
        await LoadItemsAsync(companyId);
        await LoadSundryLedgersAsync(companyId);
    }

    private async Task LoadSuppliersAsync(Guid companyId)
    {
        var allLedgers = await _ledgerService.GetAllLedgersAsync(companyId);
        var groupsList = await _accountGroupService.GetAllGroupsAsync(companyId);
        var allGroups = (groupsList ?? Array.Empty<AccountGroupDto>()).ToDictionary(g => g.Id);

        var eligibleGroupIds = new HashSet<Guid>();
        foreach (var group in allGroups.Values)
        {
            if (IsSupplierEligibleGroup(group, allGroups))
            {
                eligibleGroupIds.Add(group.Id);
            }
        }

        SupplierOptions.Clear();
        foreach (var ledger in allLedgers ?? Array.Empty<LedgerDto>())
        {
            if (eligibleGroupIds.Contains(ledger.GroupId))
            {
                SupplierOptions.Add(ledger);
            }
        }
    }

    private static bool IsSupplierEligibleGroup(AccountGroupDto group, IReadOnlyDictionary<Guid, AccountGroupDto> allGroups)
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

    private async Task LoadItemsAsync(Guid companyId)
    {
        var items = await _itemRepository.GetAllAsync(companyId);
        var units = (await _unitService.GetAllUnitsAsync(companyId)).ToDictionary(u => u.Id, u => u.Name);

        AvailableItems.Clear();
        foreach (var item in items)
        {
            var unitName = units.TryGetValue(item.UnitId, out var uName) ? uName : "NOS";
            AvailableItems.Add(new ItemDto(
                item.Id,
                item.CompanyId,
                item.ItemGroupId,
                string.Empty,
                item.UnitId,
                unitName,
                null,
                null,
                null,
                item.Name,
                item.PrintName,
                item.Code,
                item.OpeningStockQuantity,
                item.OpeningStockRate,
                item.OpeningStockValue,
                item.SalesPrice,
                item.PurchasePrice,
                item.MRP,
                item.MinSalePrice,
                item.DiscountPercent,
                item.MinLevel,
                item.MaxLevel,
                item.ReorderLevel,
                item.Description,
                item.Barcode,
                item.IsActive,
                item.CreatedAt,
                item.CreatedBy,
                item.ModifiedAt,
                item.ModifiedBy));
        }
    }

    private async Task LoadSundryLedgersAsync(Guid companyId)
    {
        var lookups = await _ledgerService.GetLookupLedgersAsync(companyId);
        AvailableSundryLedgers.Clear();
        foreach (var l in lookups)
        {
            AvailableSundryLedgers.Add(l);
        }
    }

    partial void OnSelectedSupplierChanged(LedgerDto? value)
    {
        _ = UpdateSupplierLiveBalanceAsync(value);
    }

    private async Task UpdateSupplierLiveBalanceAsync(LedgerDto? supplier)
    {
        if (supplier == null || _contextService.ActiveCompany == null)
        {
            SupplierLiveBalanceText = "Balance: ₹0.00";
            return;
        }

        try
        {
            var companyId = _contextService.ActiveCompany.Id;
            var vouchers = await _voucherRepository.GetAllAsync(companyId, ledgerId: supplier.Id);

            decimal balance = supplier.OpeningBalance;
            if (supplier.OpeningBalanceType == DebitCreditType.Credit)
            {
                balance = -balance; // Negative represents Cr in standard algebraic sign convention
            }

            foreach (var v in vouchers)
            {
                foreach (var line in v.Lines.Where(l => l.LedgerId == supplier.Id))
                {
                    if (line.Type == DebitCreditType.Debit)
                        balance += line.Amount.Amount;
                    else
                        balance -= line.Amount.Amount;
                }
            }

            if (balance < 0)
                SupplierLiveBalanceText = $"Balance: ₹{Math.Abs(balance):N2} Cr";
            else if (balance > 0)
                SupplierLiveBalanceText = $"Balance: ₹{balance:N2} Dr";
            else
                SupplierLiveBalanceText = "Balance: ₹0.00";
        }
        catch
        {
            SupplierLiveBalanceText = "Balance: ₹0.00";
        }
    }

    [RelayCommand]
    public void AddItemLine()
    {
        var line = new ItemLineRowViewModel(ItemLines.Count + 1, RecalculateTotals, () => AvailableItems);
        ItemLines.Add(line);
        SelectedLine = line;
        RecalculateTotals();
    }

    [RelayCommand]
    public void RemoveItemLine()
    {
        if (SelectedLine != null)
        {
            ItemLines.Remove(SelectedLine);
            ReSequenceItemLines();
            RecalculateTotals();
        }
    }

    [RelayCommand]
    public void AddSundryLine()
    {
        var sundry = new SundryLineRowViewModel(SundryLines.Count + 1, RecalculateTotals, () => AvailableSundryLedgers);
        // Default to Freight & Forwarding Charges if present
        var freight = AvailableSundryLedgers.FirstOrDefault(s =>
            s.Name.Contains("Freight", StringComparison.OrdinalIgnoreCase));
        if (freight != null)
            sundry.SelectedLedger = freight;

        SundryLines.Add(sundry);
        SelectedSundry = sundry;
        RecalculateTotals();
    }

    [RelayCommand]
    public void RemoveSundryLine()
    {
        if (SelectedSundry != null)
        {
            SundryLines.Remove(SelectedSundry);
            ReSequenceSundryLines();
            RecalculateTotals();
        }
    }

    private void ReSequenceItemLines()
    {
        for (int i = 0; i < ItemLines.Count; i++)
        {
            ItemLines[i].SequenceNumber = i + 1;
        }
    }

    private void ReSequenceSundryLines()
    {
        for (int i = 0; i < SundryLines.Count; i++)
        {
            SundryLines[i].SequenceNumber = i + 1;
        }
    }

    private void RecalculateTotals()
    {
        ItemSubtotal = ItemLines.Sum(l => l.Amount);
        SundrySubtotal = SundryLines.Sum(s => s.Amount);
        GrandTotal = ItemSubtotal + SundrySubtotal;
    }

    [RelayCommand]
    public async Task QuickCreateSupplierAsync(Window? owner)
    {
        if (_contextService.ActiveCompany == null)
            return;

        var editVm = new LedgerEditViewModel(_ledgerService, _accountGroupService, _contextService.ActiveCompany.Id);
        var dialog = new LedgerEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.SavedLedger != null)
        {
            await LoadSuppliersAsync(_contextService.ActiveCompany.Id);
            SelectedSupplier = SupplierOptions.FirstOrDefault(s => s.Id == editVm.SavedLedger.Id);
        }
    }

    [RelayCommand]
    public async Task SaveAsync(Window? window)
    {
        ErrorMessage = null;

        if (_contextService.ActiveCompany == null || _contextService.ActiveFinancialYear == null)
        {
            ErrorMessage = "No active company or financial year selected.";
            return;
        }

        if (string.IsNullOrWhiteSpace(InvoiceNumber))
        {
            ErrorMessage = "Invoice Number is required.";
            return;
        }

        if (SelectedSupplier == null)
        {
            ErrorMessage = "Party / Supplier is required.";
            return;
        }

        var validItemLines = ItemLines.Where(l => l.SelectedItem != null && l.Quantity > 0).ToList();
        if (validItemLines.Count == 0)
        {
            ErrorMessage = "Please add at least one valid item line with quantity > 0.";
            return;
        }

        // BUSY Class 8 Prompt: Transport Details Dialog on F2 Save
        var transportVm = new TransportDetailsViewModel();
        var transportDialog = new TransportDetailsDialog(transportVm) { Owner = window };
        var transportResult = transportDialog.ShowDialog();

        if (transportResult != true)
        {
            // User cancelled transport prompt -> abort invoice save
            return;
        }

        try
        {
            var lineDtos = validItemLines.Select(l => new CreatePurchaseInvoiceItemLineDto(
                l.SelectedItem!.Id,
                l.Quantity,
                l.UnitId,
                l.Price)).ToList();

            var sundryDtos = SundryLines
                .Where(s => s.SelectedLedger != null && s.Amount > 0)
                .Select(s => new CreatePurchaseInvoiceSundryDto(
                    s.SelectedLedger!.Id,
                    s.Percentage,
                    s.Amount)).ToList();

            var dto = new CreatePurchaseInvoiceDto(
                _contextService.ActiveCompany.Id,
                _contextService.ActiveFinancialYear.Id,
                InvoiceNumber.Trim(),
                DateOnly.FromDateTime(InvoiceDate),
                SelectedSupplier.Id,
                Series: Series.Trim(),
                MaterialCentre: MaterialCentre.Trim(),
                Narration: string.IsNullOrWhiteSpace(Narration) ? null : Narration.Trim(),
                Lines: lineDtos,
                Sundries: sundryDtos,
                Transport: transportVm.Details);

            SavedInvoice = await _purchaseService.CreatePurchaseInvoiceAsync(dto);

            if (window != null)
                window.DialogResult = true;
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save purchase invoice: {ex.Message}";
        }
    }
}
