using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Inventory;

public partial class ItemEditDialog : Window
{
    public ItemEditDialog(ItemEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += () =>
        {
            DialogResult = viewModel.IsSaved;
            Close();
        };

        Loaded += async (s, e) => await viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
