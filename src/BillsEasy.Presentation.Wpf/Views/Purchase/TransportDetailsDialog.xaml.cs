using System.Windows;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Purchase;

public partial class TransportDetailsDialog : Window
{
    public TransportDetailsDialog(TransportDetailsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
