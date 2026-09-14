# BillsEasy — UI Design System (v2 Native Indian Accounting)
**Status**: Authoritative Technical Specification  
**Phase**: 1.4.4 Native Accounting Visual Redesign v2  
**Target Audience**: Presentation Layer Developers  

---

## 1. Visual Identity Principles

BillsEasy embodies the visual identity of a high-speed, serious native desktop Indian accounting application. It rejects generic web SaaS aesthetics (cards, rounded pills, drop shadows, oversized whitespace) in favor of:

1. **High Information Density**: Screen real estate is treated as premium accounting workspace. Controls use 24px heights, 22px DataGrid rows, and 12px outer dialog padding.
2. **Sharp Desktop Aesthetic**: 0px/1px corner radii, 1px crisp borders (`#CBD5E1`), and structured tabular alignments.
3. **Instant Keystroke Feedback ("Where will my next keystroke go?")**: Active inputs glow with a distinct 1.5px deep navy border (`#1E3A8A`) and an unmistakable soft accounting ledger yellow tint (`#FFFEE6`).
4. **Structured Horizontal Field Alignment**: Forms prioritize horizontal `[Label (160px)] [Input]` pairs organized by clean horizontal section rules (`IDENTIFICATION ─────────`), eliminating bloated nested GroupBoxes.
5. **Tabular Numerals & Monospace Balances**: Monetary values are rendered right-aligned in `Consolas` monospace font formatted to Indian standards (`₹ #,##,##0.00`).

---

## 2. Palette & Semantic Brushes (`Themes/Colors.xaml`)

| Token Name | Hex Value | Purpose / Semantic Meaning |
| :--- | :--- | :--- |
| `WindowBackgroundBrush` | `#F1F5F9` | Neutral slate-gray desktop window canvas |
| `SurfaceBackgroundBrush` | `#FFFFFF` | Input controls and primary card surfaces |
| `ActiveFieldBackgroundBrush` | `#FFFEE6` | **Iconic soft ledger tint on focused inputs** |
| `ActiveFieldBorderBrush` | `#1E3A8A` | High-contrast deep navy border on focused inputs |
| `PrimaryBrandBrush` | `#1E3A8A` | Executive Navy for primary buttons, titles, and headers |
| `ShellHeaderBackgroundBrush` | `#0F2744` | Midnight Navy for top application & context strips |
| `ShortcutBarBackgroundBrush` | `#EAEFF5` | Dedicated bottom keyboard shortcut bar |
| `GridHeaderBackgroundBrush` | `#EAEFF5` | Structured desktop accounting column headers |
| `GridAlternateRowBackgroundBrush`| `#F8FAFC` | Subtle alternating zebra striping |
| `GridSelectedRowBackgroundBrush` | `#DCEBF7` | High-visibility steel blue active row selection |
| `DebitBrush` | `#166534` | Forest green for Dr balance polarity |
| `CreditBrush` | `#991B1B` | Crimson red for Cr balance polarity |
| `ErrorBackgroundBrush` | `#FEE2E2` | Light red alert banner background |
| `ErrorBrush` | `#B91C1C` | Deep red validation error text and borders |

---

## 3. Spacing & Dimension Tokens (`Themes/Spacing.xaml`)

```xml
<!-- Control Heights -->
<sys:Double x:Key="ControlHeightDense">24</sys:Double>
<sys:Double x:Key="ControlHeightStandard">26</sys:Double>
<sys:Double x:Key="ButtonHeightDense">24</sys:Double>
<sys:Double x:Key="DataGridRowHeightDense">22</sys:Double>
<sys:Double x:Key="DataGridHeaderHeightDense">24</sys:Double>

<!-- Form Label Widths -->
<GridLength x:Key="FormLabelWidth">160</GridLength>
<GridLength x:Key="FormLabelWidthShort">120</GridLength>

<!-- Corner Radii -->
<CornerRadius x:Key="ControlCornerRadius">0</CornerRadius>
<CornerRadius x:Key="CardCornerRadius">1</CornerRadius>

<!-- Margins & Padding -->
<Thickness x:Key="FieldMarginDense">0,1,0,3</Thickness>
<Thickness x:Key="DialogPadding">12,10,12,10</Thickness>
<Thickness x:Key="GridCellPadding">6,1</Thickness>
```

---

## 4. Typography Hierarchy (`Themes/Typography.xaml`)

- **Primary Font**: `Segoe UI, Tahoma, Arial, sans-serif`
- **Monospace Font**: `Consolas, 'Courier New', monospace` (for all numeric columns, voucher lines, and currency amounts)

| Style Key | Size | Weight | Alignment | Application |
| :--- | :--- | :--- | :--- | :--- |
| `FormLabelStyle` | 12px | SemiBold | Left (Center V) | Left-column input labels |
| `SectionHeaderStyle` | 11px | Bold | Left | AccountingSection title bars |
| `DialogTitleStyle` | 14px | Bold | Left | Dialog headers |
| `NumericValueStyle` | 12px | Regular | Right | Tabular balances and amounts |
| `ShortcutLegendStyle` | 11px | SemiBold | Left | Bottom shortcut guide cues |
| `HelperTextStyle` | 11px | Regular | Left | Explanatory field notes |
