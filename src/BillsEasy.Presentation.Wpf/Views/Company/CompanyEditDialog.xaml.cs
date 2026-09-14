using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Company;

public partial class CompanyEditDialog : Window
{
    private readonly CompanyEditViewModel _viewModel;

    public CompanyEditDialog(CompanyEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += () => { DialogResult = true; Close(); };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
