# 06. Account Groups & Hierarchy Review

## 1. Hierarchy & Classification Model
- **Purpose:** Provide classification and financial statement structure for all accounts/ledgers.
- **Root Natures (`GroupNature`):**
  1. `Asset` (Balance Sheet)
  2. `Liability` (Balance Sheet)
  3. `Capital` (Balance Sheet / Equity)
  4. `Income` (Profit & Loss / Trading)
  5. `Expense` (Profit & Loss / Trading)
- **Inheritance:** When creating a child group, its `Nature` is automatically inherited from its immediate parent.

---

## 2. Uniqueness Invariant & Sibling Policy (Phase 1.3 Correction)

The domain invariant established in Phase 1.3 and strictly verified:
1. **Same Company + Same Parent + Same Name:** **REJECTED** (Duplicate sibling name under same parent).
2. **Same Company + Different Parent + Same Name:** **ALLOWED** (e.g. `Office Expenses` under `Direct Expenses` vs `Indirect Expenses`).
3. **Same Company + Both Root Groups (Null Parent) + Same Name:** **REJECTED** (Duplicate root group name).

### Database Enforcement:
```sql
CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Parent_Name
    ON AccountGroups(CompanyId, ParentGroupId, Name COLLATE NOCASE)
    WHERE ParentGroupId IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS UQ_AccountGroups_Company_Root_Name
    ON AccountGroups(CompanyId, Name COLLATE NOCASE)
    WHERE ParentGroupId IS NULL;
```

---

## 3. Circular Hierarchy & Reparenting Invariants
Encapsulated directly within the domain entity (`AccountGroup.Reparent(newParent, companyGroups)`):
- Traverses upwards from the proposed new `newParent` towards root using the required `companyGroups` hierarchy.
- If any ancestor matches `this.Id`, throws `DomainException("Cannot set parent: circular hierarchy detected. Group '...' is an ancestor of the target parent group '...'.")`.
- Automatically enforces nature inheritance (`Nature = newParent.Nature`) and cross-company rejection.
- Verified by domain unit tests directly on the entity as well as service-level tests.

---

## 4. Deletion Protection Guards
Deleting an `AccountGroup` is blocked if:
1. `IsDefaultSystemGroup == true` (System protected).
2. Has child sub-groups (`HasChildrenAsync(groupId)`).
3. Has ledgers assigned to this group (`HasLedgersAsync(groupId)`).
4. Foreign key constraint `ON DELETE RESTRICT` enforces this at the SQLite engine level as well.
