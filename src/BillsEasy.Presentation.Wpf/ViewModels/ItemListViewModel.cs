using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Presentation.Wpf.Views.Inventory;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class ItemListViewModel : ViewModelBase
{
    private readonly IItemService _itemService;
    private readonly IItemGroupService _itemGroupService;
    private readonly IUnitService _unitService;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private ObservableCollection<ItemListDto> _items = new();

    [ObservableProperty]
    private ObservableCollection<ItemListDto> _filteredItems = new();

    [ObservableProperty]
    private ItemListDto? _selectedItem;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public string ActiveCompanyName => _contextService.ActiveCompany?.CompanyName ?? "No Company";

    public event Action? RequestClose;

    public ItemListViewModel(
        IItemService itemService,
        IItemGroupService itemGroupService,
        IUnitService unitService,
        ICompanyContextService contextService)
    {
        _itemService = itemService;
        _itemGroupService = itemGroupService;
        _unitService = unitService;
        _contextService = contextService;
    }

    [RelayCommand]
    public async Task LoadItemsAsync()
    {
        if (_contextService.ActiveCompany == null)
            return;

        try
        {
            ErrorMessage = null;
            var list = await _itemService.GetAllItemsAsync(_contextService.ActiveCompany.Id);
            Items = new ObservableCollection<ItemListDto>(list);
            ApplyFilter();
            StatusMessage = $"Loaded {Items.Count} items.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load items: {ex.Message}";
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredItems = new ObservableCollection<ItemListDto>(Items);
        }
        else
        {
            var term = SearchText.Trim();
            FilteredItems = new ObservableCollection<ItemListDto>(
                Items.Where(i => i.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                              || (i.Code != null && i.Code.Contains(term, StringComparison.OrdinalIgnoreCase))
                              || i.ItemGroupName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [RelayCommand]
    public async Task AddItemAsync(Window? owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var editVm = new ItemEditViewModel(_itemService, _itemGroupService, _unitService, _contextService.ActiveCompany.Id);
        await editVm.InitializeAsync();

        var dialog = new ItemEditDialog(editVm)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true && editVm.SavedItem != null)
        {
            await LoadItemsAsync();
            StatusMessage = $"Created item '{editVm.SavedItem.Name}'.";
        }
    }

    [RelayCommand]
    public async Task ModifyItemAsync(Window? owner)
    {
        if (_contextService.ActiveCompany == null || SelectedItem == null) return;

        var fullItem = await _itemService.GetItemByIdAsync(_contextService.ActiveCompany.Id, SelectedItem.Id);
        if (fullItem == null) return;

        var editVm = new ItemEditViewModel(_itemService, _itemGroupService, _unitService, _contextService.ActiveCompany.Id, fullItem);
        await editVm.InitializeAsync();

        var dialog = new ItemEditDialog(editVm)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true && editVm.SavedItem != null)
        {
            await LoadItemsAsync();
            StatusMessage = $"Updated item '{editVm.SavedItem.Name}'.";
        }
    }

    [RelayCommand]
    public async Task DeleteItemAsync()
    {
        if (_contextService.ActiveCompany == null || SelectedItem == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete item '{SelectedItem.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            ErrorMessage = null;
            await _itemService.DeleteItemAsync(_contextService.ActiveCompany.Id, SelectedItem.Id);
            StatusMessage = $"Deleted item '{SelectedItem.Name}'.";
            Items.Remove(SelectedItem);
            ApplyFilter();
            SelectedItem = null;
        }
        catch (DomainException dex)
        {
            ErrorMessage = dex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete item: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}
