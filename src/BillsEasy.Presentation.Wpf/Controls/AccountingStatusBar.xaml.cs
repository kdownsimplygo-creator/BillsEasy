using System.Windows;
using System.Windows.Controls;

namespace BillsEasy.Presentation.Wpf.Controls;

public partial class AccountingStatusBar : UserControl
{
    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(nameof(StatusMessage), typeof(string), typeof(AccountingStatusBar), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActiveCompanyProperty =
        DependencyProperty.Register(nameof(ActiveCompany), typeof(string), typeof(AccountingStatusBar), new PropertyMetadata("None"));

    public static readonly DependencyProperty ActiveFinancialYearProperty =
        DependencyProperty.Register(nameof(ActiveFinancialYear), typeof(string), typeof(AccountingStatusBar), new PropertyMetadata("None"));

    public static readonly DependencyProperty CurrentUserProperty =
        DependencyProperty.Register(nameof(CurrentUser), typeof(string), typeof(AccountingStatusBar), new PropertyMetadata("system_admin"));

    public static readonly DependencyProperty VersionTagProperty =
        DependencyProperty.Register(nameof(VersionTag), typeof(string), typeof(AccountingStatusBar), new PropertyMetadata("BillsEasy 1.4"));

    public string StatusMessage
    {
        get => (string)GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    public string ActiveCompany
    {
        get => (string)GetValue(ActiveCompanyProperty);
        set => SetValue(ActiveCompanyProperty, value);
    }

    public string ActiveFinancialYear
    {
        get => (string)GetValue(ActiveFinancialYearProperty);
        set => SetValue(ActiveFinancialYearProperty, value);
    }

    public string CurrentUser
    {
        get => (string)GetValue(CurrentUserProperty);
        set => SetValue(CurrentUserProperty, value);
    }

    public string VersionTag
    {
        get => (string)GetValue(VersionTagProperty);
        set => SetValue(VersionTagProperty, value);
    }

    public AccountingStatusBar()
    {
        InitializeComponent();
    }
}
