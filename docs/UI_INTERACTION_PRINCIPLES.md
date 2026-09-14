# BillsEasy — UI Interaction Principles
**Status**: Authoritative Reference  
**Phase**: 1.4.3 UX Foundation  
**Target Audience**: UI Engineers, Accounting Domain Reviewers  

---

## 1. Core Accounting UX Philosophy

BillsEasy is a specialized, native Windows desktop accounting system designed for high-speed, keyboard-driven data entry by professional accountants, bookkeepers, and business operators. It is **not** a generic web CRUD application ported to desktop.

### 1.1 The Accountant's Operating Reality
1. **Speed Over Mice**: Professional accountants rarely touch a mouse during day-to-day operations. Every action, navigation step, selection, submission, and cancellation must be achievable via predictable keystrokes.
2. **Deterministic Focus & Flow**: The cursor must never be "lost" or left in an ambiguous focus state. Every screen must have a deterministic initial focus element, and field progression must follow the logical sequence of Indian accounting vouchers and master forms.
3. **High-Density, High-Legibility**: Accounting documents present large amounts of structured tabular and transactional data simultaneously. Space wasted on oversized margins, bulky touch controls, or excessive whitespace slows down cognitive processing and requires unnecessary scrolling. High density must remain crisp, well-aligned, and visually comfortable.
4. **Immediate & Unambiguous Feedback**: Data validation errors, status messages, Dr/Cr balance polarity, and active system modes (e.g., Company Active, FY Active) must be instantly discernible through color, contrast, and dedicated status panes.

---

## 2. Global Interaction Principles

### 2.1 The Two-Mode Navigation Paradigm
BillsEasy separates navigation into two primary modes:

| Mode | Context | Navigation Mechanism | Action Keys |
| :--- | :--- | :--- | :--- |
| **Form / Data Entry** | Master editing dialogs, transaction header entry, filter dialogs | `Enter` advances forward; `Shift+Enter` retreats backward; `Tab` / `Shift+Tab` standard fallback | `F2` = Save, `Esc` = Cancel / Dismiss, `Ctrl+Enter` = multiline newline / fast commit |
| **Tabular / Grid** | Voucher lines, ledger list grids, account group tree grids | Arrow keys (`Up`/`Down`/`Left`/`Right`), `Tab` / `Shift+Tab` across editable cells | `Enter` commits cell and moves to next cell or next row; `F7` = Delete line; `Insert` = Insert line |

### 2.2 Reversible Actions & Explicit Confirmation
- Destructive actions (deleting an active ledger, removing a company, cancelling an uncommitted voucher) must require explicit confirmation with `Enter` defaulted to **Cancel / Safe Option** to avoid accidental data loss.
- Non-destructive dismissal (closing a clean view or list) with `Esc` closes immediately. If changes have been made and are uncommitted, `Esc` presents an unsaved changes confirmation dialog.

### 2.3 Visual Polarity & Financial Indicators
- **Debit (Dr)**: Represented as standard neutral/primary text or with explicit `Dr` suffix.
- **Credit (Cr)**: Explicitly identified with `Cr` suffix; negative/adverse balance states leverage warning/caution tones without visual garishness.
- **Amounts**: Right-aligned monospace/tabular numeric rendering. Every monetary figure is formatted to 2 decimal places with standard Indian numbering separators (`₹ #,##,##0.00`).

---

## 3. Screen Lifecycle & State Rules

1. **Activation / Open**:
   - The primary input field is focused automatically (e.g., `CompanyNameTextBox`, `GroupNameTextBox`, `LedgerNameTextBox`).
   - The entire text of the initial field is pre-selected for fast overwrite if editing, or positioned at index 0 if blank.
2. **Dirty State Tracking**:
   - Master edit screens track pristine vs. modified state.
   - Closing an untouched dialog via `Esc` dismisses immediately without prompts.
   - Closing a dirty dialog via `Esc` warns the user: "Discard unsaved changes? [Yes / No]".
3. **Execution / Save (`F2`)**:
   - Pressing `F2` triggers immediate validation.
   - If invalid, focus jumps directly to the first offending control with its validation tooltip / error border highlighted.
   - If valid, the command executes asynchronously, displays progress, and dismisses on success or reports error in the dialog footer.
