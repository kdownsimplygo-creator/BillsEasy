using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Company;

public partial class FinancialYearManagementDialog : Window
{
    private readonly FinancialYearManagementViewModel _viewModel;

    public FinancialYearManagementDialog(FinancialYearManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += () => { DialogResult = true; Close(); };
        Loaded += async (s, e) => await _viewModel.LoadYearsAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
