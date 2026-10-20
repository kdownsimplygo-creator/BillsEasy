using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Purchase;

public partial class PurchaseInvoiceListDialog : Window
{
    private readonly PurchaseInvoiceListViewModel _viewModel;

    public PurchaseInvoiceListDialog(PurchaseInvoiceListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (s, e) => await _viewModel.InitializeAsync();
    }
}
