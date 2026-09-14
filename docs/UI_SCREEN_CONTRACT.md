# BillsEasy — UI Screen Contract
**Status**: Authoritative Architectural Contract  
**Phase**: 1.4.3 UX Foundation  
**Target Audience**: Presentation Layer Developers  

---

## 1. Overview & Purpose

Every user interface screen in BillsEasy must adhere to a strict structural and behavioral contract. This guarantees that accountants experience 100% predictable keyboard navigation, uniform visual density, standardized error presentation, and consistent action keys across all modules (Masters, Transactions, Reports, Settings).

---

## 2. Standard Screen Archetypes

BillsEasy defines four standard screen archetypes:

```
+-------------------------------------------------------------------------+
| Archetype 1: Master Edit Dialog (Modal, fixed/auto height, ~520-650px)   |
| Archetype 2: Master Management / List (Modal/Modeless, 800x600px min)   |
| Archetype 3: Transaction Entry (Modeless/Maximized, High-Density Grid)   |
| Archetype 4: Accounting Report View (Tabular Grid with Drill-Down)       |
+-------------------------------------------------------------------------+
```

---

## 3. Structural Zone Contract

Every screen must contain three well-defined zones:

```
+-------------------------------------------------------------------------+
| ZONE 1: HEADER (AccountingDialogHeader)                                 |
| - Title & Subtitle / Context                                            |
| - Active Company & Active Financial Year                                |
| - Entity Mode (Create New / Edit [Name])                                |
+-------------------------------------------------------------------------+
| ZONE 2: WORKSPACE / BODY                                                |
| - Master Edit: 2-column or 4-column structured form layout              |
| - Management List: Filter toolbar + High-density DataGrid               |
| - Transaction: Header info strip + Grid lines + Summary / Tax block     |
+-------------------------------------------------------------------------+
| ZONE 3: FOOTER (AccountingDialogFooter / AccountingStatusBar)           |
| - Keyboard Shortcut Legend (e.g. [F2] Save  [Esc] Cancel)               |
| - Error / Validation / Status message strip                             |
| - Explicit Action Buttons (Primary = Accent, Secondary = Neutral)       |
+-------------------------------------------------------------------------+
```

---

## 4. Contract Rules & Invariants

### 4.1 Initial Focus Invariant
- **Rule**: Every screen **MUST** set keyboard focus to its primary operational field upon rendering (`Loaded` event).
- **Master Edit**: Focus the first data input field (e.g., `CompanyName`, `GroupName`, `LedgerName`). Focus must NOT land on Cancel or Header buttons.
- **Master List**: Focus either the Search / Filter field, or the first row of the DataGrid.
- **Transaction**: Focus the first header input field (e.g., `VoucherDate`, `VoucherNumber`, `Series`).

### 4.2 Deterministic Tab & Enter Sequence
- Visual top-to-bottom, left-to-right reading order must match the `TabIndex` sequence.
- Skipping fields or jumping irregularly across column boundaries is strictly forbidden.
- Read-only or calculated display fields (e.g., `GSTIN Verification Status`, `Net Balance`) must have `IsTabStop="False"`.

### 4.3 Action Keys Standard
- **Save / Commit**: `F2` (always bound at window level to the Primary Submit Command).
- **Cancel / Close**: `Esc` (always bound at window level).
  - If screen state is unmodified: Closes immediately without prompt.
  - If screen state is dirty: Prompts "Discard changes? [Yes / No]".
- **Delete Record**: `Alt+D` or `Ctrl+Delete` with modal safety confirmation.

### 4.4 Validation Presentation Invariant
- Validation occurs on field exit (`LostFocus`) and on submission (`F2`).
- When a save action fails validation:
  1. The footer error banner activates, displaying the specific validation summary.
  2. Keyboard focus is automatically moved to the first invalid control.
  3. The invalid control displays the error border and tooltip.

### 4.5 Destructive Action Confirmation Invariant
- Every deletion or permanent cancellation must present an explicit modal confirmation.
- The confirmation dialog must have **Cancel (No)** focused by default.
- Pressing `Enter` in the confirmation dialog without explicitly arrowing to `Yes` must result in safe cancellation.
