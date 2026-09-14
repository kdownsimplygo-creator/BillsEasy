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

public partial class LedgerListViewModel : ViewModelBase
{
    private readonly ILedgerService _ledgerService;
    private readonly IAccountGroupService _accountGroupService;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private ObservableCollection<LedgerDto> _ledgers = new();

    [ObservableProperty]
    private ObservableCollection<LedgerDto> _filteredLedgers = new();

    [ObservableProperty]
    private LedgerDto? _selectedLedger;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private AccountGroupLookupDto? _selectedGroupFilter;

    [ObservableProperty]
    private ObservableCollection<AccountGroupLookupDto> _groupFilterOptions = new();

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public string ActiveCompanyName => _contextService.ActiveCompany?.CompanyName ?? "No Company";

    public event Action? RequestClose;

    public LedgerListViewModel(
        ILedgerService ledgerService,
        IAccountGroupService accountGroupService,
        ICompanyContextService contextService)
    {
        _ledgerService = ledgerService;
        _accountGroupService = accountGroupService;
        _contextService = contextService;
    }

    [RelayCommand]
    public async Task LoadLedgersAsync()
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
            var list = await _ledgerService.GetAllLedgersAsync(companyId);
            var groups = await _accountGroupService.GetLookupGroupsAsync(companyId);

            Ledgers = new ObservableCollection<LedgerDto>(list);

            var filterOptions = new List<AccountGroupLookupDto>
            {
                new AccountGroupLookupDto(Guid.Empty, "(All Groups)", Domain.Enums.GroupNature.Asset, "All", null, false)
            };
            filterOptions.AddRange(groups);
            GroupFilterOptions = new ObservableCollection<AccountGroupLookupDto>(filterOptions);

            if (SelectedGroupFilter == null)
            {
                SelectedGroupFilter = GroupFilterOptions.FirstOrDefault();
            }

            ApplyFilter();

            if (FilteredLedgers.Count > 0 && SelectedLedger == null)
            {
                SelectedLedger = FilteredLedgers[0];
            }

            StatusMessage = $"Total Ledgers: {Ledgers.Count} ({Ledgers.Count(l => l.IsDefaultSystemLedger)} System, {Ledgers.Count(l => !l.IsDefaultSystemLedger)} User-Created)";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading ledgers: {ex.Message}";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedGroupFilterChanged(AccountGroupLookupDto? value) => ApplyFilter();

    private void ApplyFilter()
    {
        var result = Ledgers.AsEnumerable();

        if (SelectedGroupFilter != null && SelectedGroupFilter.Id != Guid.Empty)
        {
            result = result.Where(l => l.GroupId == SelectedGroupFilter.Id);
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim().ToLowerInvariant();
            result = result.Where(l =>
                l.Name.ToLowerInvariant().Contains(query) ||
                l.PrintName.ToLowerInvariant().Contains(query) ||
                (l.Alias != null && l.Alias.ToLowerInvariant().Contains(query)) ||
                l.GroupName.ToLowerInvariant().Contains(query) ||
                (l.GSTIN != null && l.GSTIN.ToLowerInvariant().Contains(query)) ||
                (l.City != null && l.City.ToLowerInvariant().Contains(query)) ||
                (l.ContactPerson != null && l.ContactPerson.ToLowerInvariant().Contains(query)) ||
                (l.MobileNumber != null && l.MobileNumber.Contains(query)));
        }

        FilteredLedgers = new ObservableCollection<LedgerDto>(result);

        if (SelectedLedger != null && !FilteredLedgers.Contains(SelectedLedger))
        {
            SelectedLedger = FilteredLedgers.FirstOrDefault();
        }
    }

    [RelayCommand]
    public async Task AddLedgerAsync(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var editVm = new LedgerEditViewModel(_ledgerService, _accountGroupService, _contextService.ActiveCompany.Id);
        await editVm.InitializeAsync();

        var dialog = new LedgerEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.IsSaved)
        {
            await LoadLedgersAsync();
            if (editVm.SavedLedger != null)
            {
                SelectedLedger = FilteredLedgers.FirstOrDefault(l => l.Id == editVm.SavedLedger.Id);
            }
            StatusMessage = $"Ledger '{editVm.SavedLedger?.Name}' created successfully.";
        }
    }

    [RelayCommand]
    public async Task ModifyLedgerAsync(Window owner)
    {
        if (_contextService.ActiveCompany == null || SelectedLedger == null) return;

        var editVm = new LedgerEditViewModel(_ledgerService, _accountGroupService, _contextService.ActiveCompany.Id, SelectedLedger);
        await editVm.InitializeAsync();

        var dialog = new LedgerEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.IsSaved)
        {
            await LoadLedgersAsync();
            if (editVm.SavedLedger != null)
            {
                SelectedLedger = FilteredLedgers.FirstOrDefault(l => l.Id == editVm.SavedLedger.Id);
            }
            StatusMessage = $"Ledger '{editVm.SavedLedger?.Name}' updated successfully.";
        }
    }

    [RelayCommand]
    public async Task DeleteLedgerAsync()
    {
        if (_contextService.ActiveCompany == null || SelectedLedger == null) return;

        if (SelectedLedger.IsDefaultSystemLedger)
        {
            MessageBox.Show(
                $"Ledger '{SelectedLedger.Name}' is a predefined system ledger and cannot be deleted.",
                "Protected System Ledger",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete ledger '{SelectedLedger.Name}'?\n\nThis action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                ErrorMessage = null;
                await _ledgerService.DeleteLedgerAsync(_contextService.ActiveCompany.Id, SelectedLedger.Id);
                var deletedName = SelectedLedger.Name;
                await LoadLedgersAsync();
                StatusMessage = $"Ledger '{deletedName}' deleted successfully.";
            }
            catch (DomainException dex)
            {
                MessageBox.Show(dex.Message, "Cannot Delete Ledger", MessageBoxButton.OK, MessageBoxImage.Warning);
                ErrorMessage = dex.Message;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting ledger: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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