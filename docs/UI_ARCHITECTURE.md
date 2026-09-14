# BillsEasy — UI Architecture & Keyboard UX Design Specification

**UI Platform:** Windows Presentation Foundation (WPF on .NET 8.0)  
**MVVM Framework:** CommunityToolkit.Mvvm  
**Philosophy:** Keyboard-First, High-Density Data Entry, Instant Autocomplete, Sub-50ms Response Time.

---

## 1. MVVM Architecture & Component Structure

`
BillsEasy.Presentation.Wpf/
+-- App.xaml / App.xaml.cs          # Application Lifecycle & Global Exception Handler
+-- Styles/
¦   +-- Colors.xaml                 # Modern High-Contrast Accounting Palette
¦   +-- Typography.xaml             # Monospace tabular numerals, clean Segoe UI
¦   +-- DataGridStyles.xaml         # Dense, non-padded, keyboard-selectable grid styling
+-- Controls/
¦   +-- FocusBehavior.cs            # Enter-to-Next-Field attached property
¦   +-- LedgerSearchBox.xaml        # Search-as-you-type fuzzy ledger picker
¦   +-- MoneyInputBox.xaml          # Auto-formatting decimal box (e.g., ?1,25,000.00)
¦   +-- DatePickerBox.xaml          # Fast date entry (e.g., typing '1209' -> '12/09/2026')
¦   +-- BillAllocationDialog.xaml   # Modal popup for distributing payment across pending bills
+-- Views/
¦   +-- ShellView.xaml              # Top menu, status bar (Company/FY), workspace container
¦   +-- Company/                    # Company creation, selection, configuration views
¦   +-- Masters/                    # Ledger list, Ledger edit, Account group views
¦   +-- Vouchers/                   # Payment voucher, Receipt voucher, Journal entry views
¦   +-- Reports/                    # Ledger register, Outstanding receivables/payables
+-- ViewModels/
    +-- ViewModelBase.cs            # CommunityToolkit.Mvvm ObservableObject base
    +-- ShellViewModel.cs
    +-- Company/...
    +-- Masters/...
    +-- Vouchers/...
    +-- Reports/...
`

---

## 2. Keyboard Interaction & Navigation Engine

Accounting professionals enter hundreds of vouchers daily without touching a mouse. The application enforces a strict keyboard contract:

### 2.1 The Enter-Key Contract
* Pressing Enter on any input control validates the current field and shifts keyboard focus immediately to the next logical control in the tab sequence (FocusBehavior.AdvancesOnEnter = true).
* Pressing Shift+Enter returns focus to the previous control.
* Pressing Enter on the final save confirmation button commits the voucher atomically and resets the form for the next transaction.

### 2.2 The Escape (Esc) Contract
* In a modal dialog (e.g. Bill Allocation popup): Esc cancels the popup and returns to the parent field.
* In an active voucher screen: Esc prompts to abandon changes if dirty, or exits to the main dashboard if unchanged.

### 2.3 Universal Accounting Hotkeys
| Key Combination | Scope | Action |
| :--- | :--- | :--- |
| Alt+C | Within any Ledger/Item picker | Opens the \"Create Master\" dialog on the fly, auto-selecting the created master upon closing. |
| Alt+A | Voucher Line Grid | Appends a new debit/credit entry row. |
| Alt+D | Voucher Line Grid | Deletes the currently highlighted entry row. |
| F2 | Global / Voucher Entry | Focuses the Voucher Date picker for rapid date modification. |
| Ctrl+A | Active Form | Fast Save / Commit Voucher. |
| F5 | Any Register / Report | Refreshes statement data and recalculates balances. |

---

## 3. Reusable Custom Accounting Controls

1. **LedgerSearchBox:**
   - Dropdown with fuzzy text search over Ledger Name, Print Name, Alias, and Group Name.
   - Displays real-time current balance (e.g. ?45,000.00 Dr) alongside ledger name during selection.
2. **MoneyInputBox:**
   - Standardized decimal formatting with automatic thousand separators in Indian numbering format (e.g. 12,34,567.89).
   - Rejects non-numeric characters; supports inline calculator arithmetic (e.g. typing 5000+250 automatically computes 5250).
3. **BillAllocationGrid:**
   - Auto-populates all pending bills for the selected party ledger with columns: Bill No, Date, Original Amt, Pending Amt, Allocated Amt.
   - Offers an Auto-Allocate (FIFO) button/shortcut to automatically settle oldest bills first.

---

## 4. Input Validation & Error Handling

- ViewModels utilize IDataErrorInfo / INotifyDataErrorInfo integrated with FluentValidation.
- Validation errors are presented inline with red focus rings and tooltip explanations rather than blocking Windows Message Boxes, preserving operator data-entry rhythm.
- Critical double-entry imbalances ( \neq Credits$) visually highlight the discrepancy in red in the footer summary bar and disable the final save action.
