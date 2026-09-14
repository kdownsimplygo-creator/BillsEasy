using System.Linq;
using System.Windows;
using BillsEasy.Application.DTOs;
using BillsEasy.Presentation.Wpf.ViewModels;

namespace BillsEasy.Presentation.Wpf.Views.Masters;

public partial class AccountGroupManagementDialog : Window
{
    private readonly AccountGroupListViewModel _viewModel;

    public AccountGroupManagementDialog(AccountGroupListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += () => Close();

        Loaded += async (s, e) =>
        {
            await _viewModel.LoadGroupsAsync();
            UpdateViewVisibility();
        };
    }

    private void ListView_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsTreeView = false;
        UpdateViewVisibility();
    }

    private void TreeView_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsTreeView = true;
        UpdateViewVisibility();
    }

    private void UpdateViewVisibility()
    {
        if (HierarchyTreeView == null) return;
        HierarchyTreeView.Visibility = _viewModel.IsTreeView ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HierarchyTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is AccountGroupNodeDto node)
        {
            _viewModel.SelectedGroup = _viewModel.Groups.FirstOrDefault(g => g.Id == node.Id);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}