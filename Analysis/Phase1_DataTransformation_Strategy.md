# Phase 1 - Data Transformation Strategy for New Columns

## Overview

Target database (MMGTDB2A) has introduced new columns in Applications and ApplicationDocuments tables. Source data must be mapped to accommodate these new columns during migration.

---

## New Columns: Applications Table

### Summary
- 12 new columns added to MMGTDB2A Applications table
- All are nullable or have defaults
- No data exists in source to populate these fields
- Strategy: INSERT NULL or use appropriate default values

### Detailed New Columns in Applications

| Column Name | Type | Nullable? | Default | Migration Strategy | Notes |
|---|---|---|---|---|---|
| ApplicantId | uniqueidentifier | YES (NULL) | NULL | Set to NULL | Future: link to Users for front-end applicant info |
| LoanAmount | decimal(18,2) | YES (NULL) | NULL | Set to NULL | Financial field, not tracked in legacy system |
| PropertyAddress | nvarchar(500) | YES (NULL) | NULL | Set to NULL | Property info not tracked in legacy system |
| PropertyValue | decimal(18,2) | YES (NULL) | NULL | Set to NULL | Property valuation not tracked in legacy system |
| LoanTerm | int | YES (NULL) | NULL | Set to NULL | Loan duration in months not tracked |
| InterestRate | decimal(10,4) | YES (NULL) | NULL | Set to NULL | Interest rate not tracked in legacy system |
| MortgageType | nvarchar(50) | YES (NULL) | NULL | Set to NULL | Mortgage type/product not tracked |
| ApprovalDate | datetime2 | YES (NULL) | NULL | Use ApprovedAt value if exists, else NULL | Maps to legacy ApprovedAt |
| ClosingDate | datetime2 | YES (NULL) | NULL | Set to NULL | Closing date not tracked in legacy system |
| Notes | nvarchar(max) | YES (NULL) | NULL | Set to NULL | Additional notes field for future use |
| AssignedToUserId | uniqueidentifier | YES (NULL) | NULL | Set to NULL | For future assignment/routing feature |
| Priority | int | YES (NULL) | NULL | Set to NULL | Priority level for workload management |

### Migration SQL for Applications
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	Id,
	ApplicationNumber,
	PMICompanyId,
	Status,
	SubmissionDate,
	CreatedAt,
	UpdatedAt,
	ApprovedAt,
	RejectionReason,
	TotalDocuments,
	ReviewedDocuments,
	PendingDocuments,
	ApplicantId,
	LoanAmount,
	PropertyAddress,
	PropertyValue,
	LoanTerm,
	InterestRate,
	MortgageType,
	ApprovalDate,
	ClosingDate,
	Notes,
	AssignedToUserId,
	Priority
)
SELECT 
	Id,
	ApplicationNumber,
	PMICompanyId,
	Status,
	SubmissionDate,
	CreatedAt,
	UpdatedAt,
	ApprovedAt,
	RejectionReason,
	TotalDocuments,
	ReviewedDocuments,
	PendingDocuments,
	NULL AS ApplicantId,                    -- New field, use NULL
	NULL AS LoanAmount,                     -- New field, use NULL
	NULL AS PropertyAddress,                -- New field, use NULL
	NULL AS PropertyValue,                  -- New field, use NULL
	NULL AS LoanTerm,                       -- New field, use NULL
	NULL AS InterestRate,                   -- New field, use NULL
	NULL AS MortgageType,                   -- New field, use NULL
	ApprovedAt AS ApprovalDate,             -- Mapping: ApprovedAt → ApprovalDate
	NULL AS ClosingDate,                    -- New field, use NULL
	NULL AS Notes,                          -- New field, use NULL
	NULL AS AssignedToUserId,               -- New field, use NULL
	NULL AS Priority                        -- New field, use NULL
FROM MMGTDBCOPY.dbo.Applications;
```

---

## New Columns: ApplicationDocuments Table

### Summary
- 10 new columns added to MMGTDB2A ApplicationDocuments table
- Most are nullable; some have defaults
- Critical decision: Set IsLegacy=1 for all migrated documents (disk-based storage)
- FileContent remains NULL (BLOB only for new uploads via API)
- Version tracking fields set to default values

### Detailed New Columns in ApplicationDocuments

| Column Name | Type | Nullable? | Default | Migration Strategy | Notes |
|---|---|---|---|---|---|
| DocumentTypeId | uniqueidentifier | YES (NULL) | NULL | NULL (or map if DocumentType exists) | FK to DocumentTypes; source has DocumentType (string), not GUID |
| FileContent | varbinary(max) | YES (NULL) | NULL | Set to NULL | BLOB storage only for new uploads; legacy files use FilePath |
| Version | int | NO | 1 | Set to 1 | Default version for all migrated documents |
| IsLatestVersion | bit | NO | 1 | Set to 1 (true) | All migrated documents are latest (only version) |
| ReviewStatus | nvarchar(50) | YES (NULL) | NULL | Set to NULL | Review status not tracked per-document in legacy |
| ReviewComments | nvarchar(max) | YES (NULL) | NULL | Set to NULL | Review comments per-document not in legacy |
| ReviewedBy | uniqueidentifier | YES (NULL) | NULL | Set to NULL | Reviewed by user tracking not per-document |
| ReviewedAt | datetime2 | YES (NULL) | NULL | Set to NULL | Review timestamp per-document not in legacy |
| UploadedBy | uniqueidentifier | YES (NULL) | NULL | Set to NULL | Upload author not tracked in legacy system |
| IsLegacy | bit | NO | 0 | **Set to 1 (true)** | 🔑 **CRITICAL: Flag all migrated docs as legacy disk-files** |

### DocumentType Mapping Challenge

**Problem:** 
- Source ApplicationDocuments.DocumentType is nvarchar(100) - contains string values like "NIL", "Title Deed", etc.
- Target ApplicationDocuments.DocumentTypeId is uniqueidentifier FK to DocumentTypes table
- Target DocumentTypes has 24 seeded records with codes/names

**Solution Strategy:**
1. Query target DocumentTypes to understand available types
2. Attempt to match source DocumentType values to target DocumentType codes or names
3. If match found: use matched DocumentTypeId
4. If match NOT found: Set DocumentTypeId to NULL (acceptable, field is nullable)

**Query to understand DocumentTypes mapping:**
```sql
USE MMGTDB2A;
SELECT Id, Code, Name FROM DocumentTypes ORDER BY Code;
```

Expected output: 24 DocumentType records with IDs and Names (e.g., "NATIONAL_ID", "TITLE_DEED", etc.)

**Source DocumentType values (sample from legacy system):**
```
NIL
Title Deed
Passport
Marriage Certificate
Divorce Certificate
POA
...etc
```

**Mapping Decision:**
- Create a CASE statement or lookup table in migration script
- IF source DocumentType matches target DocumentType.Code or Name THEN use matched Id
- ELSE set to NULL

### Migration SQL for ApplicationDocuments

```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	Id,
	FileName,
	FilePath,
	FileType,
	DocumentTypeId,
	FileContent,
	Version,
	IsLatestVersion,
	ReviewStatus,
	ReviewComments,
	ReviewedBy,
	ReviewedAt,
	UploadedBy,
	FileSize,
	CreatedAt,
	ApplicationId,
	IsLegacy
)
SELECT 
	Id,
	FileName,
	FilePath,
	FileType,
	-- DocumentTypeId: Map source DocumentType string to target DocumentTypeId
	CASE 
		WHEN ad.DocumentType = 'NIL' THEN (SELECT Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code = 'NIL' OR Name LIKE '%NIL%')
		WHEN ad.DocumentType = 'Title Deed' THEN (SELECT Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%TITLE%' OR Name LIKE '%TITLE%')
		WHEN ad.DocumentType = 'National ID' THEN (SELECT Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code = 'NATIONAL_ID' OR Name LIKE '%NATIONAL%')
		-- ... add more mappings based on actual source values and target DocumentTypes
		ELSE NULL  -- Default to NULL if no match found
	END AS DocumentTypeId,
	NULL AS FileContent,                    -- BLOB not used for legacy files
	1 AS Version,                           -- Default version
	1 AS IsLatestVersion,                   -- All migrated docs are latest version
	NULL AS ReviewStatus,                   -- No per-document review in legacy
	NULL AS ReviewComments,                 -- No per-document review comments
	NULL AS ReviewedBy,                     -- No per-document reviewer tracking
	NULL AS ReviewedAt,                     -- No per-document review timestamp
	NULL AS UploadedBy,                     -- Upload user not tracked in legacy
	FileSize,
	CreatedAt,
	ApplicationId,
	1 AS IsLegacy                           -- 🔑 CRITICAL: Mark as legacy disk-based file
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad;
```

---

## Critical Decisions for Data Migration

### Decision 1: IsLegacy Flag
**Setting: IsLegacy = 1 for ALL migrated ApplicationDocuments**

Rationale:
- All ApplicationDocuments in source are disk-based (FilePath populated)
- New system supports BLOB storage (FileContent) for new uploads
- IsLegacy flag distinguishes old disk files from new BLOB files
- FileService.cs checks IsLegacy and prevents replace/delete on legacy docs (protection)
- Downloads can read from either FilePath (legacy) or FileContent (new BLOB)

**Implication:**
- Existing files will be protected from modification (read-only)
- Users can still view/download legacy documents
- New uploads after migration will have IsLegacy=0 and use FileContent

### Decision 2: Version Tracking
**Setting: Version = 1, IsLatestVersion = 1 for ALL migrated ApplicationDocuments**

Rationale:
- Legacy system doesn't track document versions
- All migrated documents are "version 1"
- IsLatestVersion=1 indicates no newer versions exist
- If document replaced after migration, replacement increments Version and creates new record

**Implication:**
- Migrated documents won't show version history (expected)
- New replacements will have Version=2, 3, etc.
- Document history preserved but starting at v1

### Decision 3: FileContent (BLOB) = NULL
**Setting: FileContent = NULL for ALL migrated ApplicationDocuments**

Rationale:
- Legacy files are on disk (FilePath contains path)
- BLOB storage (FileContent) only used for new uploads via API
- No physical file migration needed (files stay on disk)
- FileService.DownloadFileAsync prioritizes FileContent, falls back to FilePath

**Implication:**
- Do NOT attempt to read physical files and populate FileContent
- Legacy files accessible via FilePath
- New uploads after migration populate FileContent
- Hybrid storage model maintained

### Decision 4: ApprovalDate (Application)
**Mapping: Source ApprovedAt → Target ApprovalDate**

Rationale:
- Target Applications table has new ApprovalDate column
- Source has ApprovedAt timestamp when application was approved
- Both represent same business data (approval timestamp)
- No new data to populate; same value serves both fields

**Implication:**
- ApprovalDate will be populated with existing ApprovedAt values
- Applications that were never approved will have ApprovalDate=NULL
- Both fields now represent approval timestamp

---

## Document Type Mapping Task

### Pre-Migration Action Required:

Before running full migration, must determine DocumentType mappings:

1. **Query source system for all unique DocumentType values:**
```sql
SELECT DISTINCT DocumentType FROM MMGTDBCOPY.dbo.ApplicationDocuments ORDER BY DocumentType;
```

2. **Query target system for DocumentTypes:**
```sql
USE MMGTDB2A;
SELECT Id, Code, Name, DisplayOrder FROM DocumentTypes ORDER BY DisplayOrder;
```

3. **Create mapping table** (manual or automated):
```
Source DocumentType → Target DocumentType Id/Code

Example:
NIL → 00000000-0000-0000-0000-000000000000 (assume this is NIL in target)
Title Deed → 11111111-1111-1111-1111-111111111111 (assume this is TITLE_DEED)
...etc
```

4. **Update migration script** with actual mappings

### Alternative: Use NULL (Simpler)
If mapping is complex and error-prone:
- Set DocumentTypeId=NULL for all migrated documents
- Document type information is preserved in ApplicationDocuments.DocumentType column (string)
- New documents uploaded via API will have proper DocumentTypeId
- Can add mapping logic later via post-migration script

---

## Transformation Summary: SQL Pseudo-Code

```sql
-- Disable FK constraints in target
ALTER TABLE Users NOCHECK CONSTRAINT ALL;
ALTER TABLE PMICompanies NOCHECK CONSTRAINT ALL;
ALTER TABLE Applications NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationDocuments NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationReviewHistory NOCHECK CONSTRAINT ALL;

-- Clear target tables (remove seeded development data)
DELETE FROM ApplicationReviewHistory;
DELETE FROM ApplicationDocuments;
DELETE FROM Applications;
DELETE FROM Users;
DELETE FROM PMICompanies;

-- Phase 1: Copy lookup tables (no transformations needed)
INSERT INTO MMGTDB2A.dbo.Users
SELECT * FROM MMGTDBCOPY.dbo.Users;

INSERT INTO MMGTDB2A.dbo.PMICompanies
SELECT * FROM MMGTDBCOPY.dbo.PMICompanies;

-- Phase 2: Copy Applications with new column transformations
INSERT INTO MMGTDB2A.dbo.Applications
(...column list...)
SELECT 
	-- Source columns pass through unchanged
	Id, ApplicationNumber, PMICompanyId, Status, SubmissionDate, CreatedAt, UpdatedAt, ApprovedAt, RejectionReason, TotalDocuments, ReviewedDocuments, PendingDocuments,
	-- New columns: mostly NULL, except ApprovalDate from ApprovedAt
	NULL, NULL, NULL, NULL, NULL, NULL, NULL, ApprovedAt, NULL, NULL, NULL, NULL
FROM MMGTDBCOPY.dbo.Applications;

-- Phase 3: Copy ApplicationDocuments with new column transformations
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments
(...column list...)
SELECT 
	-- Source columns pass through
	Id, FileName, FilePath, FileType,
	-- New columns: DocumentTypeId (NULL), FileContent (NULL), Version (1), IsLatestVersion (1), etc.
	NULL, NULL, 1, 1, NULL, NULL, NULL, NULL, NULL,
	-- Source columns continue
	FileSize, CreatedAt, ApplicationId,
	-- Critical: IsLegacy = 1 (LEGACY DISK FILE)
	1
FROM MMGTDBCOPY.dbo.ApplicationDocuments;

-- Phase 4: Copy ApplicationReviewHistory (no new columns)
INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory
SELECT * FROM MMGTDBCOPY.dbo.ApplicationReviewHistory;

-- Re-enable FK constraints and validate
ALTER TABLE Users CHECK CONSTRAINT ALL;
ALTER TABLE PMICompanies CHECK CONSTRAINT ALL;
ALTER TABLE Applications CHECK CONSTRAINT ALL;
ALTER TABLE ApplicationDocuments CHECK CONSTRAINT ALL;
ALTER TABLE ApplicationReviewHistory CHECK CONSTRAINT ALL;
```

---

## Data Quality Validation Post-Migration

### Verify Applications Transformation
```sql
SELECT 
	COUNT(*) AS TotalCount,
	COUNT(CASE WHEN ApplicantId IS NULL THEN 1 END) AS ApplicantIdNULL,
	COUNT(CASE WHEN ApprovalDate IS NOT NULL THEN 1 END) AS ApprovalDatePopulated,
	COUNT(CASE WHEN ApprovedAt IS NOT NULL AND ApprovalDate IS NOT NULL THEN 1 END) AS ApprovalDateMatches
FROM MMGTDB2A.dbo.Applications;
```

Expected: 13,789 rows, all new columns NULL except ApprovalDate populated where ApprovedAt exists.

### Verify ApplicationDocuments Transformation
```sql
SELECT 
	COUNT(*) AS TotalCount,
	COUNT(CASE WHEN IsLegacy = 1 THEN 1 END) AS LegacyFlagSet,
	COUNT(CASE WHEN FileContent IS NULL THEN 1 END) AS FileContentNULL,
	COUNT(CASE WHEN Version = 1 THEN 1 END) AS VersionOne,
	COUNT(CASE WHEN IsLatestVersion = 1 THEN 1 END) AS IsLatestTrue
FROM MMGTDB2A.dbo.ApplicationDocuments;
```

Expected: 14,047 rows, all with IsLegacy=1, FileContent=NULL, Version=1, IsLatestVersion=1.

---

## Summary: Transformation Strategy

| Table | Change Type | New Columns Handled | Validation |
|---|---|---|---|
| Users | No transformation | 0 new | Direct copy, 110 records |
| PMICompanies | No transformation | 0 new | Direct copy, 24 records |
| Applications | Add new fields | 12 new (all NULL/mapped) | ApprovalDate = ApprovedAt; others NULL |
| ApplicationDocuments | Add new fields | 10 new (critical transformations) | IsLegacy=1, FileContent=NULL, Version=1, IsLatestVersion=1 |
| ApplicationReviewHistory | No transformation | 0 new | Direct copy, 31,135 records |

