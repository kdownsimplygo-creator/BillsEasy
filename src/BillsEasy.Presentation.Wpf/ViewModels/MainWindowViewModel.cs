using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Application.Services;
using BillsEasy.Presentation.Wpf.Views.Company;
using BillsEasy.Presentation.Wpf.Views.Masters;
using BillsEasy.Presentation.Wpf.Views.Inventory;
using BillsEasy.Presentation.Wpf.Views.Purchase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly CompanyService _companyService;
    private readonly FinancialYearService _financialYearService;
    private readonly IAccountGroupService _accountGroupService;
    private readonly ILedgerService _ledgerService;
    private readonly IUnitService _unitService;
    private readonly IItemGroupService _itemGroupService;
    private readonly IItemService _itemService;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICompanyContextService _contextService;

    private readonly IPurchaseService _purchaseService;
    private readonly IItemRepository _itemRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly ILedgerBalanceService _ledgerBalanceService;
    private readonly IStockMovementRepository _stockMovementRepository;

    [ObservableProperty]
    private string _title = "BillsEasy — Native Indian Accounting";

    [ObservableProperty]
    private string _activeCompanyText = "No Company Open";

    [ObservableProperty]
    private string _activeFinancialYearText = "No Financial Year";

    [ObservableProperty]
    private string _activeCompanyStateText = "";

    [ObservableProperty]
    private string _activeCompanyBooksBeginningText = "";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _hasActiveCompany;

    [ObservableProperty]
    private decimal _cashBalance;

    [ObservableProperty]
    private string _cashBalanceType = "Dr";

    [ObservableProperty]
    private decimal _bankBalance;

    [ObservableProperty]
    private string _bankBalanceType = "Dr";

    [ObservableProperty]
    private decimal _receivablesBalance;

    [ObservableProperty]
    private string _receivablesBalanceType = "Dr";

    [ObservableProperty]
    private decimal _payablesBalance;

    [ObservableProperty]
    private string _payablesBalanceType = "Cr";

    [ObservableProperty]
    private ObservableCollection<DashboardStockItemDto> _inventorySnapshot = new();

    [ObservableProperty]
    private bool _isLoadingDashboard;

    public MainWindowViewModel(
        CompanyService companyService,
        FinancialYearService financialYearService,
        IAccountGroupService accountGroupService,
        ILedgerService ledgerService,
        IUnitService unitService,
        IItemGroupService itemGroupService,
        IItemService itemService,
        ICompanyRepository companyRepository,
        ICompanyContextService contextService,
        IPurchaseService purchaseService,
        IItemRepository itemRepository,
        IVoucherRepository voucherRepository,
        ILedgerBalanceService ledgerBalanceService,
        IStockMovementRepository stockMovementRepository)
    {
        _companyService = companyService;
        _financialYearService = financialYearService;
        _accountGroupService = accountGroupService;
        _ledgerService = ledgerService;
        _unitService = unitService;
        _itemGroupService = itemGroupService;
        _itemService = itemService;
        _companyRepository = companyRepository;
        _contextService = contextService;
        _purchaseService = purchaseService;
        _itemRepository = itemRepository;
        _voucherRepository = voucherRepository;
        _ledgerBalanceService = ledgerBalanceService ?? throw new ArgumentNullException(nameof(ledgerBalanceService));
        _stockMovementRepository = stockMovementRepository ?? throw new ArgumentNullException(nameof(stockMovementRepository));

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
            ActiveCompanyStateText = $"{_contextService.ActiveCompany.StateName} ({_contextService.ActiveCompany.StateCode})";
            ActiveCompanyBooksBeginningText = _contextService.ActiveCompany.BooksBeginningFrom.ToString("dd/MM/yyyy");
            Title = $"BillsEasy — {_contextService.ActiveCompany.CompanyName} ({ActiveFinancialYearText})";
            StatusMessage = $"Active: {_contextService.ActiveCompany.CompanyName} | State: {_contextService.ActiveCompany.StateName} ({_contextService.ActiveCompany.StateCode})";
            _ = RefreshDashboardMetricsAsync();
        }
        else
        {
            HasActiveCompany = false;
            ActiveCompanyText = "No Company Open";
            ActiveFinancialYearText = "No Financial Year";
            ActiveCompanyStateText = "";
            ActiveCompanyBooksBeginningText = "";
            Title = "BillsEasy — Native Indian Accounting";
            StatusMessage = "Ready — Please Open or Create a Company to begin.";
            _ = RefreshDashboardMetricsAsync();
        }
    }

    [RelayCommand]
    public async Task RefreshDashboardAsync()
    {
        await RefreshDashboardMetricsAsync();
    }

    public async Task RefreshDashboardMetricsAsync()
    {
        if (_contextService.ActiveCompany == null)
        {
            CashBalance = 0;
            BankBalance = 0;
            ReceivablesBalance = 0;
            PayablesBalance = 0;
            InventorySnapshot.Clear();
            return;
        }

        var companyId = _contextService.ActiveCompany.Id;
        IsLoadingDashboard = true;
        try
        {
            var summary = await _ledgerBalanceService.GetDashboardFinancialSummaryAsync(companyId);
            CashBalance = summary.CashBalance;
            CashBalanceType = summary.CashBalanceType;
            BankBalance = summary.BankBalance;
            BankBalanceType = summary.BankBalanceType;
            ReceivablesBalance = summary.ReceivablesBalance;
            ReceivablesBalanceType = summary.ReceivablesBalanceType;
            PayablesBalance = summary.PayablesBalance;
            PayablesBalanceType = summary.PayablesBalanceType;

            var items = await _itemService.GetAllItemsAsync(companyId);
            var snapshotList = new List<DashboardStockItemDto>();
            foreach (var item in items.Where(i => i.IsActive))
            {
                var qty = await _stockMovementRepository.GetCurrentQuantityAsync(companyId, item.Id);
                var status = qty > 0 ? "In Stock" : "Out of Stock";
                snapshotList.Add(new DashboardStockItemDto(item.Id, item.Name, item.Code, qty, item.UnitName, status));
            }

            InventorySnapshot.Clear();
            foreach (var s in snapshotList)
            {
                InventorySnapshot.Add(s);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Dashboard refresh notice: {ex.Message}";
        }
        finally
        {
            IsLoadingDashboard = false;
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
    public void ManageUnits(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var unitVm = new UnitListViewModel(_unitService, _contextService);
        var dialog = new UnitManagementDialog(unitVm) { Owner = owner };
        dialog.ShowDialog();
    }

    [RelayCommand]
    public void ManageItemGroups(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var groupVm = new ItemGroupListViewModel(_itemGroupService, _contextService);
        var dialog = new ItemGroupManagementDialog(groupVm) { Owner = owner };
        dialog.ShowDialog();
    }

    [RelayCommand]
    public void ManageItems(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var itemVm = new ItemListViewModel(_itemService, _itemGroupService, _unitService, _contextService);
        var dialog = new ItemManagementDialog(itemVm) { Owner = owner };
        dialog.ShowDialog();
    }

    [RelayCommand]
    public void OpenPurchaseInvoice(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var editVm = new PurchaseInvoiceEditViewModel(
            _purchaseService,
            _ledgerService,
            _accountGroupService,
            _itemService,
            _unitService,
            _itemRepository,
            _voucherRepository,
            _contextService);

        var dialog = new PurchaseInvoiceEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true && editVm.SavedInvoice != null)
        {
            StatusMessage = $"Purchase invoice '{editVm.SavedInvoice.InvoiceNumber}' recorded successfully (₹{editVm.SavedInvoice.GrandTotal:N2}).";
            _ = RefreshDashboardMetricsAsync();
        }
    }

    [RelayCommand]
    public void OpenPurchaseList(Window owner)
    {
        if (_contextService.ActiveCompany == null) return;

        var listVm = new PurchaseInvoiceListViewModel(
            _purchaseService,
            _contextService,
            () => new PurchaseInvoiceEditViewModel(
                _purchaseService,
                _ledgerService,
                _accountGroupService,
                _itemService,
                _unitService,
                _itemRepository,
                _voucherRepository,
                _contextService));

        var dialog = new PurchaseInvoiceListDialog(listVm) { Owner = owner };
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