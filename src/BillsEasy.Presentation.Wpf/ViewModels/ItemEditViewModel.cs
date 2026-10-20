using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class ItemEditViewModel : ViewModelBase
{
    private readonly IItemService _itemService;
    private readonly IItemGroupService _itemGroupService;
    private readonly IUnitService _unitService;
    private readonly Guid _companyId;
    private readonly ItemDto? _existingItem;

    [ObservableProperty]
    private string _dialogTitle = "Add Item Master";

    // 1. Basic Information
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _printName = string.Empty;

    [ObservableProperty]
    private string? _code;

    [ObservableProperty]
    private bool _isActive = true;

    // 2. Group / Unit
    [ObservableProperty]
    private ItemGroupLookupDto? _selectedGroup;

    [ObservableProperty]
    private ObservableCollection<ItemGroupLookupDto> _groupOptions = new();

    [ObservableProperty]
    private UnitLookupDto? _selectedUnit;

    [ObservableProperty]
    private ObservableCollection<UnitLookupDto> _unitOptions = new();

    [ObservableProperty]
    private UnitLookupDto? _selectedAlternateUnit;

    [ObservableProperty]
    private ObservableCollection<UnitLookupDto> _alternateUnitOptions = new();

    [ObservableProperty]
    private decimal? _conversionFactor;

    // 3. Opening Stock
    [ObservableProperty]
    private decimal _openingStockQuantity;

    [ObservableProperty]
    private decimal _openingStockRate;

    [ObservableProperty]
    private decimal _openingStockValue;

    // 4. Pricing
    [ObservableProperty]
    private decimal? _salesPrice;

    [ObservableProperty]
    private decimal? _purchasePrice;

    [ObservableProperty]
    private decimal? _mrp;

    [ObservableProperty]
    private decimal? _minSalePrice;

    [ObservableProperty]
    private decimal? _discountPercent;

    // 5. Inventory Controls
    [ObservableProperty]
    private decimal? _minLevel;

    [ObservableProperty]
    private decimal? _maxLevel;

    [ObservableProperty]
    private decimal? _reorderLevel;

    // 6. Description / Barcode
    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string? _barcode;

    [ObservableProperty]
    private string? _errorMessage;

    public ItemDto? SavedItem { get; private set; }
    public bool IsSaved { get; private set; }

    public event Action? RequestClose;

    public ItemEditViewModel(
        IItemService itemService,
        IItemGroupService itemGroupService,
        IUnitService unitService,
        Guid companyId,
        ItemDto? existingItem = null)
    {
        _itemService = itemService;
        _itemGroupService = itemGroupService;
        _unitService = unitService;
        _companyId = companyId;
        _existingItem = existingItem;

        if (_existingItem != null)
        {
            DialogTitle = $"Modify Item — {_existingItem.Name}";
            Name = _existingItem.Name;
            PrintName = _existingItem.PrintName;
            Code = _existingItem.Code;
            IsActive = _existingItem.IsActive;

            ConversionFactor = _existingItem.ConversionFactor;

            OpeningStockQuantity = _existingItem.OpeningStockQuantity;
            OpeningStockRate = _existingItem.OpeningStockRate.Amount;
            OpeningStockValue = _existingItem.OpeningStockValue.Amount;

            SalesPrice = _existingItem.SalesPrice?.Amount;
            PurchasePrice = _existingItem.PurchasePrice?.Amount;
            Mrp = _existingItem.MRP?.Amount;
            MinSalePrice = _existingItem.MinSalePrice?.Amount;
            DiscountPercent = _existingItem.DiscountPercent;

            MinLevel = _existingItem.MinLevel;
            MaxLevel = _existingItem.MaxLevel;
            ReorderLevel = _existingItem.ReorderLevel;

            Description = _existingItem.Description;
            Barcode = _existingItem.Barcode;
        }
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        try
        {
            var groups = await _itemGroupService.GetLookupGroupsAsync(_companyId);
            GroupOptions = new ObservableCollection<ItemGroupLookupDto>(groups);

            var units = await _unitService.GetLookupUnitsAsync(_companyId);
            UnitOptions = new ObservableCollection<UnitLookupDto>(units);
            AlternateUnitOptions = new ObservableCollection<UnitLookupDto>(units);

            if (_existingItem != null)
            {
                SelectedGroup = GroupOptions.FirstOrDefault(g => g.Id == _existingItem.ItemGroupId);
                SelectedUnit = UnitOptions.FirstOrDefault(u => u.Id == _existingItem.UnitId);
                if (_existingItem.AlternateUnitId.HasValue)
                {
                    SelectedAlternateUnit = AlternateUnitOptions.FirstOrDefault(u => u.Id == _existingItem.AlternateUnitId.Value);
                }
            }
            else
            {
                SelectedGroup = GroupOptions.FirstOrDefault();
                SelectedUnit = UnitOptions.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load lookup data: {ex.Message}";
        }
    }

    partial void OnOpeningStockQuantityChanged(decimal value)
    {
        RecalculateOpeningStockValueIfZero();
    }

    partial void OnOpeningStockRateChanged(decimal value)
    {
        RecalculateOpeningStockValueIfZero();
    }

    private void RecalculateOpeningStockValueIfZero()
    {
        if (OpeningStockValue == 0 && OpeningStockQuantity > 0 && OpeningStockRate > 0)
        {
            OpeningStockValue = OpeningStockQuantity * OpeningStockRate;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Item name is required.";
            return;
        }

        if (SelectedGroup == null)
        {
            ErrorMessage = "Item group selection is required.";
            return;
        }

        if (SelectedUnit == null)
        {
            ErrorMessage = "Main unit selection is required.";
            return;
        }

        if (SelectedAlternateUnit != null)
        {
            if (SelectedAlternateUnit.Id == SelectedUnit.Id)
            {
                ErrorMessage = "Alternate unit cannot be the same as main unit.";
                return;
            }

            if (!ConversionFactor.HasValue || ConversionFactor.Value <= 0)
            {
                ErrorMessage = "Conversion factor must be greater than zero when an alternate unit is selected.";
                return;
            }
        }

        try
        {
            if (_existingItem == null)
            {
                var dto = new CreateItemDto(
                    _companyId,
                    SelectedGroup.Id,
                    SelectedUnit.Id,
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(PrintName) ? Name.Trim() : PrintName.Trim(),
                    Code?.Trim(),
                    SelectedAlternateUnit?.Id,
                    ConversionFactor,
                    OpeningStockQuantity,
                    OpeningStockRate,
                    OpeningStockValue,
                    SalesPrice,
                    PurchasePrice,
                    Mrp,
                    MinSalePrice,
                    DiscountPercent,
                    MinLevel,
                    MaxLevel,
                    ReorderLevel,
                    Description?.Trim(),
                    Barcode?.Trim(),
                    IsActive);

                SavedItem = await _itemService.CreateItemAsync(dto);
            }
            else
            {
                var dto = new UpdateItemDto(
                    _existingItem.Id,
                    _companyId,
                    SelectedGroup.Id,
                    SelectedUnit.Id,
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(PrintName) ? Name.Trim() : PrintName.Trim(),
                    Code?.Trim(),
                    SelectedAlternateUnit?.Id,
                    ConversionFactor,
                    OpeningStockQuantity,
                    OpeningStockRate,
                    OpeningStockValue,
                    SalesPrice,
                    PurchasePrice,
                    Mrp,
                    MinSalePrice,
                    DiscountPercent,
                    MinLevel,
                    MaxLevel,
                    ReorderLevel,
                    Description?.Trim(),
                    Barcode?.Trim(),
                    IsActive);

                SavedItem = await _itemService.UpdateItemAsync(dto);
            }

            IsSaved = true;
            RequestClose?.Invoke();
        }
        catch (DomainException dex)
        {
            ErrorMessage = dex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error saving item: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        IsSaved = false;
        RequestClose?.Invoke();
    }
}
