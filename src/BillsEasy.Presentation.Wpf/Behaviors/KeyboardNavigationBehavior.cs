using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace BillsEasy.Presentation.Wpf.Behaviors;

/// <summary>
/// Provides centralized, deterministic keyboard navigation for Indian accounting data entry.
/// Handles Enter / Shift+Enter field traversal, multiline Ctrl+Enter, ComboBox popup state,
/// DataGrid boundary isolation, and initial focus establishment.
/// </summary>
public static class KeyboardNavigationBehavior
{
    #region AdvanceOnEnter Attached Property

    public static readonly DependencyProperty AdvanceOnEnterProperty =
        DependencyProperty.RegisterAttached(
            "AdvanceOnEnter",
            typeof(bool),
            typeof(KeyboardNavigationBehavior),
            new UIPropertyMetadata(false, OnAdvanceOnEnterChanged));

    public static bool GetAdvanceOnEnter(DependencyObject obj) =>
        (bool)obj.GetValue(AdvanceOnEnterProperty);

    public static void SetAdvanceOnEnter(DependencyObject obj, bool value) =>
        obj.SetValue(AdvanceOnEnterProperty, value);

    private static void OnAdvanceOnEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
            {
                element.KeyDown += Element_KeyDown;
            }
            else
            {
                element.KeyDown -= Element_KeyDown;
            }
        }
    }

    #endregion

    #region IsAutoAdvanceEnabled Attached Property

    public static readonly DependencyProperty IsAutoAdvanceEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsAutoAdvanceEnabled",
            typeof(bool),
            typeof(KeyboardNavigationBehavior),
            new UIPropertyMetadata(false, OnIsAutoAdvanceEnabledChanged));

    public static bool GetIsAutoAdvanceEnabled(DependencyObject obj) =>
        (bool)obj.GetValue(IsAutoAdvanceEnabledProperty);

    public static void SetIsAutoAdvanceEnabled(DependencyObject obj, bool value) =>
        obj.SetValue(IsAutoAdvanceEnabledProperty, value);

    private static void OnIsAutoAdvanceEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element)
        {
            if ((bool)e.NewValue)
            {
                element.PreviewKeyDown += Container_PreviewKeyDown;
            }
            else
            {
                element.PreviewKeyDown -= Container_PreviewKeyDown;
            }
        }
    }

    #endregion

    #region InitialFocusElement Attached Property

    public static readonly DependencyProperty InitialFocusElementProperty =
        DependencyProperty.RegisterAttached(
            "InitialFocusElement",
            typeof(FrameworkElement),
            typeof(KeyboardNavigationBehavior),
            new PropertyMetadata(null, OnInitialFocusElementChanged));

    public static FrameworkElement? GetInitialFocusElement(DependencyObject obj) =>
        (FrameworkElement?)obj.GetValue(InitialFocusElementProperty);

    public static void SetInitialFocusElement(DependencyObject obj, FrameworkElement? value) =>
        obj.SetValue(InitialFocusElementProperty, value);

    private static void OnInitialFocusElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe)
        {
            fe.Loaded += (s, args) =>
            {
                var target = GetInitialFocusElement(fe);
                if (target != null && target.IsLoaded)
                {
                    target.Focus();
                    if (target is TextBox tb)
                    {
                        tb.SelectAll();
                    }
                }
            };
        }
    }

    #endregion

    #region Key Event Handlers & Deterministic Navigation Rules

    private static void Element_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter) return;

        TryNavigate(sender as DependencyObject, e);
    }

    private static void Container_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Enter) return;

        var focused = Keyboard.FocusedElement as DependencyObject ?? e.OriginalSource as DependencyObject;
        if (focused != null)
        {
            TryNavigate(focused, e);
        }
    }

    private static void TryNavigate(DependencyObject? source, KeyEventArgs e)
    {
        if (source == null) return;

        // Rule 1: Never intercept Enter inside a DataGrid (DataGrid manages cell/row commit traversal)
        if (IsInsideDataGrid(source)) return;

        // Rule 2: Never advance on Button or ButtonBase (Enter triggers Click / Command)
        if (source is ButtonBase || FindVisualParent<ButtonBase>(source) != null) return;

        // Rule 3: Multiline TextBox handling
        var textBox = source as TextBox ?? FindVisualParent<TextBox>(source);
        if (textBox != null && textBox.AcceptsReturn)
        {
            // If Ctrl+Enter is pressed, advance to the next field
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                NavigateDirection(textBox, FocusNavigationDirection.Next);
                e.Handled = true;
            }
            // Otherwise, Enter alone inserts a newline (native editing)
            return;
        }

        // Rule 4: ComboBox handling
        var comboBox = source as ComboBox ?? FindVisualParent<ComboBox>(source);
        if (comboBox != null)
        {
            if (comboBox.IsDropDownOpen)
            {
                // Let the ComboBox commit selection and close dropdown; do NOT advance focus
                return;
            }

            // Dropdown is closed: Advance on Enter
            var dir = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                ? FocusNavigationDirection.Previous
                : FocusNavigationDirection.Next;

            NavigateDirection(comboBox, dir);
            e.Handled = true;
            return;
        }

        // Rule 5: DatePicker handling
        var datePicker = source as DatePicker ?? FindVisualParent<DatePicker>(source);
        if (datePicker != null)
        {
            if (datePicker.IsDropDownOpen)
            {
                datePicker.IsDropDownOpen = false;
                e.Handled = true;
                return;
            }

            var dir = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                ? FocusNavigationDirection.Previous
                : FocusNavigationDirection.Next;

            NavigateDirection(datePicker, dir);
            e.Handled = true;
            return;
        }

        // Rule 6: Standard Single-Line Input / Control traversal
        var uie = source as UIElement ?? FindVisualParent<UIElement>(source);
        if (uie != null)
        {
            var dir = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                ? FocusNavigationDirection.Previous
                : FocusNavigationDirection.Next;

            NavigateDirection(uie, dir);
            e.Handled = true;
        }
    }

    private static void NavigateDirection(UIElement element, FocusNavigationDirection direction)
    {
        element.MoveFocus(new TraversalRequest(direction));
    }

    private static bool IsInsideDataGrid(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is DataGrid || current is DataGridCell || current is DataGridRow)
                return true;

            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static T? FindVisualParent<T>(DependencyObject element) where T : DependencyObject
    {
        var current = element;
        while (current != null)
        {
            if (current is T match)
                return match;

            if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
            {
                current = VisualTreeHelper.GetParent(current);
            }
            else
            {
                current = LogicalTreeHelper.GetParent(current);
            }
        }
        return null;
    }

    #endregion
}
