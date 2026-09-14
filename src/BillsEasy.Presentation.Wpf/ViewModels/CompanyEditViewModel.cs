using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BillsEasy.Application.Common;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class CompanyEditViewModel : ViewModelBase
{
    private readonly CompanyService _companyService;
    private readonly Guid? _editingCompanyId;

    [ObservableProperty]
    private string _windowTitle = "Create Company";

    [ObservableProperty]
    private string _companyName = string.Empty;

    [ObservableProperty]
    private string _printName = string.Empty;

    [ObservableProperty]
    private string _shortName = string.Empty;

    [ObservableProperty]
    private IndianStateDto? _selectedState;

    [ObservableProperty]
    private DateTime _financialYearBeginning = new(DateTime.Today.Year, 4, 1);

    [ObservableProperty]
    private DateTime _booksBeginningFrom = new(DateTime.Today.Year, 4, 1);

    [ObservableProperty]
    private string _currencySymbol = "₹";

    [ObservableProperty]
    private string _currencyFormalName = "INR";

    [ObservableProperty]
    private string _gstin = string.Empty;

    [ObservableProperty]
    private GSTRegistrationType _selectedGstType = GSTRegistrationType.Regular;

    [ObservableProperty]
    private bool _isSecurityEnabled;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isEditMode;

    public ObservableCollection<IndianStateDto> States { get; } = new(IndianStates.All);
    public ObservableCollection<GSTRegistrationType> GstTypes { get; } = new(Enum.GetValues<GSTRegistrationType>());

    public event Action? RequestClose;
    public CompanyDto? SavedCompany { get; private set; }

    public CompanyEditViewModel(CompanyService companyService, CompanyDto? existingCompany = null)
    {
        _companyService = companyService;

        if (existingCompany != null)
        {
            _editingCompanyId = existingCompany.Id;
            IsEditMode = true;
            WindowTitle = $"Edit Company — {existingCompany.CompanyName}";

            CompanyName = existingCompany.CompanyName;
            PrintName = existingCompany.PrintName;
            ShortName = existingCompany.ShortName ?? string.Empty;
            FinancialYearBeginning = existingCompany.FinancialYearBeginning.ToDateTime(TimeOnly.MinValue);
            BooksBeginningFrom = existingCompany.BooksBeginningFrom.ToDateTime(TimeOnly.MinValue);
            CurrencySymbol = existingCompany.CurrencySymbol;
            CurrencyFormalName = existingCompany.CurrencyFormalName;
            Gstin = existingCompany.GSTIN ?? string.Empty;
            SelectedGstType = existingCompany.GSTRegistrationType;
            IsSecurityEnabled = existingCompany.IsSecurityEnabled;

            foreach (var state in States)
            {
                if (state.StateCode == existingCompany.StateCode)
                {
                    SelectedState = state;
                    break;
                }
            }
        }
        else
        {
            SelectedState = States[23]; // Default to Gujarat (24)
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            ErrorMessage = "Company Name is required.";
            return;
        }

        if (SelectedState == null)
        {
            ErrorMessage = "State must be selected.";
            return;
        }

        try
        {
            var fyStart = DateOnly.FromDateTime(FinancialYearBeginning);
            var booksStart = DateOnly.FromDateTime(BooksBeginningFrom);

            if (_editingCompanyId.HasValue)
            {
                SavedCompany = await _companyService.UpdateCompanyAsync(
                    _editingCompanyId.Value,
                    CompanyName,
                    PrintName,
                    ShortName,
                    SelectedState.StateName,
                    SelectedState.StateCode,
                    Gstin,
                    SelectedGstType,
                    IsSecurityEnabled);
            }
            else
            {
                SavedCompany = await _companyService.CreateCompanyAsync(
                    CompanyName,
                    PrintName,
                    ShortName,
                    SelectedState.StateName,
                    SelectedState.StateCode,
                    fyStart,
                    booksStart,
                    Gstin,
                    SelectedGstType,
                    currencySymbol: CurrencySymbol,
                    currencyFormalName: CurrencyFormalName,
                    isSecurityEnabled: IsSecurityEnabled);
            }

            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
