# BillsEasy — UI Constitution
**Status**: Authoritative & Permanent UI Design Authority  
**Effective Date**: Phase 1.4.5 (Post-Remediation Freeze)  
**Target Audience**: Presentation Layer Developers, UX Engineers, Domain Reviewers  

---

## 1. Vision & Identity Statement

> **"Native Indian Accounting Workflow + Modern Professional Windows Desktop UX"**

BillsEasy is a dedicated, high-speed Windows desktop accounting application built for professional Indian accountants, tax practitioners, and business operators. It is **not** a web SaaS dashboard, a mobile port, or a generic CRUD admin tool.

### BillsEasy MUST Always Feel:
- **Fast**: Zero lag, zero decorative animations, instant keystroke processing.
- **Operational**: Designed for 8–10 hours of continuous data entry without cognitive fatigue.
- **Keyboard-First**: Every primary workflow is achievable without touching the mouse.
- **Information-Dense**: High data-to-pixel ratio, compact tabular rows, aligned fields.
- **Predictable**: Focus, tab stops, action keys, and error states follow universal rules.
- **Trustworthy**: Financial numbers, balance polarities, and validation rules are unambiguous.
- **Desktop-Native**: Crisp 1px borders, rectangular controls, standard Windows shortcuts.

### BillsEasy MUST NEVER Feel:
- Like a card-heavy modern web dashboard.
- Like a marketing SaaS landing page with oversized fonts and empty whitespace.
- Like a mobile application with pill-shaped buttons and oversized touch targets.
- Overly spacious, floaty, or decorative.
- Sluggish or animation-heavy.

---

## 2. Core UX Principles (The 15 Laws of BillsEasy UX)

### Principle 1 — Operational Speed Over Decoration
Priority order for all UI decisions:
1. Accuracy & Financial Integrity
2. Keyboard Data Entry Efficiency
3. Clarity & Visual Hierarchy
4. Information Density
5. Discoverability & Shortcut Cues
6. Visual Polish

*Rule*: Never sacrifice accounting data-entry speed merely to make a screen "prettier" or more spacious.

---

### Principle 2 — Keyboard First
Every operational screen must be fully operable from the keyboard. The standard keyboard mapping is universal across the application:

| Key | Context | Standard Action |
| :--- | :--- | :--- |
| `Esc` | Universal | Cancel / Close current dialog or dismiss view |
| `Enter` | Form / Data Entry | Validate current field and advance to next operational field |
| `Shift+Enter` | Form / Data Entry | Retreat focus to previous operational field |
| `Ctrl+Enter` | Multiline TextBox | Advance focus to next operational field |
| `F2` | Master / Transaction | Save / Commit record |
| `F3` | Transaction Grid | Lookup / In-grid master selection (Phase 1.5+) |
| `F5` | Management / List | Refresh / Reload data from database |
| `Insert` | DataGrid | Add / Insert new line above current row (Phase 1.5+) |
| `Delete` / `Alt+D` | Record / Line | Delete selected record or transaction line (with confirmation) |
| `Alt+mnemonic` | Universal | Direct access to primary menus, buttons, and views |

*Rule*: Never introduce arbitrary or conflicting shortcut bindings on individual screens.

---

### Principle 3 — Focus Must Always Be Obvious
The operator must instantly answer at a glance: **"Where will my next keystroke go?"**
- Focused inputs MUST have an unmistakable visual state: a crisp 1.5px deep navy border (`#1E3A8A`) and an iconic soft accounting ledger yellow tint (`#FFFEE6`).
- Never hide focus indicators. Never rely solely on default faint Windows focus rectangles.
- Never use pulsing or fading focus animations.

---

### Principle 4 — Accounting Density (Dense But Readable, Never Cramped)
Accounting users process thousands of transactions and ledger accounts. Space wasted on oversized margins forces unnecessary scrolling.
- Use compact control heights (24px for inputs and buttons, 22px for DataGrid rows).
- Form fields must use horizontal label-input alignments (`[Label: 160px] [Input]`).
- Whitespace must be intentional and functional: used between distinct accounting sections, never as blank emptiness inside forms.

---

### Principle 5 — Numbers Are Special
Financial numbers convey economic reality and must visually behave differently from plain text:
- **Monetary Values**: Strictly right-aligned, formatted to 2 decimal places with standard Indian numbering grouping (`₹ #,##,##0.00`), using tabular monospace fonts (`Consolas`).
- **Dr / Cr Indicators**: Explicitly shown in adjacent columns or suffixes; color-coded with deep forest green for Debit (`#166534`) and deep crimson for Credit (`#991B1B`).
- **Quantities**: Right-aligned.
- **Codes & GSTIN**: Fixed-width, monospace or regular font, left-aligned.
- *Rule*: Never center monetary values merely for visual symmetry.

---

### Principle 6 — Consistent Screen Contract
Every major operational screen must adhere to a standardized 3-zone structure:
```
┌────────────────────────────────────────────────────────────┐
│ ZONE 1: HEADER (AccountingDialogHeader)                    │
│ - Brand & Company / Financial Year Context                 │
│ - Screen Title & Entity Mode Badge ([New], [Edit])         │
│ - Short contextual subtitle                                │
├────────────────────────────────────────────────────────────┤
│ ZONE 2: BODY (Structured Form or Accounting DataGrid)      │
│ - Aligned horizontal fields or compact grid lines          │
│ - Clean AccountingSection hairline rules                   │
├────────────────────────────────────────────────────────────┤
│ ZONE 3: FOOTER (AccountingDialogFooter)                    │
│ - High-contrast alert banner (for validation errors)       │
│ - Permanent Shortcut Bar: [F2] Save • [Esc] Cancel • ...   │
│ - Primary [F2] Save & [Esc] Cancel action buttons          │
└────────────────────────────────────────────────────────────┘
```

---

### Principle 7 — Context Is Critical
An accountant must never wonder which company or financial year they are modifying:
- The active Company Name and active Financial Year date bounds (`01-Apr-2026 → 31-Mar-2027`) must be continuously visible in the top context strip of `MainWindow` and in the header of child dialogs.
- Do not clutter individual form bodies with redundant company selector inputs when operating inside an active company context.

---

### Principle 8 — Windows Desktop First
BillsEasy is built on Windows WPF/.NET 8:
- Prefer rectangular controls (0px to 1px corner radii), 1px borders (`#CBD5E1`), native menus, toolbars, grids, and dialogs.
- Reject web SaaS design trends: no floating cards, no drop shadows, no giant pill buttons, no hero banners, and no decorative gradients.

---

### Principle 9 — Don't Blindly Copy Tally or BUSY
Learn from traditional software's strengths (keyboard speed, extreme density, master-first workflow, voucher flow), but do not replicate their legacy DOS-era flaws (unclear hierarchies, missing field labels, obscure non-standard key sequences).
- Combine **traditional Indian accounting efficiency** with **modern visual hierarchy** and **professional desktop usability**.

---

### Principle 10 — Reusability Is Mandatory
Never solve a layout or styling problem with ad-hoc code on individual screens:
- All screens MUST consume centralized tokens from `Themes/Colors.xaml`, `Themes/Spacing.xaml`, `Themes/Typography.xaml`, and `Themes/InputStyles.xaml`.
- All screens MUST use reusable components: `AccountingSection`, `AccountingDialogHeader`, `AccountingDialogFooter`, and `AccountingStatusBar`.

---

### Principle 11 — Accessibility & Error Prevention
- Validation errors must display immediately on submission in the dedicated footer banner (`⚠ Error message`) and focus must jump automatically to the first invalid field.
- Destructive actions (e.g. deleting a company or ledger) require explicit confirmation dialogs with default focus on **Cancel**.
- System-protected records (e.g., Default System Groups / System Ledgers) must display clear informational badges indicating protected status.

---

### Principle 12 — No Premature Features
Never add UI fields (such as Currency Sub-String, Print Fonts, In-Grid Master Creation, Round-off configs) merely because reference software displays them.
- Every field must map to an approved domain/application model entity.
- Deferred fields are tracked in `docs/UI_TODO.md` and await their respective phases.
- **Visible Representation of Frozen Operations**: Future functionality (such as Phase 1.5 Voucher shortcuts: Payment F5, Receipt F6, Sales F8, Purchase F9) may be visibly represented in the UI when clearly marked as frozen, locked, or unavailable (`[LOCKED]` / `🔒`), ensuring operators understand the planned workflow and reserved shortcuts. However, **no implementation, entity, command, or screen may exist prior to its authorized phase.**

---

### Principle 13 — Screen Size Must Match Workload
- Compact dialogs for simple entities (380–520px height, 540–740px width).
- Generous width for forms containing multi-column inputs (e.g., Company Master with 720px width) to guarantee horizontal field pairs never wrap awkwardly or clip.
- Workstations and grids should utilize available display space effectively without forcing unnecessary scrollbars.

---

### Principle 14 — Functional Visual Hierarchy
Visual emphasis must carry operational meaning:
- **Strongest Emphasis**: Active input field (`#FFFEE6` yellow tint + navy border), selected grid row (`#DCEBF7`), primary save button (`PrimaryBrandBrush`), validation error banner (`#FEE2E2`).
- **Subtle Emphasis**: Field labels, section headers, grid headers, read-only display fields.
- **De-emphasized**: Helper text, status icons, disabled buttons.

---

### Principle 15 — No UI Magic Numbers
All padding, margin, width, height, and color values must derive from centralized design tokens in `Themes/`. No arbitrary numbers (e.g., `Height="27"`, `Margin="13,7,3,9"`) are permitted in view XAML.

---

## 3. Accounting-Specific Form & Grid Rules

### Master Entry Forms
1. Standard section sequence:
   - `IDENTIFICATION` (Name, Print Name, Code/Alias)
   - `CLASSIFICATION` (Parent Group, Nature)
   - `STATUTORY & TAX` (State, GSTIN, PAN, Aadhaar)
   - `ACCOUNTING & BALANCE` (Opening Balance, Dr/Cr, Credit Terms)
   - `CONTACT & BANKING` (Person, Phone, Bank details)
2. Left label column width standardized to `{StaticResource FormLabelWidth}` (160px).
3. Section dividers rendered via `<controls:AccountingSection Title="..." />`.

### Master List & Directory Screens
1. Header contains search box with real-time filter.
2. Toolbar features `[Alt+A] Add`, `[Alt+M] Modify`, `[Alt+D] Delete`, `[F5] Refresh`.
3. Pressing `Enter` on a selected DataGrid row opens that record for editing.
4. Active row highlighted in `#DCEBF7` with bold primary text.

### DataGrids
1. Compact 22px row height, 24px header height.
2. Alternating row background (`#F8FAFC`).
3. Visible 1px gridlines (`#E2E8F0`).
4. Tabular monospace font for numeric columns, right-aligned with Dr/Cr indicators in dedicated adjacent columns.

---

## 4. Future Screen Certification Checklist

Before any future screen (including Phase 1.5 Voucher Engine screens) is certified, verify:
- [ ] Conforms strictly to the 15 Principles of the BillsEasy UI Constitution.
- [ ] Keyboard navigation is 100% deterministic (Enter advances, Shift+Enter retreats, Esc closes, F2 saves).
- [ ] Initial focus lands on the primary operational input on `Loaded`.
- [ ] Monetary columns are right-aligned monospace with 2 decimal precision.
- [ ] Active focus is unmistakable with `#FFFEE6` background tint and 1.5px navy border.
- [ ] Employs `AccountingDialogHeader` and `AccountingDialogFooter`.
- [ ] Zero hardcoded magic pixel numbers; all dimensions derive from `Themes/Spacing.xaml`.
- [ ] Does NOT expose or introduce unapproved domain/application fields.
- [ ] Looks and behaves like a serious native Windows desktop accounting application.
