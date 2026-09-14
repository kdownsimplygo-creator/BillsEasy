# Phase 1.4.1 — UI Architecture & Design System Assessment

**Milestone:** Phase 1.4.1 (BillsEasy Native Accounting UI Foundation)  
**Status:** Inspection & Assessment Complete — Awaiting Approval to Implement  
**Date:** 2026-09-13  

---

## 1. Executive Summary & Design Vision
The objective of Phase 1.4.1 is to establish a high-performance, compact, keyboard-first native desktop UI foundation for BillsEasy prior to implementing the Phase 1.5 Voucher Engine.

> **Design Philosophy:** *"Modern engineering underneath, traditional native Indian accounting desktop experience on top."*

The application must feel like a dedicated Windows accounting powerhouse (reminiscent of the speed, density, and keyboard fluency of BUSY and Tally) without copying any proprietary artwork or branding.

---

## 2. Inspection of Existing Presentation Layer

### Current Structure:
- **Presentation Project:** `src/BillsEasy.Presentation.Wpf` (.NET 8.0, WPF, `CommunityToolkit.Mvvm`).
- **Views (Dialogs):**
  - Shell: `MainWindow.xaml`
  - Company: `CompanyListDialog.xaml`, `CompanyEditDialog.xaml`, `FinancialYearManagementDialog.xaml`
  - Masters: `AccountGroupManagementDialog.xaml`, `AccountGroupEditDialog.xaml`, `LedgerManagementDialog.xaml`, `LedgerEditDialog.xaml`

### Shortcomings & Technical Debt Identified:
1. **Scattered Inline Styling:** Every dialog explicitly sets heights (`Height="26"`), margins (`Margin="0,0,0,3"`), colors (`Foreground="#64748B"`), and paddings. There is no centralized design system.
2. **Mouse-Dependent Navigation:** Standard WPF requires the `Tab` key to move between fields. Traditional accounting operators expect the `Enter` key to advance to the next field and `Shift+Enter` or `Esc` to go back.
3. **Lack of Accounting-Specific Control Styles:**
   - TextBoxes lack distinct high-contrast active focus rings.
   - Numeric and amount fields are left-aligned instead of right-aligned with currency/decimal formatting.
   - Date inputs rely on standard WPF DatePicker without fast numeric text entry ergonomics.
4. **DataGrid Density & Selection:** DataGrids use standard WPF padding and row heights rather than compact 22-24px accounting grid density.
5. **No Shared Dialog Chrome/Frame:** Dialogs duplicate header labels, status/error banners, and footer button bars.

---

## 3. Proposed UI Foundation Architecture

### Directory & Resource Organization:
```
src/BillsEasy.Presentation.Wpf/
├── Themes/
│   ├── Colors.xaml               (Accounting Palette: Deep Slate, Navy, Focus Gold/Blue, Crisp Borders)
│   └── Typography.xaml           (Dense, legible desktop typography: 11pt body, 12pt bold headers)
├── Styles/
│   ├── ButtonStyles.xaml         (Primary F2/Save, Danger, Secondary, Toolbar action buttons)
│   ├── InputStyles.xaml          (Compact TextBoxes, ComboBoxes, DatePickers with high-contrast focus)
│   ├── DataGridStyles.xaml       (Dense rows, distinct header, row selection highlight)
│   └── TabControlStyles.xaml     (Compact accounting master tabs)
├── Behaviors/
│   ├── KeyboardNavigationBehavior.cs (Enter key moves to next control, Shift+Enter to previous)
│   └── SelectAllOnFocusBehavior.cs   (Selects all text on focus for rapid overtyping)
├── Converters/
│   └── AccountingFormatConverters.cs (Indian currency formatting ₹ ##,##,##0.00, Dr/Cr badge colors)
├── Controls/
│   ├── AccountingDialogHeader.xaml   (Standardized master/voucher dialog header with company context)
│   └── AccountingDialogFooter.xaml   (Standardized error banner and Save/Cancel button strip)
```

---

## 4. Keyboard-First Specification

1. **Enter Key Advance (`KeyboardNavigationBehavior.AdvanceOnEnter`):**
   - Pressing `Enter` on any input field (TextBox, ComboBox, DatePicker, CheckBox) automatically moves focus to the next logical focusable control in the tab sequence.
   - Pressing `Shift+Enter` moves focus to the previous control.
   - When focused on a `Button`, `Enter` triggers the button command rather than advancing.
2. **Select All on Focus (`SelectAllOnFocusBehavior`):**
   - Entering any amount or text field selects all existing text so the user can immediately type new values without pressing backspace.
3. **Standard Accounting Function Keys & Mnemonic Shortcuts:**
   - `F2` / `Alt+S`: Save / Commit form.
   - `Esc`: Cancel / Close dialog.
   - `F5`: Refresh data.
   - `Alt+N` / `Alt+A`: Add new record.
   - `Alt+M` / `F3`: Modify selected record.
   - `Alt+D` / `Delete`: Delete record (with domain guard prompt).

---

## 5. Scope Boundaries & File Impact

### Files to Add:
1. `Themes/Colors.xaml`
2. `Themes/Typography.xaml`
3. `Styles/ButtonStyles.xaml`
4. `Styles/InputStyles.xaml`
5. `Styles/DataGridStyles.xaml`
6. `Styles/TabControlStyles.xaml`
7. `Behaviors/KeyboardNavigationBehavior.cs`
8. `Behaviors/SelectAllOnFocusBehavior.cs`
9. `Converters/AccountingFormatConverters.cs`
10. `Controls/AccountingDialogHeader.xaml` & `.xaml.cs`
11. `Controls/AccountingDialogFooter.xaml` & `.xaml.cs`

### Files to Modify (UI Styling & Behavior Integration):
1. `App.xaml` (Register all shared resource dictionaries)
2. `MainWindow.xaml` (Accounting application shell & status bar)
3. `Views/Company/CompanyListDialog.xaml`
4. `Views/Company/CompanyEditDialog.xaml`
5. `Views/Company/FinancialYearManagementDialog.xaml`
6. `Views/Masters/AccountGroupManagementDialog.xaml`
7. `Views/Masters/AccountGroupEditDialog.xaml`
8. `Views/Masters/LedgerManagementDialog.xaml`
9. `Views/Masters/LedgerEditDialog.xaml`

### Files That Must NOT Be Touched:
- **All Domain code (`src/BillsEasy.Domain/`)** — Strictly locked.
- **All Application interfaces & business services (`src/BillsEasy.Application/`)** — Strictly locked.
- **All Persistence repositories & SQLite schema (`src/BillsEasy.Infrastructure/`)** — Strictly locked.
- Zero changes to database tables, foreign keys, or domain invariants.

---

## 6. Readiness & Next Steps
Upon user approval of this assessment, implementation will proceed systematically through Steps 2 to 11.
