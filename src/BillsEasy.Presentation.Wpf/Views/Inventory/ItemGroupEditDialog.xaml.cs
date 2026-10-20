using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Inventory;

public partial class ItemGroupEditDialog : Window
{
    public ItemGroupEditDialog(ItemGroupEditViewModel viewModel)
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
