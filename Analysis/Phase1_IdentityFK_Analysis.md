# Phase 1 - Identity Column & Foreign Key Analysis

## Identity Column Strategy

### SOURCE DATABASE: MMGTDBCOPY

All primary key columns are GUID/uniqueidentifier type (NOT identity integers):

| Table | Primary Key Column | Type | Is Identity | Seed | Increment | Notes |
|---|---|---|---|---|---|---|
| Users | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| PMICompanies | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| Applications | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| ApplicationDocuments | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| ApplicationReviewHistory | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |

### TARGET DATABASE: MMGTDB2A

All primary key columns are identicalGUID/uniqueidentifier type (NOT identity integers):

| Table | Primary Key Column | Type | Is Identity | Seed | Increment | Notes |
|---|---|---|---|---|---|---|
| Users | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| PMICompanies | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| Applications | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| ApplicationDocuments | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |
| ApplicationReviewHistory | Id | uniqueidentifier | NO | NULL | NULL | GUID PK, no seed/incr |

### Migration Strategy: Identity Columns

**✅ NO SET IDENTITY_INSERT REQUIRED** - Since all PKs are GUIDs (not identity integers), we do NOT need `SET IDENTITY_INSERT ON/OFF`. We can directly INSERT with the original GUID values, preserving all foreign key relationships.

**Decision: PRESERVE EXISTING GUID VALUES**
- All GUID PKs from source will be preserved in target
- All GUID FKs will continue pointing to correct records
- No re-seeding needed
- No identity conflicts possible

---

## Foreign Key Relationships

### SOURCE: MMGTDBCOPY - FK Dependencies

```
Users
  ├─ PMICompanyId → PMICompanies.Id (NO_ACTION on DELETE)
  └─ (No other FKs from Users)

PMICompanies
  └─ (No FKs from PMICompanies)

Applications
  ├─ PMICompanyId → PMICompanies.Id (NO_ACTION on DELETE)
  ├─ UserId → Users.Id (NO_ACTION on DELETE)
  └─ (No other FKs)

ApplicationDocuments
  └─ ApplicationId → Applications.Id (CASCADE on DELETE)

ApplicationReviewHistory
  ├─ ApplicationId → Applications.Id (CASCADE on DELETE)
  └─ ReviewedById → Users.Id (NO_ACTION on DELETE)
```

### TARGET: MMGTDB2A - FK Dependencies

```
Users (15 columns)
  ├─ PMICompanyId → PMICompanies.Id (NO_ACTION on DELETE)
  └─ (No other FKs from Users)

PMICompanies (9 columns)
  └─ (No FKs from PMICompanies)

Applications (24 columns)
  ├─ PMICompanyId → PMICompanies.Id (NO_ACTION on DELETE)
  ├─ AssignedToUserId → Users.Id (nullable, new field)
  └─ (No FK to ApplicantId yet - defined but no constraint)

ApplicationDocuments (18 columns)
  ├─ ApplicationId → Applications.Id (CASCADE on DELETE)
  ├─ DocumentTypeId → DocumentTypes.Id (nullable, new field)
  └─ UploadedBy → Users.Id (nullable, new field)

ApplicationReviewHistory (6 columns)
  ├─ ApplicationId → Applications.Id (CASCADE on DELETE)
  └─ ReviewerId → Users.Id (NO_ACTION on DELETE)

[NEW in Target] DocumentTypes (12 columns)
  └─ (referenced by ApplicationDocuments)

[NEW in Target] ApplicationAssignments (7 columns)
  ├─ ApplicationId → Applications.Id
  ├─ UserId → Users.Id
  └─ (other FKs)

[NEW in Target] ReviewerPerformanceMetrics (19 columns)
  ├─ ReviewerId → Users.Id
  └─ (other FKs)

[And more new tables with various FKs]
```

### Migration Order (FK Dependency Analysis)

| Phase | Order | Table | Record Count | Reason | Depends On |
|---|---|---|---|---|---|
| 1 | 1 | Users | 110 | No FKs pointing to it in migration set, but needed by others | None |
| 1 | 2 | PMICompanies | 24 | No FKs pointing to it, but referenced by Users & Applications | None |
| 2 | 3 | Applications | 13,789 | FK→PMICompanies, FK→Users | Users, PMICompanies |
| 3 | 4 | ApplicationDocuments | 14,047 | FK→Applications, new FK→DocumentTypes (nullable) | Applications |
| 3 | 5 | ApplicationReviewHistory | 31,135 | FK→Applications, FK→Users | Applications, Users |

### Validation: No Cyclic Dependencies ✅
- Users depends on: nothing
- PMICompanies depends on: nothing
- Applications depends on: Users, PMICompanies (acyclic) ✅
- ApplicationDocuments depends on: Applications (acyclic) ✅
- ApplicationReviewHistory depends on: Applications, Users (acyclic) ✅

---

## Foreign Key Constraint Handling During Migration

### Pre-Migration: DISABLE Constraints
Target database has foreign key constraints that would block INSERT if violated. We must:

1. **Disable all foreign key constraints in MMGTDB2A** before copying:
```sql
-- Disable all FK constraints temporarily
ALTER TABLE Users NOCHECK CONSTRAINT ALL;
ALTER TABLE PMICompanies NOCHECK CONSTRAINT ALL;
ALTER TABLE Applications NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationDocuments NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationReviewHistory NOCHECK CONSTRAINT ALL;
```

2. **Copy data in migration order** (Users → PMICompanies → Applications → ApplicationDocuments → ApplicationReviewHistory)

3. **Re-enable and validate constraints**:
```sql
ALTER TABLE Users CHECK CONSTRAINT ALL;
ALTER TABLE PMICompanies CHECK CONSTRAINT ALL;
ALTER TABLE Applications CHECK CONSTRAINT ALL;
ALTER TABLE ApplicationDocuments CHECK CONSTRAINT ALL;
ALTER TABLE ApplicationReviewHistory CHECK CONSTRAINT ALL;
```

### Why Disable Constraints During Migration?
- Source data was already validated (FK integrity exists in source)
- Constraint checking during bulk INSERT is slower
- If constraint violation detected during re-enable, we catch it and verify data quality before rollback
- Simpler transaction management

### Risk: Constraint Re-enable Failure
If any FK constraint fails to re-enable:
- Indicates data integrity problem in source or mapping error
- Must investigate and fix before proceeding
- Migration is rolled back; no corrupted data persists

---

## Unique Constraints and Indexes

### SOURCE: MMGTDBCOPY Unique Constraints

| Table | Column | Constraint Type | Notes |
|---|---|---|---|
| Users | UserName | UNIQUE INDEX | Must be preserved |
| Users | NormalizedUserName | UNIQUE INDEX | Must be preserved |
| Users | Email | UNIQUE INDEX | May be NULL |
| Users | NormalizedEmail | UNIQUE INDEX | May be NULL |
| PMICompanies | CompanyCode | UNIQUE CONSTRAINT | Must be unique |
| Applications | ApplicationNumber | UNIQUE CONSTRAINT | Must be unique |

### TARGET: MMGTDB2A Unique Constraints

Same as source (all 6 unique constraints present in target).

### Migration Risk: Duplicate Unique Key Values
- Source has 110 Users with unique UserName values
- Target has 5 seeded Users with different UserNames
- After INSERT, target will conflict if UserName already exists

**SOLUTION: CLEAR target data first**
```sql
DELETE FROM ApplicationReviewHistory;
DELETE FROM ApplicationDocuments;
DELETE FROM Applications;
DELETE FROM Users;
DELETE FROM PMICompanies;
```

---

## Column Constraint Validation

### NOT NULL Constraints in Core Tables

| Table | Column | Source NOT NULL | Target NOT NULL | Match | Action |
|---|---|---|---|---|---|
| Users | Id | YES | YES | ✅ | Direct copy |
| Users | UserName | YES | YES | ✅ | Direct copy |
| PMICompanies | Id | YES | YES | ✅ | Direct copy |
| PMICompanies | CompanyName | YES | YES | ✅ | Direct copy |
| Applications | Id | YES | YES | ✅ | Direct copy |
| Applications | ApplicationNumber | YES | YES | ✅ | Direct copy |
| ApplicationDocuments | Id | YES | YES | ✅ | Direct copy |
| ApplicationDocuments | FileName | YES | YES | ✅ | Direct copy |
| ApplicationDocuments | FileSize | YES | YES | ✅ | Direct copy |
| ApplicationReviewHistory | Id | YES | YES | ✅ | Direct copy |

### New Nullable Columns in Applications (target only)
All 12 new columns in target Applications are nullable - can INSERT NULL or set defaults:
- ApplicantId → NULL
- LoanAmount → NULL or 0
- PropertyAddress → NULL
- PropertyValue → NULL or 0
- LoanTerm → NULL or 0
- InterestRate → NULL or 0
- MortgageType → NULL
- ApprovalDate → NULL
- ClosingDate → NULL
- Notes → NULL
- AssignedToUserId → NULL
- Priority → NULL

### New Nullable Columns in ApplicationDocuments (target only)
All 10 new columns in target ApplicationDocuments are nullable:
- DocumentTypeId → NULL (no mapping in source)
- FileContent → NULL (legacy files use FilePath, new files use BLOB)
- Version → 1 (default)
- IsLatestVersion → 1 (default)
- ReviewStatus → NULL
- ReviewComments → NULL
- ReviewedBy → NULL
- ReviewedAt → NULL
- UploadedBy → NULL
- IsLegacy → 1 (indicates legacy disk file)

---

## Summary: Identity & FK Migration Strategy

| Aspect | Decision | Rationale |
|---|---|---|
| Identity Columns | NO SET IDENTITY_INSERT needed | All PKs are GUIDs, not identity ints |
| Preserve PKs | YES | GUID values must be preserved for FK integrity |
| Migration Order | User → PMICompanies → Applications → Docs → History | Respects FK dependencies |
| Constraint Handling | Disable during migration, re-enable after | Speeds up bulk INSERT, validates data integrity |
| Unique Constraints | Will be validated on re-enable | Source data already unique, no conflicts expected |
| New Nullable Columns | INSERT NULL or defaults | All new columns nullable, won't violate NOT NULL |
| Legacy Document Flag | Set IsLegacy=1 for all ApplicationDocuments | Indicates disk-based storage, not BLOB |

---

## Pre-Migration Checklist: Identity & FK

- [ ] Verify source database has no FK orphans (all FKs resolvable)
- [ ] Confirm target has all required FK constraints defined
- [ ] Verify ApplicationDocuments.ApplicationId FKs in source are all valid
- [ ] Verify ApplicationReviewHistory.ReviewerId FKs in source are all valid
- [ ] Check for any circular dependencies (found: ✅ NONE)
- [ ] Confirm target Users table is empty (current: 5 seeded dev users, must clear)
- [ ] Confirm target PMICompanies table is empty (current: 1 seeded dev company, must clear)
- [ ] Verify target DocumentTypes table is seeded (current: 24 records) - KEEP THESE
- [ ] Prepare scripts to disable/enable all FK constraints
- [ ] Plan rollback strategy if constraints fail to re-enable

