using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BillsEasy.Application.Common;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Domain.Common;
using BillsEasy.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class LedgerEditViewModel : ViewModelBase
{
    private readonly ILedgerService _ledgerService;
    private readonly IAccountGroupService _accountGroupService;
    private readonly Guid _companyId;
    private readonly LedgerDto? _existingLedger;

    [ObservableProperty]
    private string _dialogTitle = "Add Account / Ledger";

    // Basic Master Details
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _printName = string.Empty;

    [ObservableProperty]
    private string? _alias;

    [ObservableProperty]
    private AccountGroupLookupDto? _selectedGroup;

    [ObservableProperty]
    private ObservableCollection<AccountGroupLookupDto> _groupOptions = new();

    // Opening Balance Setup
    [ObservableProperty]
    private decimal _openingBalance;

    [ObservableProperty]
    private DebitCreditType _selectedOpeningBalanceType = DebitCreditType.Debit;

    [ObservableProperty]
    private ObservableCollection<DebitCreditType> _openingBalanceTypeOptions = new();

    // Party / Bill-Wise & Credit Terms
    [ObservableProperty]
    private bool _billWiseEnabled;

    [ObservableProperty]
    private int? _creditPeriodDays;

    [ObservableProperty]
    private decimal? _creditLimit;

    // Statutory & Address
    [ObservableProperty]
    private string? _addressLine1;

    [ObservableProperty]
    private string? _addressLine2;

    [ObservableProperty]
    private string? _city;

    [ObservableProperty]
    private IndianStateDto? _selectedState;

    [ObservableProperty]
    private ObservableCollection<IndianStateDto> _states = new();

    [ObservableProperty]
    private string? _pinCode;

    [ObservableProperty]
    private string _country = "India";

    [ObservableProperty]
    private string? _gstin;

    [ObservableProperty]
    private GSTRegistrationType _selectedGstType = GSTRegistrationType.Regular;

    [ObservableProperty]
    private ObservableCollection<GSTRegistrationType> _gstTypes = new();

    [ObservableProperty]
    private string? _pan;

    [ObservableProperty]
    private string? _aadhaarNumber;

    // Contact Details
    [ObservableProperty]
    private string? _contactPerson;

    [ObservableProperty]
    private string? _mobileNumber;

    [ObservableProperty]
    private string? _whatsAppNumber;

    [ObservableProperty]
    private string? _email;

    // Bank Details
    [ObservableProperty]
    private string? _bankName;

    [ObservableProperty]
    private string? _bankAccountNumber;

    [ObservableProperty]
    private string? _bankIFSC;

    [ObservableProperty]
    private string? _bankBranch;

    // System Protection
    [ObservableProperty]
    private bool _isDefaultSystemLedger;

    [ObservableProperty]
    private string? _errorMessage;

    public LedgerDto? SavedLedger { get; private set; }
    public bool IsSaved { get; private set; }

    public event Action? RequestClose;

    public LedgerEditViewModel(
        ILedgerService ledgerService,
        IAccountGroupService accountGroupService,
        Guid companyId,
        LedgerDto? existingLedger = null)
    {
        _ledgerService = ledgerService;
        _accountGroupService = accountGroupService;
        _companyId = companyId;
        _existingLedger = existingLedger;

        OpeningBalanceTypeOptions = new ObservableCollection<DebitCreditType>(Enum.GetValues<DebitCreditType>());
        GstTypes = new ObservableCollection<GSTRegistrationType>(Enum.GetValues<GSTRegistrationType>());
        States = new ObservableCollection<IndianStateDto>(
            IndianStates.All.Select(s => new IndianStateDto(s.StateName, s.StateCode)));

        if (existingLedger != null)
        {
            DialogTitle = $"Modify Ledger — {existingLedger.Name}";
            Name = existingLedger.Name;
            PrintName = existingLedger.PrintName;
            Alias = existingLedger.Alias;
            OpeningBalance = existingLedger.OpeningBalance;
            SelectedOpeningBalanceType = existingLedger.OpeningBalanceType;
            BillWiseEnabled = existingLedger.BillWiseEnabled;
            CreditPeriodDays = existingLedger.CreditPeriodDays;
            CreditLimit = existingLedger.CreditLimit;
            AddressLine1 = existingLedger.AddressLine1;
            AddressLine2 = existingLedger.AddressLine2;
            City = existingLedger.City;
            PinCode = existingLedger.PinCode;
            Country = existingLedger.Country;
            Gstin = existingLedger.GSTIN;
            SelectedGstType = existingLedger.GSTRegistrationType;
            Pan = existingLedger.PAN;
            AadhaarNumber = existingLedger.AadhaarNumber;
            ContactPerson = existingLedger.ContactPerson;
            MobileNumber = existingLedger.MobileNumber;
            WhatsAppNumber = existingLedger.WhatsAppNumber;
            Email = existingLedger.Email;
            BankName = existingLedger.BankName;
            BankAccountNumber = existingLedger.BankAccountNumber;
            BankIFSC = existingLedger.BankIFSC;
            BankBranch = existingLedger.BankBranch;
            IsDefaultSystemLedger = existingLedger.IsDefaultSystemLedger;
        }
        else
        {
            DialogTitle = "Add Account / Ledger";
            SelectedState = States.FirstOrDefault(s => s.StateName.Equals("Gujarat", StringComparison.OrdinalIgnoreCase)) ?? States.FirstOrDefault();
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            var groups = await _accountGroupService.GetLookupGroupsAsync(_companyId);
            GroupOptions = new ObservableCollection<AccountGroupLookupDto>(groups);

            if (_existingLedger != null)
            {
                SelectedGroup = GroupOptions.FirstOrDefault(g => g.Id == _existingLedger.GroupId);
                SelectedState = States.FirstOrDefault(s => s.StateName.Equals(_existingLedger.StateName, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                SelectedGroup = GroupOptions.FirstOrDefault(g => g.Name.Equals("Sundry Debtors", StringComparison.OrdinalIgnoreCase))
                             ?? GroupOptions.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading account groups: {ex.Message}";
        }
    }

    partial void OnNameChanged(string value)
    {
        if (_existingLedger == null && (string.IsNullOrWhiteSpace(PrintName) || PrintName == value.Substring(0, Math.Max(0, value.Length - 1))))
        {
            PrintName = value;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Ledger name is required.";
            return;
        }

        if (SelectedGroup == null)
        {
            ErrorMessage = "Please select an Account Group.";
            return;
        }

        try
        {
            if (_existingLedger == null)
            {
                var createDto = new CreateLedgerDto(
                    _companyId,
                    SelectedGroup.Id,
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(PrintName) ? Name.Trim() : PrintName.Trim(),
                    Alias,
                    OpeningBalance,
                    SelectedOpeningBalanceType,
                    BillWiseEnabled,
                    CreditPeriodDays,
                    CreditLimit,
                    AddressLine1,
                    AddressLine2,
                    City,
                    SelectedState?.StateName,
                    SelectedState?.StateCode,
                    PinCode,
                    Country,
                    Gstin,
                    SelectedGstType,
                    Pan,
                    AadhaarNumber,
                    ContactPerson,
                    MobileNumber,
                    WhatsAppNumber,
                    Email,
                    BankName,
                    BankAccountNumber,
                    BankIFSC,
                    BankBranch);

                SavedLedger = await _ledgerService.CreateLedgerAsync(createDto);
            }
            else
            {
                var updateDto = new UpdateLedgerDto(
                    _existingLedger.Id,
                    _companyId,
                    SelectedGroup.Id,
                    Name.Trim(),
                    string.IsNullOrWhiteSpace(PrintName) ? Name.Trim() : PrintName.Trim(),
                    Alias,
                    OpeningBalance,
                    SelectedOpeningBalanceType,
                    BillWiseEnabled,
                    CreditPeriodDays,
                    CreditLimit,
                    AddressLine1,
                    AddressLine2,
                    City,
                    SelectedState?.StateName,
                    SelectedState?.StateCode,
                    PinCode,
                    Country,
                    Gstin,
                    SelectedGstType,
                    Pan,
                    AadhaarNumber,
                    ContactPerson,
                    MobileNumber,
                    WhatsAppNumber,
                    Email,
                    BankName,
                    BankAccountNumber,
                    BankIFSC,
                    BankBranch);

                SavedLedger = await _ledgerService.UpdateLedgerAsync(updateDto);
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
            ErrorMessage = $"An error occurred while saving: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke();
    }
}