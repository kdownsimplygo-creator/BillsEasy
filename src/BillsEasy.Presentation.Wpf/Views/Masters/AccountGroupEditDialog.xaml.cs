using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Masters;

public partial class AccountGroupEditDialog : Window
{
    public AccountGroupEditDialog(AccountGroupEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += () =>
        {
            DialogResult = viewModel.IsSaved;
            Close();
        };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}