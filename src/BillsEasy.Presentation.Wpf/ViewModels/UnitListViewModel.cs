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

public partial class UnitListViewModel : ViewModelBase
{
    private readonly IUnitService _unitService;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private ObservableCollection<UnitDto> _units = new();

    [ObservableProperty]
    private ObservableCollection<UnitDto> _filteredUnits = new();

    [ObservableProperty]
    private UnitDto? _selectedUnit;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public string ActiveCompanyName => _contextService.ActiveCompany?.CompanyName ?? "No Company";

    public event Action? RequestClose;

    public UnitListViewModel(
        IUnitService unitService,
        ICompanyContextService contextService)
    {
        _unitService = unitService;
        _contextService = contextService;
    }

    [RelayCommand]
    public async Task LoadUnitsAsync()
    {
        if (_contextService.ActiveCompany == null)
            return;

        try
        {
            ErrorMessage = null;
            var list = await _unitService.GetAllUnitsAsync(_contextService.ActiveCompany.Id);
            Units = new ObservableCollection<UnitDto>(list);
            ApplyFilter();
            StatusMessage = $"Loaded {Units.Count} units.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load units: {ex.Message}";
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
            FilteredUnits = new ObservableCollection<UnitDto>(Units);
        }
        else
        {
            var term = SearchText.Trim();
            FilteredUnits = new ObservableCollection<UnitDto>(
                Units.Where(u => u.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                              || u.PrintName.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }
    }

    [RelayCommand]
    public void AddUnit(Window? owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var editVm = new UnitEditViewModel(_unitService, _contextService.ActiveCompany.Id);
        var dialog = new UnitEditDialog(editVm)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true && editVm.SavedUnit != null)
        {
            Units.Add(editVm.SavedUnit);
            ApplyFilter();
            SelectedUnit = editVm.SavedUnit;
            StatusMessage = $"Created unit '{editVm.SavedUnit.Name}'.";
        }
    }

    [RelayCommand]
    public void ModifyUnit(Window? owner)
    {
        if (_contextService.ActiveCompany == null || SelectedUnit == null) return;

        var editVm = new UnitEditViewModel(_unitService, _contextService.ActiveCompany.Id, SelectedUnit);
        var dialog = new UnitEditDialog(editVm)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() == true && editVm.SavedUnit != null)
        {
            var index = Units.IndexOf(SelectedUnit);
            if (index >= 0)
            {
                Units[index] = editVm.SavedUnit;
            }
            ApplyFilter();
            SelectedUnit = editVm.SavedUnit;
            StatusMessage = $"Updated unit '{editVm.SavedUnit.Name}'.";
        }
    }

    [RelayCommand]
    public async Task DeleteUnitAsync()
    {
        if (_contextService.ActiveCompany == null || SelectedUnit == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete unit '{SelectedUnit.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            ErrorMessage = null;
            await _unitService.DeleteUnitAsync(_contextService.ActiveCompany.Id, SelectedUnit.Id);
            StatusMessage = $"Deleted unit '{SelectedUnit.Name}'.";
            Units.Remove(SelectedUnit);
            ApplyFilter();
            SelectedUnit = null;
        }
        catch (DomainException dex)
        {
            ErrorMessage = dex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete unit: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Close()
    {
        RequestClose?.Invoke();
    }
}
