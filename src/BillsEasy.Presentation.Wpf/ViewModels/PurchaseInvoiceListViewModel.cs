using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using BillsEasy.Application.Common.Interfaces;
using BillsEasy.Application.DTOs;
using BillsEasy.Presentation.Wpf.Views.Purchase;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BillsEasy.Presentation.Wpf.ViewModels;

public partial class PurchaseInvoiceListViewModel : ViewModelBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ICompanyContextService _contextService;
    private readonly Func<PurchaseInvoiceEditViewModel> _purchaseEditVmFactory;

    [ObservableProperty]
    private string _title = "Purchase Invoices (BUSY Class 8)";

    [ObservableProperty]
    private ObservableCollection<PurchaseInvoiceSummaryDto> _invoices = new();

    [ObservableProperty]
    private PurchaseInvoiceSummaryDto? _selectedInvoice;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private decimal _grandTotalSum;

    public PurchaseInvoiceListViewModel(
        IPurchaseService purchaseService,
        ICompanyContextService contextService,
        Func<PurchaseInvoiceEditViewModel> purchaseEditVmFactory)
    {
        _purchaseService = purchaseService;
        _contextService = contextService;
        _purchaseEditVmFactory = purchaseEditVmFactory;
    }

    public async Task InitializeAsync()
    {
        await LoadInvoicesAsync();
    }

    [RelayCommand]
    public async Task LoadInvoicesAsync()
    {
        if (_contextService.ActiveCompany == null)
            return;

        var companyId = _contextService.ActiveCompany.Id;
        var fyId = _contextService.ActiveFinancialYear?.Id;

        var list = await _purchaseService.GetSummariesAsync(companyId, fyId);
        Invoices.Clear();
        decimal total = 0;
        foreach (var inv in list)
        {
            Invoices.Add(inv);
            total += inv.GrandTotal;
        }

        TotalCount = Invoices.Count;
        GrandTotalSum = total;
    }

    [RelayCommand]
    public async Task CreateNewInvoiceAsync(Window? owner)
    {
        var editVm = _purchaseEditVmFactory();
        var dialog = new PurchaseInvoiceEditDialog(editVm) { Owner = owner };
        if (dialog.ShowDialog() == true)
        {
            await LoadInvoicesAsync();
        }
    }
}
