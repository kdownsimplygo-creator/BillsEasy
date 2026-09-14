# 10. WPF & MVVM Architecture Review

## 1. Presentation Architecture
- **Framework:** Windows Presentation Foundation (.NET 8.0 on Windows).
- **MVVM Library:** `CommunityToolkit.Mvvm` (Source Generators for `[ObservableProperty]`, `[RelayCommand]`).
- **Dependency Injection:** `Microsoft.Extensions.DependencyInjection` configured in `App.xaml.cs`.
- **Decoupling:** ViewModels receive Application services via constructor injection. Views receive ViewModels via `DataContext`.

---

## 2. Keyboard-First & High-Speed Data Entry Readiness
Indian accounting software users expect rapid, keyboard-only data entry workflows.

### Evaluation of Current UI Architecture:
1. **Access Keys & Shortcuts:** Menus implement standard access keys (`Alt+N` Create Company, `Alt+O` Open Company, `Alt+F` Manage FY, `Alt+G` Account Groups, `Alt+L` Ledgers, `F2` Add, `F3` Edit, `Delete` Remove).
2. **Dialog Form Layouts:**
   - Account Groups: Dual list and tree view with keyboard selection.
   - Ledgers: Tabbed high-density dialog with clean tab order across Basic, Opening/Credit, Statutory, and Banking tabs.
3. **Future Voucher Grid Entry:** The MVVM setup supports future high-speed transaction grids (`DataGrid` with customized keyboard commit on `Enter` and modal search popups on `Space`/`F3`).
