using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Inventory;

public partial class UnitEditDialog : Window
{
    public UnitEditDialog(UnitEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += () =>
        {
            DialogResult = viewModel.IsSaved;
            Close();
        };
    }
}
