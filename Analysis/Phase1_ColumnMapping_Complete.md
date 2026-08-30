# Phase 1 - Detailed Column Mapping for Data Migration

## Source Database: MMGTDBCOPY

### Table: Users
Columns (15):
- Id | uniqueidentifier | PK | NOT NULL
- UserName | nvarchar(256) | NOT NULL
- NormalizedUserName | nvarchar(256) | NULL
- Email | nvarchar(256) | NULL
- NormalizedEmail | nvarchar(256) | NULL
- EmailConfirmed | bit | NOT NULL
- PasswordHash | nvarchar(max) | NULL
- SecurityStamp | nvarchar(max) | NULL
- ConcurrencyStamp | nvarchar(max) | NULL
- PhoneNumber | nvarchar(max) | NULL
- PhoneNumberConfirmed | bit | NOT NULL
- TwoFactorEnabled | bit | NOT NULL
- LockoutEnd | datetimeoffset | NULL
- LockoutEnabled | bit | NOT NULL
- AccessFailedCount | int | NOT NULL
- FirstLoginTokenHash | nvarchar(128) | NULL
- FirstLoginTokenExpiresAt | datetime2 | NULL
**Record Count: 110**

### Table: PMICompanies
Columns (9):
- Id | uniqueidentifier | PK | NOT NULL
- CompanyName | nvarchar(255) | NOT NULL
- CompanyCode | nvarchar(50) | NOT NULL (Unique)
- RegistrationNumber | nvarchar(100) | NULL
- ContactEmail | nvarchar(255) | NULL
- PhoneNumber | nvarchar(20) | NULL
- Address | nvarchar(500) | NULL
- IsActive | bit | NOT NULL (Default: 1)
- CreatedAt | datetime2 | NOT NULL
**Record Count: 24**

### Table: Applications
Columns (12):
- Id | uniqueidentifier | PK | NOT NULL
- ApplicationNumber | nvarchar(50) | NOT NULL (Unique)
- PMICompanyId | uniqueidentifier | FK NOT NULL
- Status | nvarchar(50) | NOT NULL
- SubmissionDate | datetime2 | NULL
- CreatedAt | datetime2 | NOT NULL
- UpdatedAt | datetime2 | NULL
- ApprovedAt | datetime2 | NULL
- RejectionReason | nvarchar(500) | NULL
- TotalDocuments | int | NULL
- ReviewedDocuments | int | NULL
- PendingDocuments | int | NULL
**Record Count: 13,789**

### Table: ApplicationDocuments
Columns (8):
- Id | uniqueidentifier | PK | NOT NULL
- FileName | nvarchar(255) | NOT NULL
- FilePath | nvarchar(500) | NOT NULL
- FileType | nvarchar(100) | NOT NULL
- DocumentType | nvarchar(100) | NOT NULL
- FileSize | bigint | NOT NULL
- CreatedAt | datetime2 | NOT NULL
- ApplicationId | uniqueidentifier | FK NOT NULL
**Record Count: 14,047**

### Table: ApplicationReviewHistory
Columns (6):
- Id | uniqueidentifier | PK | NOT NULL
- ApplicationId | uniqueidentifier | FK NOT NULL
- ReviewerId | uniqueidentifier | FK NOT NULL
- Status | nvarchar(50) | NOT NULL
- Comments | nvarchar(max) | NULL
- ReviewedAt | datetime2 | NOT NULL
**Record Count: 31,135**

---

## Target Database: MMGTDB2A

### Table: Users
Columns (15):
- Id | uniqueidentifier | PK | NOT NULL
- UserName | nvarchar(256) | NOT NULL
- NormalizedUserName | nvarchar(256) | NULL
- Email | nvarchar(256) | NULL
- NormalizedEmail | nvarchar(256) | NULL
- EmailConfirmed | bit | NOT NULL
- PasswordHash | nvarchar(max) | NULL
- SecurityStamp | nvarchar(max) | NULL
- ConcurrencyStamp | nvarchar(max) | NULL
- PhoneNumber | nvarchar(max) | NULL
- PhoneNumberConfirmed | bit | NOT NULL
- TwoFactorEnabled | bit | NOT NULL
- LockoutEnd | datetimeoffset | NULL
- LockoutEnabled | bit | NOT NULL
- AccessFailedCount | int | NOT NULL
- FirstLoginTokenHash | nvarchar(128) | NULL
- FirstLoginTokenExpiresAt | datetime2 | NULL
**Record Count: 5 (development seeded)**
**MATCH: ✅ Identical schema**

### Table: PMICompanies
Columns (9):
- Id | uniqueidentifier | PK | NOT NULL
- CompanyName | nvarchar(255) | NOT NULL
- CompanyCode | nvarchar(50) | NOT NULL (Unique)
- RegistrationNumber | nvarchar(100) | NULL
- ContactEmail | nvarchar(255) | NULL
- PhoneNumber | nvarchar(20) | NULL
- Address | nvarchar(500) | NULL
- IsActive | bit | NOT NULL (Default: 1)
- CreatedAt | datetime2 | NOT NULL
**Record Count: 1 (development seeded)**
**MATCH: ✅ Identical schema**

### Table: Applications
Columns (24):
- Id | uniqueidentifier | PK | NOT NULL
- ApplicationNumber | nvarchar(50) | NOT NULL (Unique)
- PMICompanyId | uniqueidentifier | FK NOT NULL
- Status | nvarchar(50) | NOT NULL
- SubmissionDate | datetime2 | NULL
- CreatedAt | datetime2 | NOT NULL
- UpdatedAt | datetime2 | NULL
- ApprovedAt | datetime2 | NULL
- RejectionReason | nvarchar(500) | NULL
- TotalDocuments | int | NULL
- ReviewedDocuments | int | NULL
- PendingDocuments | int | NULL
- **[NEW] ApplicantId | uniqueidentifier | NULL** ← NEW in target
- **[NEW] LoanAmount | decimal(18,2) | NULL** ← NEW in target
- **[NEW] PropertyAddress | nvarchar(500) | NULL** ← NEW in target
- **[NEW] PropertyValue | decimal(18,2) | NULL** ← NEW in target
- **[NEW] LoanTerm | int | NULL** ← NEW in target
- **[NEW] InterestRate | decimal(10,4) | NULL** ← NEW in target
- **[NEW] MortgageType | nvarchar(50) | NULL** ← NEW in target
- **[NEW] ApprovalDate | datetime2 | NULL** ← NEW in target
- **[NEW] ClosingDate | datetime2 | NULL** ← NEW in target
- **[NEW] Notes | nvarchar(max) | NULL** ← NEW in target
- **[NEW] AssignedToUserId | uniqueidentifier | NULL** ← NEW in target
- **[NEW] Priority | int | NULL** ← NEW in target
**Record Count: 0 (to be populated)**
**MATCH: ⚠️ Target has 12 additional columns (all nullable)**

### Table: ApplicationDocuments
Columns (18):
Source columns:
- Id | uniqueidentifier | PK | NOT NULL
- FileName | nvarchar(255) | NOT NULL
- FilePath | nvarchar(500) | NULL (nullable in target)
- FileType | nvarchar(100) | NOT NULL
- DocumentType | nvarchar(100) | NULL (nullable in target)
- FileSize | bigint | NOT NULL
- CreatedAt | datetime2 | NOT NULL
- ApplicationId | uniqueidentifier | FK NOT NULL

**NEW columns in target:**
- **[NEW] DocumentTypeId | uniqueidentifier | NULL** ← FK to DocumentTypes (not in source)
- **[NEW] FileContent | varbinary(max) | NULL** ← BLOB storage (not in source; legacy files stay in FilePath)
- **[NEW] Version | int | NOT NULL | Default: 1** ← Version tracking
- **[NEW] IsLatestVersion | bit | NOT NULL | Default: 1** ← Version management
- **[NEW] ReviewStatus | nvarchar(50) | NULL** ← New, default NULL
- **[NEW] ReviewComments | nvarchar(max) | NULL** ← New review capability
- **[NEW] ReviewedBy | uniqueidentifier | NULL** ← New review tracking
- **[NEW] ReviewedAt | datetime2 | NULL** ← New review tracking
- **[NEW] UploadedBy | uniqueidentifier | NULL** ← New audit tracking
- **[NEW] IsLegacy | bit | NOT NULL | Default: 0** ← Flag for legacy disk files

**Record Count: 0 (to be populated)**
**MATCH: ⚠️ Target has 10 additional columns (most nullable with defaults)**

### Table: ApplicationReviewHistory
Columns (6):
- Id | uniqueidentifier | PK | NOT NULL
- ApplicationId | uniqueidentifier | FK NOT NULL
- ReviewerId | uniqueidentifier | FK NOT NULL
- Status | nvarchar(50) | NOT NULL
- Comments | nvarchar(max) | NULL
- ReviewedAt | datetime2 | NOT NULL
**Record Count: 0 (to be populated)**
**MATCH: ✅ Identical schema**

---

## NEW Tables in MMGTDB2A (Target Only)

These tables either empty or minimal seeding:
1. ApplicationAssignments (7 columns) - 0 records
2. ApplicationDocumentHistory (9 columns) - 0 records
3. AssignmentEscalations (11 columns) - 0 records
4. DocumentTypeAuditLogs (9 columns) - 0 records
5. DocumentTypes (12 columns) - 24 records (seeded)
6. EscalationRules (12 columns) - 0 records
7. ReassignmentHistories (12 columns) - 0 records
8. ReviewerAvailabilities (8 columns) - 0 records
9. ReviewerPerformanceMetrics (19 columns) - 0 records
10. SystemConfigurations (6 columns) - 2 records (seeded)

These are application features that did not exist in the source and will remain empty after migration.

---

## Column Mapping Summary

| Source Table | Target Table | Source Columns | Target Columns | Status | Notes |
|---|---|---|---|---|---|
| Users | Users | 15 | 15 | ✅ Identical | Direct copy, no mapping needed |
| PMICompanies | PMICompanies  | 9 | 9 | ✅ Identical | Direct copy, no mapping needed |
| Applications | Applications | 12 | 24 | ⚠️ Has new cols | Copy 12, set remaining to NULL (nullable) |
| ApplicationDocuments | ApplicationDocuments | 8 | 18 | ⚠️ Has new cols | Copy 8, map IsLegacy=1 (disk), set new cols per defaults |
| ApplicationReviewHistory | ApplicationReviewHistory | 6 | 6 | ✅ Identical | Direct copy, no mapping needed |

---

## Data Migration Strategy

### Phase 1: Core Tables (No Dependencies)
1. **Users** - 110 records
   - Direct INSERT with identity preservation
   - Command: `SET IDENTITY_INSERT Users ON; INSERT INTO MMGTDB2A.dbo.Users SELECT * FROM MMGTDBCOPY.dbo.Users; SET IDENTITY_INSERT Users OFF;`

2. **PMICompanies** - 24 records
   - Direct INSERT with identity preservation
   - Verify no duplicate CompanyCode after migration

### Phase 2: Reference Tables (Depend on Phase 1)
3. **Applications** - 13,789 records
   - Copy from source; set new nullable columns to NULL (or appropriate defaults)
   - Column mapping:
	 * Source → Target: Id → Id, ApplicationNumber → ApplicationNumber, PMICompanyId → PMICompanyId, etc.
	 * New columns: ApplicantId=NULL, LoanAmount=NULL, PropertyAddress=NULL, PropertyValue=NULL, LoanTerm=NULL, InterestRate=NULL, MortgageType=NULL, ApprovalDate=NULL, ClosingDate=NULL, Notes=NULL, AssignedToUserId=NULL, Priority=NULL

### Phase 3: Documents & History (Depend on Phase 2)
4. **ApplicationDocuments** - 14,047 records
   - Special handling: Set IsLegacy=1 (all source files are legacy disk-based)
   - Column mapping:
	 * Source → Target: Id → Id, FileName → FileName, FilePath → FilePath (preserve disk paths), FileType → FileType, FileSize → FileSize, CreatedAt → CreatedAt, ApplicationId → ApplicationId
	 * New columns: DocumentTypeId=NULL, FileContent=NULL (not used for legacy), Version=1, IsLatestVersion=1, ReviewStatus=NULL, ReviewComments=NULL, ReviewedBy=NULL, ReviewedAt=NULL, UploadedBy=NULL, IsLegacy=1
	 * Note: DocumentType column from source (nvarchar) must be mapped to DocumentTypeId (FK) - requires lookup; may default to NULL if type not found in target DocumentTypes

5. **ApplicationReviewHistory** - 31,135 records
   - Direct INSERT with identity preservation

---

## Risk Assessment

| Risk | Severity | Mitigation |
|---|---|---|
| Foreign key violation on ApplicationDocuments.ApplicationId | High | Ensure all Applications copied before documents |
| Foreign key violation on ApplicationReviewHistory.ReviewerId | High | Ensure all Users copied before reviews |
| Identity value conflicts | Medium | Use SET IDENTITY_INSERT ON/OFF properly |
| legacy disk FilePath invalid after migration | Medium | Do NOT move physical files; preserve FilePath strings |
| DocumentType mismatch (source nvarchar vs target FK) | Medium | Map DocumentType names to target DocumentType records or NULL |
| NULL constraint violations on non-nullable new columns | Low | Verify all new columns are nullable or have defaults |
| Duplicate ApplicationNumber | Low | Check constraints after copy |

---

## Pre-Migration Checklist

- [ ] Backup both MMGTDBCOPY and MMGTDB2A
- [ ] Verify record counts in source (Users: 110, PMICompanies: 24, Applications: 13,789, ApplicationDocuments: 14,047, ApplicationReviewHistory: 31,135)
- [ ] Confirm DocumentTypes in target match expected codes (24 seeded)
- [ ] Verify no active connections to MMGTDB2A
- [ ] Disable all foreign key constraints in target database
- [ ] Verify identity seed values in both databases
- [ ] Document ApplicationDocuments.DocumentType values that exist in source
- [ ] Create pre-migration snapshot of target schema

---

## Post-Migration Validation

- [ ] Record count verification (source = target for migrated tables)
- [ ] Foreign key constraint re-enable without errors
- [ ] Application endpoints all functional (load application, list applications, get documents, etc.)
- [ ] Document download/view still works for legacy files
- [ ] New document upload creates BLOB entries correctly
- [ ] Identity values did not change
- [ ] No orphaned records (Applications without PMICompanies, Documents without Applications, etc.)

