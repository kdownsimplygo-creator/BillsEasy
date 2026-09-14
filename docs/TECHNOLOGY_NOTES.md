# Technology Architecture & Implementation Notes

**Project:** BillsEasy  
**Target Platform:** Windows Desktop (.NET 8.0 / WPF / C#)  
**Architecture Pattern:** Clean Architecture / Modular Monolith  
**UI Pattern:** MVVM (Model-View-ViewModel)

---

## 1. Architectural Strategy

`
+-------------------------------------------------------------+
¦                 BillsEasy.Presentation.Wpf                  ¦
¦   • Views (XAML)       • ViewModels (CommunityToolkit.Mvvm)¦
¦   • Converters         • UI Controls / Themes               ¦
+-------------------------------------------------------------+
                               ¦ references
                               ?
+-------------------------------------------------------------+
¦                   BillsEasy.Application                     ¦
¦   • Use Cases / Commands / Queries (CQRS / MediatR / Svcs)  ¦
¦   • DTOs               • Validators (FluentValidation)      ¦
¦   • Interfaces (IRepository, IUnitOfWork, IGstCalculator)   ¦
+-------------------------------------------------------------+
                               ¦ references
                               ?
+-------------------------------------------------------------+
¦                      BillsEasy.Domain                       ¦
¦   • Aggregate Roots    • Entities (Voucher, Ledger, Item)   ¦
¦   • Value Objects (Money, GSTIN, HSN, TaxRate)              ¦
¦   • Domain Events      • Business Rules & Exceptions        ¦
+-------------------------------------------------------------+
                               ?
                               ¦ implements interfaces of App
+-------------------------------------------------------------+
¦                  BillsEasy.Infrastructure                   ¦
¦   • Persistence (SQLite / Microsoft.Data.Sqlite / EF Core)   ¦
¦   • Future Provider Extensibility (PostgreSQL provider)     ¦
¦   • File Storage / PDF Generators (QuestPDF / Skia)         ¦
¦   • Logging (Serilog)  • System Clock / Audit Trail Engine  ¦
+-------------------------------------------------------------+
`

---

## 2. Key Technology Selections & Justifications

### 2.1 UI Layer: WPF (.NET 8 Windows Desktop)
- **Why WPF over WinForms/MAUI/Avalonia for Windows Native:**
  - High performance desktop vector rendering with complete hardware acceleration on Windows.
  - Battle-tested for complex, multi-column accounting data grids and high-frequency keyboard navigation.
  - Mature ecosystem for custom controls, XAML styling, templating, and data virtualization (virtualizing large ledger reports with 100k+ rows).

### 2.2 MVVM Implementation: CommunityToolkit.Mvvm
- Official Microsoft high-performance MVVM library leveraging Roslyn C# source generators ([ObservableProperty], [RelayCommand]).
- Eliminates reflection overhead and boilerplate INotifyPropertyChanged code.

### 2.3 Persistence Strategy: SQLite (Phase 1) $\rightarrow$ PostgreSQL Readiness (Phase 2+)
- **Phase 1 (Single-PC):**
  - Embedded SQLite file database with WAL (Write-Ahead Logging) enabled.
  - Enforced foreign keys (PRAGMA foreign_keys = ON;).
  - Strict typing, explicit transactions (BEGIN IMMEDIATE TRANSACTION), and parameterized queries to prevent SQL injection.
- **Phase 2 (Multi-PC / LAN):**
  - Database access abstracted via Unit of Work and Repository interfaces or EF Core DbContext, ensuring queries and migrations remain compatible with PostgreSQL.

### 2.4 Audit Trail & Compliance (MCA Audit Trail Mandate)
- Indian accounting compliance requires an unalterable Edit Log / Audit Trail recording:
  - Timestamp of creation and modification.
  - User ID / workstation performing the change.
  - Previous values vs new values for every ledger/voucher change.

### 2.5 Testing & Verification Stack
- **Unit Testing:** xUnit + FluentAssertions + Moq / NSubstitute.
- **Integration Testing:** In-memory SQLite (Data Source=:memory:) test suites verifying repository queries, concurrency, and cascading ledger recalculations.

### 2.6 Logging & Diagnostics
- Serilog with rolling file sinks (logs/billseasy-.log), structured JSON output, and uncaught exception handlers in App.xaml.cs.
