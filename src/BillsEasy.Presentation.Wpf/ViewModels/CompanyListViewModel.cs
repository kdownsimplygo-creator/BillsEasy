using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class CompanyListViewModel : ViewModelBase
{
    private readonly CompanyService _companyService;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private ObservableCollection<CompanyDto> _companies = new();

    [ObservableProperty]
    private CompanyDto? _selectedCompany;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action? RequestClose;

    public CompanyListViewModel(CompanyService companyService, ICompanyContextService contextService)
    {
        _companyService = companyService;
        _contextService = contextService;
    }

    [RelayCommand]
    public async Task LoadCompaniesAsync()
    {
        try
        {
            var list = await _companyService.GetAllCompaniesAsync();
            Companies = new ObservableCollection<CompanyDto>(list);
            if (Companies.Count > 0 && SelectedCompany == null)
            {
                SelectedCompany = Companies[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading companies: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task OpenSelectedCompanyAsync()
    {
        if (SelectedCompany == null)
        {
            StatusMessage = "Please select a company to open.";
            return;
        }

        try
        {
            var company = await _companyService.GetCompanyByIdAsync(SelectedCompany.Id);
            if (company != null)
            {
                // Retrieve domain entity for context
                var entity = await _companyService.GetCompanyByIdAsync(SelectedCompany.Id);
                // Context is set in shell
                RequestClose?.Invoke();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening company: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteSelectedCompanyAsync()
    {
        if (SelectedCompany == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete company '{SelectedCompany.CompanyName}'? This action cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _companyService.DeleteCompanyAsync(SelectedCompany.Id);
                await LoadCompaniesAsync();
                StatusMessage = "Company deleted successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting company: {ex.Message}";
            }
        }
    }
}
