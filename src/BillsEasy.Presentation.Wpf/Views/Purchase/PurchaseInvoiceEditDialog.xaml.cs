using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Purchase;

public partial class PurchaseInvoiceEditDialog : Window
{
    private readonly PurchaseInvoiceEditViewModel _viewModel;

    public PurchaseInvoiceEditDialog(PurchaseInvoiceEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (s, e) => await _viewModel.InitializeAsync();
    }

    private void DataGrid_GotFocus(object sender, RoutedEventArgs e)
    {
        var cell = FindParentCell(e.OriginalSource as DependencyObject);
        if (cell != null && !cell.IsReadOnly && !cell.IsEditing)
        {
            cell.IsEditing = true;
        }
    }

    private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid dataGrid) return;
        var cell = FindParentCell(e.OriginalSource as DependencyObject);
        if (cell != null && !cell.IsReadOnly && !cell.IsEditing)
        {
            cell.Focus();
            dataGrid.BeginEdit();
        }
    }

    private void ComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            cb.Focus();
            if (cb.IsEditable && cb.Template.FindName("PART_EditableTextBox", cb) is TextBox tb)
            {
                tb.Focus();
                tb.SelectAll();
            }
        }
    }

    private void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            if (e.Key == Key.Down && !cb.IsDropDownOpen)
            {
                cb.IsDropDownOpen = true;
                e.Handled = true;
            }
        }
    }

    private static DataGridCell? FindParentCell(DependencyObject? obj)
    {
        while (obj != null && obj is not DataGridCell)
        {
            if (obj is Visual || obj is System.Windows.Media.Media3D.Visual3D)
                obj = VisualTreeHelper.GetParent(obj);
            else
                obj = LogicalTreeHelper.GetParent(obj);
        }
        return obj as DataGridCell;
    }
}
