# Phase 1.4.1.1 — Step 2: Centralized WPF UI Design System Report

## Executive Summary
Phase 1.4.1.1 Step 2 establishes the centralized native Windows accounting UI Design System for BillsEasy. The design system provides dense information layouts, high-contrast focus rings, rapid keyboard-first navigation (`Enter` field advancement, `F2` save, `Esc` cancel, `F5` refresh), and compact dialog standards inspired by traditional desktop accounting software.

---

## 1. Design System Structure
All theme files are centrally located under `src/BillsEasy.Presentation.Wpf/Themes/` and registered in `App.xaml`:
- **`Colors.xaml`**: Defines primary navy `#1E3A8A`, focus ring `#1D4ED8`, subtle surface colors, Dr/Cr balance colors (`#15803D` / `#B91C1C`), and system status badge styles.
- **`Typography.xaml`**: Segoe UI typographic scale (11pt base, 13pt headers) with numeric font variants.
- **`ButtonStyles.xaml`**: Dense 24–26px button styles (`PrimaryButtonStyle`, `DangerButtonStyle`, `ToolbarButtonStyle`).
- **`InputStyles.xaml`**: 24px `TextBox`, `NumericTextBoxStyle` (right-aligned), `ComboBox`, `CheckBox`, `RadioButton`, and `DatePicker`.
- **`DataGridStyles.xaml`**: Dense 24px row accounting DataGrid with header styling and selection brushes.
- **`TabControlStyles.xaml`**: Compact accounting tab control.
- **`DialogStyles.xaml`**: Standardized `GroupBox` and toolbar containers.
- **`MenuStyles.xaml` & `StatusBarStyles.xaml`**: Traditional desktop menu and status bar brushes.

---

## 2. Keyboard Navigation Framework
- **`KeyboardNavigationBehavior.cs`**:
  - `Enter` advances focus to the next logical input control.
  - `Shift+Enter` moves focus to the previous input control.
  - **Context-Aware Invariants**: Automatically ignores `Button` controls, multi-line `TextBox` (`AcceptsReturn=true`), and cells inside `DataGrid` (so data grids preserve native cell commit / row navigation).
- **`SelectAllOnFocusBehavior.cs`**:
  - Automatically selects all text on focus for rapid accounting data entry.
- **`Window.InputBindings`**:
  - `F2` mapped to `SaveCommand` on all edit dialogs.
  - `F5` mapped to `Refresh` / `Load` on management dialogs.

---

## 3. Reusable Controls & Converters
- **`AccountingDialogHeader`**: Standardized dialog header with title, subtitle, and right-aligned search/filter area.
- **`AccountingDialogFooter`**: Standardized footer with error message display, primary `F2` action, and `Esc` cancel button.
- **`AccountingFormatConverters.cs`**: Indian currency format (`₹ #,##,##0.00`), boolean/null visibility converters, and system badge decorators.

---

## 4. Architectural Boundaries Maintained
- **Domain Layer (`BillsEasy.Domain`)**: Unmodified.
- **Application Layer (`BillsEasy.Application`)**: Unmodified.
- **Infrastructure Layer (`BillsEasy.Infrastructure`)**: Unmodified.
- **SQLite Database Schema**: Unmodified.
- **No Voucher Engine Work**: Completely out of scope and frozen.

---

## 5. Quality Gate
- **Build Status**: `dotnet build BillsEasy.sln -c Release` -> **0 Errors, 0 Warnings**
- **Test Suite**: `dotnet test BillsEasy.sln -c Release` -> **99 / 99 Tests Passing (100%)**
