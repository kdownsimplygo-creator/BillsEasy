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

public partial class ItemGroupListViewModel : ViewModelBase
{
    private readonly IItemGroupService _itemGroupService;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private ObservableCollection<ItemGroupDto> _groups = new();

    [ObservableProperty]
    private ObservableCollection<ItemGroupDto> _filteredGroups = new();

    [ObservableProperty]
    private ItemGroupDto? _selectedGroup;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public string ActiveCompanyName => _contextService.ActiveCompany?.CompanyName ?? "No Company";

    public event Action? RequestClose;

    public ItemGroupListViewModel(
        IItemGroupService itemGroupService,
        ICompanyContextService contextService)
    {
        _itemGroupService = itemGroupService;
        _contextService = contextService;
    }

    [RelayCommand]
    public async Task LoadGroupsAsync()
    {
        if (_contextService.ActiveCompany == null)
            return;

        try
        {
            ErrorMessage = null;
            var list = await _itemGroupService.GetAllGroupsAsync(_contextService.ActiveCompany.Id);
            Groups = new ObservableCollection<ItemGroupDto>(list);
            ApplyFilter();
            StatusMessage = $"Loaded {Groups.Count} item groups.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load item groups: {ex.Message}";
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
            FilteredGroups = new ObservableCollection<ItemGroupDto>(Groups);
        }
        else
        {
            var term = SearchText.Trim();
            FilteredGroups = new ObservableCollection<ItemGroupDto>(
                Groups.Where(g => g.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                               || (g.ParentGroupName != null && g.ParentGroupName.Contains(term, StringComparison.OrdinalIgnoreCase))));
        }
    }

    [RelayCommand]
    public void AddGroup(Window? owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var editVm = new ItemGroupEditViewModel(_itemGroupService, _contextService.ActiveCompany.Id);
        var dialog = new ItemGroupEditDialog(editVm)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true && editVm.SavedGroup != null)
        {
            Groups.Add(editVm.SavedGroup);
            ApplyFilter();
            SelectedGroup = editVm.SavedGroup;
            StatusMessage = $"Created item group '{editVm.SavedGroup.Name}'.";
        }
    }

    [RelayCommand]
    public void ModifyGroup(Window? owner)
    {
        if (_contextService.ActiveCompany == null || SelectedGroup == null) return;

        var editVm = new ItemGroupEditViewModel(_itemGroupService, _contextService.ActiveCompany.Id, SelectedGroup);
        var dialog = new ItemGroupEditDialog(editVm)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true && editVm.SavedGroup != null)
        {
            var index = Groups.IndexOf(SelectedGroup);
            if (index >= 0)
            {
                Groups[index] = editVm.SavedGroup;
            }
            ApplyFilter();
            SelectedGroup = editVm.SavedGroup;
            StatusMessage = $"Updated item group '{editVm.SavedGroup.Name}'.";
        }
    }

    [RelayCommand]
    public async Task DeleteGroupAsync()
    {
        if (_contextService.ActiveCompany == null || SelectedGroup == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete item group '{SelectedGroup.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            ErrorMessage = null;
            await _itemGroupService.DeleteGroupAsync(_contextService.ActiveCompany.Id, SelectedGroup.Id);
            StatusMessage = $"Deleted item group '{SelectedGroup.Name}'.";
            Groups.Remove(SelectedGroup);
            ApplyFilter();
            SelectedGroup = null;
        }
        catch (DomainException dex)
        {
            ErrorMessage = dex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete item group: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}
