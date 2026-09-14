using System.Windows;
using System.Windows.Controls;

namespace BillsEasy.Presentation.Wpf.Controls;

public partial class AccountingDialogHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(AccountingDialogHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(AccountingDialogHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EntityBadgeProperty =
        DependencyProperty.Register(nameof(EntityBadge), typeof(string), typeof(AccountingDialogHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CompanyContextProperty =
        DependencyProperty.Register(nameof(CompanyContext), typeof(string), typeof(AccountingDialogHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HeaderRightContentProperty =
        DependencyProperty.Register(nameof(HeaderRightContent), typeof(object), typeof(AccountingDialogHeader), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string EntityBadge
    {
        get => (string)GetValue(EntityBadgeProperty);
        set => SetValue(EntityBadgeProperty, value);
    }

    public string CompanyContext
    {
        get => (string)GetValue(CompanyContextProperty);
        set => SetValue(CompanyContextProperty, value);
    }

    public object? HeaderRightContent
    {
        get => GetValue(HeaderRightContentProperty);
        set => SetValue(HeaderRightContentProperty, value);
    }

    public Visibility SubtitleVisibility => string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EntityBadgeVisibility => string.IsNullOrWhiteSpace(EntityBadge) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CompanyContextVisibility => string.IsNullOrWhiteSpace(CompanyContext) ? Visibility.Collapsed : Visibility.Visible;

    public AccountingDialogHeader()
    {
        InitializeComponent();
    }
}
