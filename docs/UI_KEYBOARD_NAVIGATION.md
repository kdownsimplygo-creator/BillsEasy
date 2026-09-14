# BillsEasy — Keyboard Navigation Specification
**Status**: Authoritative Technical Specification  
**Phase**: 1.4.3 UX Foundation  
**Target Audience**: Presentation Layer Developers  

---

## 1. Function Key Classification Taxonomy

To prevent conflicting shortcut bindings across different screens and transaction forms, all function keys in BillsEasy are strictly partitioned into four distinct operational tiers:

```
+-------------------------------------------------------------------------+
| TIER 1: GLOBAL (Universally bound on every window/dialog)               |
| - Esc       : Cancel current operation, dismiss modal, exit view        |
| - F1        : Context-sensitive Help & Shortcut Guide                   |
+-------------------------------------------------------------------------+
| TIER 2: SCREEN-SPECIFIC (Master Management & Edit screens)              |
| - F2        : Save / Commit changes (Master Edit Dialogs)               |
| - F5        : Refresh list / Reload view (Management screens)          |
| - Alt+A     : Add / Create new record                                   |
| - Alt+M     : Modify / Edit selected record                             |
| - Alt+D     : Delete selected record (with confirmation)                |
| - Alt+X     : Export grid contents                                      |
+-------------------------------------------------------------------------+
| TIER 3: TRANSACTION-SPECIFIC (Voucher Entry — Phase 1.5+)               |
| - F2        : Save voucher                                              |
| - F7        : Delete current grid line                                  |
| - Insert    : Insert line above current row                             |
| - Ctrl+D    : Duplicate voucher / line                                  |
+-------------------------------------------------------------------------+
| TIER 4: RESERVED FOR FUTURE (Tally/BUSY Traditional Fast-Keys)           |
| - F4        : Contra Voucher                                            |
| - F5        : Payment Voucher (in transaction context)                  |
| - F6        : Receipt Voucher                                           |
| - F7        : Journal Voucher (in voucher type context)                 |
| - F8        : Sales Voucher                                             |
+-------------------------------------------------------------------------+
```

> [!NOTE]
> **Visible Representation of Frozen Phase 1.5 Keys**: While F5 (Payment), F6 (Receipt), F8 (Sales), and F9 (Purchase) are visibly displayed with lock indicators (`🔒`) in MainWindow menus and gateway panels to communicate the planned accounting workflow, they are strictly non-functional, perform no actions, and execute no commands until Phase 1.5 is formally authorized.

---

## 2. Deterministic Field Traversal (Enter / Shift+Enter)

BillsEasy uses a centralized, deterministic keyboard navigation engine (`KeyboardNavigationBehavior`) applied at the form container or window level. Individual controls do NOT implement custom or ad-hoc `KeyDown` event handlers for form traversal.

### 2.1 Control-Specific Enter Behaviors

| Control Type | Key Combination | Action Taken |
| :--- | :--- | :--- |
| **Single-Line TextBox** | `Enter` | Validates current content, commits binding, advances focus to the next logical focusable element. |
| **Single-Line TextBox** | `Shift+Enter` | Retreats focus to the previous logical focusable element. |
| **Multiline TextBox** | `Enter` | Inserts a standard newline character into the text box (standard editing). |
| **Multiline TextBox** | `Ctrl+Enter` | Commits binding and advances focus to the next logical focusable element. |
| **ComboBox (Dropdown Closed)** | `Enter` | Commits current selection and advances focus to next logical element. |
| **ComboBox (Dropdown Open)** | `Enter` | Commits highlighted item, closes dropdown popup, and **remains focused** on ComboBox. Next `Enter` advances. |
| **DatePicker / DateInput** | `Enter` | Parses date text, commits binding, closes calendar if open, and advances focus to next logical element. |
| **Numeric / Currency Input** | `Enter` | Formats number to standard decimal string, commits binding, advances focus. |
| **Search / Suggestion Box** | `Enter` | Selects suggested item, closes suggestion box, advances focus. |
| **DataGrid (Cell Edit Mode)** | `Enter` | Commits cell editing, advances to the next cell in the row (or next row if at end of row). Does **NOT** escape the grid. |
| **Button / Action Control** | `Enter` | Invokes the button's Command / Click action. Does **NOT** advance focus automatically. |

### 2.2 Reversal Traversal (Shift+Enter / Shift+Tab)
- `Shift+Enter` and `Shift+Tab` always move focus backwards along the visual `TabIndex` chain across all single-line inputs and closed ComboBoxes.
- In a DataGrid, `Shift+Tab` or `Shift+Enter` moves to the preceding editable cell or previous row.

---

## 3. Keyboard Invariant Verification Checklist

Before certifying any view in BillsEasy, test the following sequence:
1. Open dialog $\rightarrow$ Cursor is immediately in Field 1.
2. Type input $\rightarrow$ Press `Enter` $\rightarrow$ Cursor advances to Field 2.
3. Open ComboBox with `Alt+Down` or typing $\rightarrow$ Arrow down $\rightarrow$ Press `Enter` $\rightarrow$ Popup closes, focus remains on ComboBox $\rightarrow$ Press `Enter` again $\rightarrow$ Advances to Field 3.
4. Reach Multiline Remarks $\rightarrow$ Type note $\rightarrow$ Press `Enter` (creates new line) $\rightarrow$ Press `Ctrl+Enter` $\rightarrow$ Advances to Save Button.
5. Press `F2` from anywhere in the form $\rightarrow$ Triggers Save directly without requiring manual tabbing to the Save button.
6. Press `Esc` from anywhere $\rightarrow$ Cancels / Closes the form safely.
