using System;
using System.Threading.Tasks;
using System.Windows;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Presentation.Wpf.Views.Company;
using BillsEasy.Presentation.Wpf.Views.Masters;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CompanyService _companyService;
    private readonly FinancialYearService _financialYearService;
    private readonly IAccountGroupService _accountGroupService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICompanyContextService _contextService;

    [ObservableProperty]
    private string _title = "BillsEasy — Native Indian Accounting";

    [ObservableProperty]
    private string _activeCompanyText = "No Company Open";

    [ObservableProperty]
    private string _activeFinancialYearText = "No Financial Year";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _hasActiveCompany;

    public MainWindowViewModel(
        CompanyService companyService,
        FinancialYearService financialYearService,
        IAccountGroupService accountGroupService,
        ILedgerService ledgerService,
        ICompanyRepository companyRepository,
        ICompanyContextService contextService)
    {
        _companyService = companyService;
        _financialYearService = financialYearService;
        _accountGroupService = accountGroupService;
        _ledgerService = ledgerService;
        _companyRepository = companyRepository;
        _contextService = contextService;

        _contextService.ActiveCompanyChanged += (s, e) => UpdateContextDisplay();
        UpdateContextDisplay();
    }

    private void UpdateContextDisplay()
    {
        if (_contextService.ActiveCompany != null)
        {
            HasActiveCompany = true;
            ActiveCompanyText = _contextService.ActiveCompany.CompanyName;
            ActiveFinancialYearText = _contextService.ActiveFinancialYear?.YearName ?? "Default FY";
            Title = $"BillsEasy — {_contextService.ActiveCompany.CompanyName} ({ActiveFinancialYearText})";
            StatusMessage = $"Active: {_contextService.ActiveCompany.CompanyName} | State: {_contextService.ActiveCompany.StateName} ({_contextService.ActiveCompany.StateCode})";
        }
        else
        {
            HasActiveCompany = false;
            ActiveCompanyText = "No Company Open";
            ActiveFinancialYearText = "No Financial Year";
            Title = "BillsEasy — Native Indian Accounting";
            StatusMessage = "Ready — Please Open or Create a Company to begin.";
        }
    }

    [RelayCommand]
    public async Task CreateCompanyAsync(Window owner)
    {
        var editVm = new CompanyEditViewModel(_companyService);
        var dialog = new CompanyEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.SavedCompany != null)
        {
            await OpenCompanyByIdAsync(editVm.SavedCompany.Id);
        }
    }

    [RelayCommand]
    public async Task OpenCompanyAsync(Window owner)
    {
        var listVm = new CompanyListViewModel(_companyService, _contextService);
        var dialog = new CompanyListDialog(listVm) { Owner = owner };
        if (dialog.ShowDialog() == true && listVm.SelectedCompany != null)
        {
            await OpenCompanyByIdAsync(listVm.SelectedCompany.Id);
        }
    }

    [RelayCommand]
    public async Task EditActiveCompanyAsync(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var currentDto = await _companyService.GetCompanyByIdAsync(_contextService.ActiveCompany.Id);
        if (currentDto == null) return;

        var editVm = new CompanyEditViewModel(_companyService, currentDto);
        var dialog = new CompanyEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.SavedCompany != null)
        {
            await OpenCompanyByIdAsync(editVm.SavedCompany.Id);
        }
    }

    [RelayCommand]
    public void ManageFinancialYears(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var fyVm = new FinancialYearManagementViewModel(_financialYearService, _contextService);
        var dialog = new FinancialYearManagementDialog(fyVm) { Owner = owner };
        dialog.ShowDialog();
    }

    [RelayCommand]
    public void ManageAccountGroups(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var groupVm = new AccountGroupListViewModel(_accountGroupService, _contextService);
        var dialog = new AccountGroupManagementDialog(groupVm) { Owner = owner };
        dialog.ShowDialog();
    }

    [RelayCommand]
    public void ManageLedgers(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var ledgerVm = new LedgerListViewModel(_ledgerService, _accountGroupService, _contextService);
        var dialog = new LedgerManagementDialog(ledgerVm) { Owner = owner };
        dialog.ShowDialog();
    }

    [RelayCommand]
    public void CloseActiveCompany()
    {
        _contextService.CloseActiveCompany();
    }

    public async Task OpenCompanyByIdAsync(Guid companyId)
    {
        try
        {
            var companyEntity = await _companyRepository.GetByIdAsync(companyId);
            if (companyEntity != null)
            {
                _contextService.SetActiveContext(companyEntity);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening company: {ex.Message}";
        }
    }
}