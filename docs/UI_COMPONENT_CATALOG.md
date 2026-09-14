# BillsEasy — UI Component Catalog (v2)
**Status**: Authoritative Technical Specification  
**Phase**: 1.4.4 Native Accounting Visual Redesign v2  
**Target Audience**: Presentation Layer Developers  

---

## 1. Reusable Visual Primitives

### 1.1 `AccountingSection`
- **Location**: `BillsEasy.Presentation.Wpf.Controls.AccountingSection`
- **Purpose**: Compact section separator replacing bulky, rounded GroupBoxes. Features a bold 11px uppercase title in brand navy followed by a 1px horizontal hairline separator rule.
- **Properties**:
  - `Title` (string): Header text (e.g., "IDENTIFICATION", "STATUTORY & JURISDICTION").
- **Usage**:
```xml
<controls:AccountingSection Title="IDENTIFICATION" />
```

---

### 1.2 `AccountingDialogHeader`
- **Location**: `BillsEasy.Presentation.Wpf.Controls.AccountingDialogHeader`
- **Purpose**: Executive desktop dialog header shell conforming to the standard accounting window layout:
  - Row 0: `BillsEasy • Native Accounting` brand + Right-aligned `CompanyContext` (`Acme Corp [FY 2026-2027]`).
  - Row 1: Dialog Title + `EntityBadge` (`[Company Master]`, `[Chart of Accounts]`) + Subtitle + Optional `HeaderRightContent` (search/filters).
- **Properties**:
  - `Title` (string)
  - `Subtitle` (string)
  - `EntityBadge` (string)
  - `CompanyContext` (string)
  - `HeaderRightContent` (object)
- **Usage**:
```xml
<controls:AccountingDialogHeader 
    Title="Create Company"
    Subtitle="Configure authoritative company profile, statutory GST, and accounting period bounds."
    EntityBadge="[Company Master]" />
```

---

### 1.3 `AccountingDialogFooter`
- **Location**: `BillsEasy.Presentation.Wpf.Controls.AccountingDialogFooter`
- **Purpose**: Standardized lower action bar containing a dedicated high-contrast alert strip for validation errors, a full keyboard shortcut guide (`[F2] Save   •   [Esc] Cancel   •   [Enter] Next   •   [Shift+Enter] Prev`), and compact primary/cancel action buttons.
- **Properties**:
  - `SaveCommand` (ICommand): Bound to `F2` / Save execution.
  - `SaveButtonText` (string, default: `_Save (F2)`)
  - `CancelButtonText` (string, default: `_Cancel (Esc)`)
  - `ShortcutLegend` (string, default: `[F2] Save   •   [Esc] Cancel   •   [Enter] Next   •   [Shift+Enter] Prev`)
  - `ErrorMessage` (string): Triggers the red alert banner when non-empty.
- **Usage**:
```xml
<controls:AccountingDialogFooter
    ErrorMessage="{Binding ErrorMessage}"
    SaveCommand="{Binding SaveCommand}"
    SaveButtonText="_Save Company (F2)"
    CancelClicked="Cancel_Click" />
```

---

### 1.4 `AccountingStatusBar`
- **Location**: `BillsEasy.Presentation.Wpf.Controls.AccountingStatusBar`
- **Purpose**: Application status footer displaying operational messages, active company, active FY, user profile, and version tag.
- **Properties**:
  - `StatusMessage`, `ActiveCompany`, `ActiveFinancialYear`, `CurrentUser`, `VersionTag`.

---

## 2. Input Styles & Form Layout

### 2.1 Aligned Horizontal Form System
Forms use a 2-column or 4-column Grid layout with `{StaticResource FormLabelWidth}` (160px) for the label column:
```xml
<Grid Margin="0,2,0,8">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="{StaticResource FormLabelWidth}" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Column="0" Text="Company Name *" Style="{StaticResource FormLabelStyle}" />
    <TextBox Grid.Column="1" Text="{Binding CompanyName}" />
</Grid>
```

### 2.2 Active Focus Indication
All textboxes and comboboxes highlight automatically with:
- `ActiveFieldBorderBrush`: `#1E3A8A` (1.5px border)
- `ActiveFieldBackgroundBrush`: `#FFFEE6` (iconic soft ledger yellow)

### 2.3 Read-Only & Numeric Fields
- `Style="{StaticResource ReadOnlyFieldStyle}"`: Non-editable display field with gray background, disabled tab-stop.
- `Style="{StaticResource NumericTextBoxStyle}"`: Tabular monospace font, right-aligned.
