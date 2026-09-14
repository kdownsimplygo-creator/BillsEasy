using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Masters;

public partial class LedgerManagementDialog : Window
{
    private readonly LedgerListViewModel _viewModel;

    public LedgerManagementDialog(LedgerListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += () => Close();

        Loaded += async (s, e) =>
        {
            await _viewModel.LoadLedgersAsync();
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}