# BillsEasy — System Architecture Specification

**Pattern:** Clean Architecture / Modular Monolith  
**Runtime:** .NET 8.0 LTS (C# 12)  
**Desktop UI Framework:** Windows Presentation Foundation (WPF)  
**MVVM Framework:** CommunityToolkit.Mvvm  
**Persistence:** Embedded SQLite 3 (Phase 1) $\longrightarrow$ Optional PostgreSQL (Phase 2+ LAN)  

---

## 1. High-Level Architectural Layers

The system follows a strict **Clean Architecture (Dependency Rule)**. Dependencies flow inward toward the pure Domain model.

`
       +-------------------------------------------------------------+
       ¦                BillsEasy.Presentation.Wpf                   ¦
       ¦  • Views (XAML)       • ViewModels (CommunityToolkit.Mvvm) ¦
       ¦  • Navigation Service • UI Behaviors / Custom Controls      ¦
       ¦  • Converters         • Keyboard Shortcuts & Themes         ¦
       +-------------------------------------------------------------+
                                      ¦ references
                                      ?
       +-------------------------------------------------------------+
       ¦                   BillsEasy.Application                     ¦
       ¦  • Application Services / Use Cases                         ¦
       ¦  • DTOs & Mappings    • Input Validation (FluentValidation) ¦
       ¦  • Interfaces (IRepository, IUnitOfWork, ICurrentUserService)¦
       +-------------------------------------------------------------+
                                      ¦ references
                                      ?
       +-------------------------------------------------------------+
       ¦                      BillsEasy.Domain                       ¦
       ¦  • Aggregate Roots    • Domain Entities                     ¦
       ¦  • Value Objects      • Domain Invariants & Rules           ¦
       ¦  • Domain Exceptions  • Domain Enums                        ¦
       +-------------------------------------------------------------+
                                      ?
                                      ¦ implements interfaces of Application
       +-------------------------------------------------------------+
       ¦                  BillsEasy.Infrastructure                   ¦
       ¦  • Persistence (SQLite / Microsoft.Data.Sqlite / Dapper/EF) ¦
       ¦  • Unit of Work & Transaction Management                    ¦
       ¦  • System Clock & Machine Identity Provider                 ¦
       ¦  • Logging (Serilog Rolling File Sink)                      ¦
       +-------------------------------------------------------------+
`

---

## 2. Project Structure & Responsibilities

| Project Name | Output Type | Dependencies | Key Responsibilities |
| :--- | :--- | :--- | :--- |
| **BillsEasy.Domain** | Class Library | *None (Zero third-party dependencies)* | Encapsulates all business logic, entities, value objects, double-entry rules, and domain invariants. |
| **BillsEasy.Application** | Class Library | BillsEasy.Domain | Orchestrates use cases, command/query handlers, DTO transformations, validation rules, and service contracts. |
| **BillsEasy.Infrastructure** | Class Library | BillsEasy.Application, BillsEasy.Domain, Microsoft.Data.Sqlite, Serilog | Handles database connections, SQL migrations, Dapper/EF data mappers, file I/O, and audit logging. |
| **BillsEasy.Presentation.Wpf** | WinExe | BillsEasy.Application, BillsEasy.Infrastructure, CommunityToolkit.Mvvm | Contains WPF views, custom accounting controls, view models, keyboard focus managers, and application entry (App.xaml). |
| **BillsEasy.UnitTests** | Test Project | BillsEasy.Domain, BillsEasy.Application, xUnit, FluentAssertions | Automated tests for double-entry rules, bill allocation, voucher balancing, and use-case validations. |
| **BillsEasy.IntegrationTests**| Test Project | BillsEasy.Infrastructure, Microsoft.Data.Sqlite, xUnit | Validates SQLite transactional behavior, schema migrations, and repository queries against in-memory SQLite. |

---

## 3. Separation of Concerns & Modular Monolith Design

Inside the application and domain layers, code is partitioned into cohesive **Feature Modules**:
1. **Company**: Company aggregate, fiscal year bounds, state registration, books beginning date.
2. **AccountingMasters**: AccountGroup hierarchy, Ledger master records, contact & statutory metadata.
3. **BillWise**: Bill references, pending bills calculation, multi-bill allocation engine.
4. **Voucher**: Voucher header, multi-line debits/credits, balancing invariant validation.
5. **Security**: User attribution metadata (CreatedBy, ModifiedBy, session context).
6. **Audit**: Immutable chronological change captures.

Modules communicate within the monolithic application boundaries via direct strongly-typed C# service invocations and domain objects, avoiding complex in-process bus serialization while preserving clear logical boundaries.

---

## 4. Why This Architecture Fits BillsEasy

1. **Testability Without Database:** The domain and application use cases can be 100% verified in memory using pure C# unit tests.
2. **Persistence Portability:** Switching or augmenting SQLite with PostgreSQL for a LAN edition requires adding a new PostgreSqlUnitOfWork in Infrastructure without modifying a single line in Domain or Presentation.
3. **High UI Responsiveness:** WPF bindings connect directly to compiled ViewModels generated by CommunityToolkit.Mvvm, keeping CPU overhead negligible for instant UI rendering and fast navigation.
