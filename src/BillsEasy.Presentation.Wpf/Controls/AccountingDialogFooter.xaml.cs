using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BillsEasy.Presentation.Wpf.Controls;

public partial class AccountingDialogFooter : UserControl
{
    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string), typeof(AccountingDialogFooter), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SaveCommandProperty =
        DependencyProperty.Register(nameof(SaveCommand), typeof(ICommand), typeof(AccountingDialogFooter), new PropertyMetadata(null));

    public static readonly DependencyProperty SaveButtonTextProperty =
        DependencyProperty.Register(nameof(SaveButtonText), typeof(string), typeof(AccountingDialogFooter), new PropertyMetadata("_Save (F2)"));

    public static readonly DependencyProperty CancelButtonTextProperty =
        DependencyProperty.Register(nameof(CancelButtonText), typeof(string), typeof(AccountingDialogFooter), new PropertyMetadata("_Cancel (Esc)"));

    public static readonly DependencyProperty ShortcutLegendProperty =
        DependencyProperty.Register(nameof(ShortcutLegend), typeof(string), typeof(AccountingDialogFooter), new PropertyMetadata("[F2] Save   •   [Esc] Cancel   •   [Enter] Next   •   [Shift+Enter] Prev"));

    public static readonly DependencyProperty StatusContentProperty =
        DependencyProperty.Register(nameof(StatusContent), typeof(object), typeof(AccountingDialogFooter), new PropertyMetadata(null));

    public event RoutedEventHandler? CancelClicked;

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public ICommand? SaveCommand
    {
        get => (ICommand?)GetValue(SaveCommandProperty);
        set => SetValue(SaveCommandProperty, value);
    }

    public string SaveButtonText
    {
        get => (string)GetValue(SaveButtonTextProperty);
        set => SetValue(SaveButtonTextProperty, value);
    }

    public string CancelButtonText
    {
        get => (string)GetValue(CancelButtonTextProperty);
        set => SetValue(CancelButtonTextProperty, value);
    }

    public string ShortcutLegend
    {
        get => (string)GetValue(ShortcutLegendProperty);
        set => SetValue(ShortcutLegendProperty, value);
    }

    public object? StatusContent
    {
        get => GetValue(StatusContentProperty);
        set => SetValue(StatusContentProperty, value);
    }

    public Visibility ErrorMessageVisibility => string.IsNullOrWhiteSpace(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    public AccountingDialogFooter()
    {
        InitializeComponent();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (CancelClicked != null)
        {
            CancelClicked(this, e);
        }
        else
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.DialogResult = false;
                window.Close();
            }
        }
    }
}
