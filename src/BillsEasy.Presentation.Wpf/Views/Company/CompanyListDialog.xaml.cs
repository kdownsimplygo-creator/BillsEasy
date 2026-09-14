using System.Windows;
using System.Windows.Input;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Company;

public partial class CompanyListDialog : Window
{
    private readonly CompanyListViewModel _viewModel;

    public CompanyListDialog(CompanyListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += () => { DialogResult = true; Close(); };
        Loaded += async (s, e) => await _viewModel.LoadCompaniesAsync();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedCompany != null)
        {
            _viewModel.OpenSelectedCompanyCommand.Execute(null);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
