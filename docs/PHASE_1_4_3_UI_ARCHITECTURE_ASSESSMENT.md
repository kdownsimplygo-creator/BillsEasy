# Phase 1.4.3 — UI/UX Architecture Assessment Report
**Product**: BillsEasy Native Windows Accounting Suite  
**Date**: 2026-09-13  
**Status**: Completed  
**Review Target**: `src/BillsEasy.Presentation.Wpf`  

---

## 1. Executive Assessment

A comprehensive review of the existing Presentation layer (`BillsEasy.Presentation.Wpf`) was conducted prior to refactoring. The goal of this assessment is to evaluate the presentation architecture against the strict requirements of a high-density, keyboard-first, native desktop accounting application.

### Key Finding
While Phase 1.4.1 established initial centralized styling and attached behaviors (`KeyboardNavigationBehavior`, `SelectAllOnFocusBehavior`), the user interface still exhibits characteristics of standard WPF CRUD forms:
1. **Scattered Focus & Navigation Inconsistencies**: Enter navigation was applied via attached properties on control types without centralized handling of compound controls (e.g., open ComboBox popups, DatePicker popups, and DataGrid cell-to-cell traversal).
2. **Hard-coded Layout Metrics**: Margins, column widths, and padding were frequently declared as literal pixel values in individual XAML views rather than consuming semantic design tokens.
3. **Implicit Function Key Expectations**: Several dialogs declared `KeyBinding Key="F2"` or `Key="F5"` without a clear global taxonomy distinguishing global, screen-level, and transaction-level key bindings.
4. **Header/Footer Variance**: Dialogs used `AccountingDialogHeader` and `AccountingDialogFooter` but lacked a formalized Screen Contract defining initial focus, validation lifecycle, and status communication.

---

## 2. Detailed Component Review

### 2.1 App.xaml & Theme Loading
- **Current State**: `App.xaml` merges 9 separate resource dictionaries (`Colors.xaml`, `Typography.xaml`, `ButtonStyles.xaml`, `InputStyles.xaml`, etc.) and registers global value converters.
- **Identified Gap**: Missing a dedicated `Spacing.xaml` dictionary. View files had to guess margins (`Margin="0,0,0,8"`, `Padding="16"`). This led to minor visual misalignments between different dialogs.

### 2.2 Keyboard Navigation & Behaviors
- **`KeyboardNavigationBehavior.cs`**:
  - Attached property `AdvanceOnEnter` intercepted `KeyDown` for `Key.Enter` and called `MoveFocus`.
  - *Identified Gaps*:
    - ComboBox behavior: Pressing Enter while the dropdown list was open would sometimes trigger focus advance before the selection was committed.
    - DatePicker behavior: DatePicker input contains nested sub-controls (`DatePickerTextBox`); focus traversal was occasionally trapped inside the sub-control.
    - DataGrid integration: DataGrid was excluded from Enter navigation (`IsInsideDataGrid`), but within-grid cell navigation (Enter $\rightarrow$ next column/row) was unmanaged.
    - Multiline textboxes: Excluded single Enter, but had no standard hotkey (e.g. `Ctrl+Enter`) to complete and advance.

### 2.3 Dialog Lifecycle & Window Chrome
- **Current State**: Windows are standard WPF `Window` instances with `WindowStartupLocation="CenterOwner"`, `ShowInTaskbar="False"`, and custom `AccountingDialogHeader`/`AccountingDialogFooter`.
- **Identified Gaps**:
  - Lack of initial focus contract: Some dialogs left keyboard focus on the first input by default, while others required a mouse click to activate the first control.
  - Esc key handling: `IsCancel="True"` was wired on Cancel buttons, but unsaved dirty state was not checked before discarding inputs.

### 2.4 Data Presentation & Alignment
- **Current State**: Numerical values are formatted using `IndianCurrencyConverter` and right-aligned using `NumericValueStyle` or `NumericTextBoxStyle`.
- **Identified Gaps**:
  - Inconsistency between DataGrid column headers and content alignment: In some grids, numeric headers were left-aligned while cell contents were right-aligned.
  - Ledger list badge styling used hardcoded font sizes (`FontSize="10"`) rather than typography tokens.

---

## 3. UX Debt Summary & Remediation Strategy

| Area | Current Anti-Pattern | Required Accounting Standard |
| :--- | :--- | :--- |
| **Keyboard** | Ad-hoc `AdvanceOnEnter` | Deterministic centralized navigation with distinct control policies |
| **Function Keys** | Ad-hoc `KeyBinding` in window XAML | Strictly categorized: Global, Screen-Specific, Transaction-Specific, Reserved |
| **Spacing** | Literal margins (`8`, `12`, `16`) in views | Centralized token scale (`SpacingXS`, `SpacingSM`, `SpacingMD`, `SpacingLG`) |
| **Focus Indication** | Subtle border change | High-contrast 1.5px border (`#1D4ED8`) with soft background tint (`#F8FAFC`) |
| **Screen Contract** | Loose conventions per dialog | Formal documented contract (`UI_SCREEN_CONTRACT.md`) implemented consistently |

---

## 4. Assessment Conclusion
The Presentation layer is well-positioned for refactoring because it strictly adheres to MVVM (CommunityToolkit.Mvvm) and Clean Architecture. No business logic exists in code-behind files. All remediations can be completed cleanly at the XAML and behavior layer without touching application services or domain entities.
