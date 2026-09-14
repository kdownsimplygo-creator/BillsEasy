using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class FinancialYearManagementViewModel : ViewModelBase
{
    private readonly FinancialYearService _fyService;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private string _companyTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FinancialYearDto> _financialYears = new();

    [ObservableProperty]
    private FinancialYearDto? _selectedFinancialYear;

    [ObservableProperty]
    private DateTime _newStartDate;

    [ObservableProperty]
    private DateTime _newEndDate;

    [ObservableProperty]
    private string _newYearName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public event Action? RequestClose;

    public FinancialYearManagementViewModel(FinancialYearService fyService, ICompanyContextService contextService)
    {
        _fyService = fyService;
        _contextService = contextService;

        if (_contextService.ActiveCompany != null)
        {
            CompanyTitle = $"Financial Years for: {_contextService.ActiveCompany.CompanyName}";
            var latestFy = _contextService.ActiveFinancialYear;
            if (latestFy != null)
            {
                var nextStart = latestFy.EndDate.AddDays(1);
                var nextEnd = nextStart.AddYears(1).AddDays(-1);
                NewStartDate = nextStart.ToDateTime(TimeOnly.MinValue);
                NewEndDate = nextEnd.ToDateTime(TimeOnly.MinValue);
                NewYearName = $"{nextStart.Year}-{nextEnd.Year}";
            }
            else
            {
                NewStartDate = new DateTime(DateTime.Today.Year, 4, 1);
                NewEndDate = new DateTime(DateTime.Today.Year + 1, 3, 31);
                NewYearName = $"{DateTime.Today.Year}-{DateTime.Today.Year + 1}";
            }
        }
    }

    [RelayCommand]
    public async Task LoadYearsAsync()
    {
        if (_contextService.ActiveCompany == null) return;

        try
        {
            var list = await _fyService.GetFinancialYearsByCompanyAsync(_contextService.ActiveCompany.Id);
            FinancialYears = new ObservableCollection<FinancialYearDto>(list);
            SelectedFinancialYear = FinancialYears.FirstOrDefault(f => f.IsActive) ?? FinancialYears.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading financial years: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SetActiveYearAsync()
    {
        if (_contextService.ActiveCompany == null || SelectedFinancialYear == null) return;

        try
        {
            await _fyService.SelectActiveFinancialYearAsync(_contextService.ActiveCompany.Id, SelectedFinancialYear.Id);
            StatusMessage = $"Active Financial Year switched to '{SelectedFinancialYear.YearName}'.";
            await LoadYearsAsync();
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error setting active year: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CreateYearAsync()
    {
        if (_contextService.ActiveCompany == null) return;

        try
        {
            StatusMessage = string.Empty;
            var start = DateOnly.FromDateTime(NewStartDate);
            var end = DateOnly.FromDateTime(NewEndDate);

            await _fyService.CreateFinancialYearAsync(
                _contextService.ActiveCompany.Id,
                start,
                end,
                string.IsNullOrWhiteSpace(NewYearName) ? null : NewYearName);

            StatusMessage = "Financial Year created successfully.";
            await LoadYearsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
