# BillsEasy — Module Boundaries & Dependency Topology

---

## 1. Module Dependency Graph

`
                            +-------------------+
                            ¦      Company      ¦?-------------------+
                            +-------------------+                    ¦
                                      ¦                              ¦
                                      ?                              ¦
                            +-------------------+                    ¦
                            ¦   FinancialYear   ¦                    ¦
                            +-------------------+                    ¦
                                      ¦                              ¦
                                      ?                              ¦
                            +-------------------+                    ¦
                            ¦  AccountGroup     ¦                    ¦
                            +-------------------+                    ¦
                                      ¦                              ¦
                                      ?                              ¦
                            +-------------------+                    ¦
                            ¦      Ledger       ¦                    ¦
                            ¦ (Accounts/Parties)¦                    ¦
                            +-------------------+                    ¦
                                      ¦                              ¦
                   +-------------------------------------+           ¦
                   ?                                     ?           ¦
         +-------------------+                 +-------------------+ ¦
         ¦     BillWise      ¦                 ¦      Voucher      ¦-+
         ¦  (Ref / Alloc)    ¦                 ¦   (Atomic Core)   ¦
         +-------------------+                 +-------------------+
                   ¦                                     ¦
                   +-------------------------------------+
                                      ¦
                                      ?
                            +-------------------+
                            ¦  Payment / Receipt¦
                            +-------------------+
                                      ¦
                                      ?
                            +-------------------+
                            ¦     Reporting     ¦
                            ¦(Ledger/Outstanding¦
                            +-------------------+

         +---------------------------------------------------------+
         ¦                    Cross-Cutting                        ¦
         ¦          Security (Context)  &  Audit (Trail)           ¦
         +---------------------------------------------------------+
`

---

## 2. Module Responsibilities & Boundary Contracts

| Module | Core Responsibility | Inward Contracts / Consumes | Outward Contracts / Exposes |
| :--- | :--- | :--- | :--- |
| **Company** | Master organization definition, fiscal year bounds, multi-company workspace discovery. | System Clock, Config | CompanyId, ActiveCompanyContext, FinancialYearStart |
| **FinancialYear** | Date validation, FY switching, cross-period boundary logic. | Company | FinancialYearRange, IsDateInActiveFY() |
| **AccountGroup** | Standard chart of accounts tree structure (Primary & Sub-groups). | Company | AccountGroupTree, NatureOfGroup |
| **Ledger** | Authoritative definition of accounts & parties (Debtors, Creditors, Bank, Cash, Expenses). | Company, AccountGroup | LedgerSummary, LedgerLookup, IsBillWiseEnabled |
| **BillWise** | Unsettled bill management, reference allocation engine, aging derivation. | Ledger, Voucher | PendingBillsQuery, BillAllocationValidator |
| **Voucher** | Double-entry transaction engine, multi-line balance verification ( == Cr$). | Company, Ledger, Security | IVoucherService, SaveVoucherCommand |
| **Payment / Receipt** | Specialized workflow wrappers for cash/bank disbursements and collections with bill allocation. | Voucher, BillWise | RecordPaymentUseCases, PaymentVoucherViewModel |
| **Reporting** | Read-only aggregation for Day Book, Ledger Account Statements, Outstanding Bills. | Voucher, Ledger, BillWise | GetLedgerStatementQuery, GetOutstandingBillsQuery |
| **Security** | Current user context, station ID, operational session metadata. | System / App Context | ICurrentUserService (UserId, SessionId) |
| **Audit** | Immutable recording of entity lifecycle changes for auditability. | All Modules | IAuditTrailService.LogChange() |
