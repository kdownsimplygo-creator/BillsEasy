using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Inventory;

public partial class ItemManagementDialog : Window
{
    public ItemManagementDialog(ItemListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += Close;
        Loaded += async (s, e) => await viewModel.LoadItemsCommand.ExecuteAsync(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
