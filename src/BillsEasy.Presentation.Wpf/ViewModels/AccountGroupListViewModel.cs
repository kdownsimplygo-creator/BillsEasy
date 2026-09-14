using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Presentation.Wpf.Views.Masters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class AccountGroupListViewModel : ViewModelBase
{
    private readonly IAccountGroupService _accountGroupService;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private ObservableCollection<AccountGroupDto> _groups = new();

    [ObservableProperty]
    private ObservableCollection<AccountGroupDto> _filteredGroups = new();

    [ObservableProperty]
    private ObservableCollection<AccountGroupNodeDto> _groupTree = new();

    [ObservableProperty]
    private AccountGroupDto? _selectedGroup;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isTreeView = false;

    public string ActiveCompanyName => _contextService.ActiveCompany?.CompanyName ?? "No Company";

    public event Action? RequestClose;

    public AccountGroupListViewModel(
        IAccountGroupService accountGroupService,
        ICompanyContextService contextService)
    {
        _accountGroupService = accountGroupService;
        _contextService = contextService;
    }

    [RelayCommand]
    public async Task LoadGroupsAsync()
    {
        if (_contextService.ActiveCompany == null)
        {
            ErrorMessage = "No active company selected.";
            return;
        }

        try
        {
            ErrorMessage = null;
            var companyId = _contextService.ActiveCompany.Id;
            var list = await _accountGroupService.GetAllGroupsAsync(companyId);
            var tree = await _accountGroupService.GetGroupTreeAsync(companyId);

            Groups = new ObservableCollection<AccountGroupDto>(list);
            GroupTree = new ObservableCollection<AccountGroupNodeDto>(tree);

            ApplyFilter();

            if (FilteredGroups.Count > 0 && SelectedGroup == null)
            {
                SelectedGroup = FilteredGroups[0];
            }

            StatusMessage = $"Total Groups: {Groups.Count} ({Groups.Count(g => g.IsDefaultSystemGroup)} System, {Groups.Count(g => !g.IsDefaultSystemGroup)} Custom)";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading account groups: {ex.Message}";
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
            FilteredGroups = new ObservableCollection<AccountGroupDto>(Groups);
        }
        else
        {
            var query = SearchText.Trim().ToLowerInvariant();
            var filtered = Groups.Where(g =>
                g.Name.ToLowerInvariant().Contains(query) ||
                g.NatureName.ToLowerInvariant().Contains(query) ||
                (g.ParentGroupName != null && g.ParentGroupName.ToLowerInvariant().Contains(query))
            ).ToList();

            FilteredGroups = new ObservableCollection<AccountGroupDto>(filtered);
        }

        if (SelectedGroup != null && !FilteredGroups.Contains(SelectedGroup))
        {
            SelectedGroup = FilteredGroups.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task AddGroupAsync(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var editVm = new AccountGroupEditViewModel(_accountGroupService, _contextService.ActiveCompany.Id);
        await editVm.InitializeAsync();

        var dialog = new AccountGroupEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.IsSaved)
        {
            await LoadGroupsAsync();
            if (editVm.SavedGroup != null)
            {
                SelectedGroup = FilteredGroups.FirstOrDefault(g => g.Id == editVm.SavedGroup.Id);
            }
            StatusMessage = $"Group '{editVm.SavedGroup?.Name}' created successfully.";
        }
    }

    [RelayCommand]
    public async Task ModifyGroupAsync(Window owner)
    {
        if (_contextService.ActiveCompany == null || SelectedGroup == null) return;

        var editVm = new AccountGroupEditViewModel(_accountGroupService, _contextService.ActiveCompany.Id, SelectedGroup);
        await editVm.InitializeAsync();

        var dialog = new AccountGroupEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.IsSaved)
        {
            await LoadGroupsAsync();
            if (editVm.SavedGroup != null)
            {
                SelectedGroup = FilteredGroups.FirstOrDefault(g => g.Id == editVm.SavedGroup.Id);
            }
            StatusMessage = $"Group '{editVm.SavedGroup?.Name}' updated successfully.";
        }
    }

    [RelayCommand]
    public async Task DeleteGroupAsync()
    {
        if (_contextService.ActiveCompany == null || SelectedGroup == null) return;

        if (SelectedGroup.IsDefaultSystemGroup)
        {
            MessageBox.Show(
                $"Account Group '{SelectedGroup.Name}' is a predefined system group and cannot be deleted.",
                "Protected System Group",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete account group '{SelectedGroup.Name}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                ErrorMessage = null;
                await _accountGroupService.DeleteGroupAsync(_contextService.ActiveCompany.Id, SelectedGroup.Id);
                var deletedName = SelectedGroup.Name;
                await LoadGroupsAsync();
                StatusMessage = $"Account group '{deletedName}' deleted successfully.";
            }
            catch (DomainException dex)
            {
                MessageBox.Show(dex.Message, "Cannot Delete Group", MessageBoxButton.OK, MessageBoxImage.Warning);
                ErrorMessage = dex.Message;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ErrorMessage = ex.Message;
            }
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}