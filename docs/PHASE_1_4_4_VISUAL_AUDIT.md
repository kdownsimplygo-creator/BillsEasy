# BillsEasy — Phase 1.4.4 Visual Audit & Forensic UX Inspection
**Date**: 2026-09-13  
**Status**: Authoritative Architectural Audit  
**Phase**: 1.4.4 Native Accounting Visual Redesign v2  
**Scope**: Presentation.Wpf layer inspection across all views, controls, and themes  

---

## 1. Executive Summary

A comprehensive visual inspection of the BillsEasy presentation layer reveals that while Phase 1.4.3 established underlying XAML resource dictionaries, the screens still visually resemble a **generic modern WPF CRUD application** rather than a **serious, native desktop Indian accounting application**.

In established Indian accounting ERPs (BUSY, Tally, Marg), forms and grids are engineered for extreme keyboard speed, high information density, horizontal field alignment, and immediate visual orientation. The current BillsEasy UI suffers from several fundamental visual deficiencies detailed below.

---

## 2. Forensic Findings Across Screens

### 2.1 MainWindow
- **The Problem**: Resembles an empty web dashboard or placeholder landing page. The central workspace contains an oversized centered title (`BillsEasy - 30pt`), large spacing, and two oversized SaaS-style buttons (`Create New Company`, `Open Existing Company`).
- **Accounting Defect**: An accountant working 8-10 hours a day requires an operational cockpit. The main window should continuously present:
  - Immediate visibility of the active accounting environment (Company, Financial Year range, Books Beginning date, GSTIN, State code).
  - Quick keyboard-driven navigation menu and master launchpad (Masters, Ledgers, Financial Years, Transactions).
  - A permanent bottom shortcut bar showing active function keys (`[F2] Save`, `[F3] Lookup`, `[F5] Refresh`, `[Esc] Close`).

### 2.2 Form Architecture & GroupBoxes (`CompanyEditDialog`, `AccountGroupEditDialog`, `LedgerEditDialog`)
- **The Problem**: Forms use standard WPF `<GroupBox>` containers with rounded corners, heavy borders, and vertical label-over-input layouts:
  ```
  Company Name *
  [____________________________________________________]
  ```
  Every field stretches across the entire window width, consuming massive vertical screen real estate and forcing unnecessary scrolling.
- **Accounting Defect**: In accounting software, inputs use horizontal label-input alignment in compact 2-column or 4-column structured tables:
  ```
  Company Name *      [____________________________]
  Print Name          [____________________________]
  Alias / Code        [____________________]
  State *             [ Gujarat ▼ ]    State Code [24]
  ```
  Section dividers must be clean, compact horizontal rules (`IDENTIFICATION ───────────────────────`), not thick nested GroupBoxes.

### 2.3 Focus Visibility ("Where will my next keystroke go?")
- **The Problem**: Active focus indication is limited to a subtle border color change. In a screen with 15 inputs, it is difficult to spot where the cursor currently rests.
- **Accounting Defect**: Accounting data entry is keyboard-driven without mouse usage. The focused field must pop out immediately with a crisp 1.5px border and a subtle high-contrast background tint (e.g. `#FEFCE8` soft ledger yellow or `#EFF6FF` soft executive tint).

### 2.4 Data Grids (`CompanyListDialog`, `FinancialYearManagementDialog`, `AccountGroupManagementDialog`, `LedgerManagementDialog`)
- **The Problem**: Grids look like standard WPF DataGrids with default header styling, uneven column padding, and lack of visual anchoring.
- **Accounting Defect**: Accounting grids require:
  - Crisp 1px borders (`#CBD5E1`) with visible cell dividers.
  - Compact 22-24px row heights.
  - Strong column headers with uppercase or clear title styling.
  - Monospace tabular font for numeric columns (`₹ #,##,##0.00`) right-aligned with Dr/Cr indicators aligned in adjacent columns.
  - Visually distinct active row selection (`#1E3A8A` deep navy or `#DCEBF7` steel blue) that guarantees high contrast with white/dark text.

### 2.5 Dialog Shells & Action Bars
- **The Problem**: Dialogs have inconsistent outer padding, disparate button sizes, and lack a unified top context banner and bottom shortcut legend.
- **Accounting Defect**: Every dialog must follow an identical structural grammar:
  1. Top Company & Entity Context Strip (`BillsEasy • Acme Corp [FY 2026-2027]`).
  2. Clear Screen Title & Subtitle.
  3. Structured, aligned form body.
  4. Bottom Action & Keyboard Legend Bar (`[F2] Save   [Esc] Cancel   [Enter] Next   [Shift+Enter] Previous`).

---

## 3. Remediation Roadmap for Phase 1.4.4

| Area | Current State (Phase 1.4.3) | Target State (Phase 1.4.4) |
| :--- | :--- | :--- |
| **Form Layout** | Vertical stacked labels inside rounded GroupBoxes | Horizontal aligned label-input pairs with compact section rules |
| **Focus State** | Subtle 1px border shift | High-contrast focus border + soft active field tint |
| **Density** | Excessive padding (16-24px), wasted whitespace | Compact 4-8px spacing, 24px control heights, dense alignment |
| **DataGrid** | Generic WPF table | Dense 22px rows, monospace numeric alignment, visible cell gridlines |
| **MainWindow** | Empty web dashboard with giant buttons | Desktop accounting workspace with active company cockpit & shortcut strip |
| **Dialog Shell** | Generic WPF window chrome | Standardized accounting window shell with unified header and F-key footer |
| **Buttons** | Large pill/rounded buttons | Crisp, rectangular 24-26px desktop accounting command buttons |
