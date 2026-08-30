# Phase 2: Data Transformation Reference & Mapping Guide

## Overview

This document provides the complete column-by-column transformation rules for migrating data from MMGTDBCOPY to MMGTDB2A.

**Total Records**: 59,105  
**Tables**: 5  
**New Columns**: 22 (12 in Applications, 10 in ApplicationDocuments)  
**Critical Transformation**: IsLegacy=1 for all 14,047 ApplicationDocuments

---

## Table 1: Users (110 records)

### Migration Strategy
**Direct Copy** - No transformations needed. All columns map 1:1 between source and target.

| Source Column | Target Column | Transformation | Notes |
|---|---|---|---|
| Id | Id | Direct copy | GUID primary key preserved |
| UserName | UserName | Direct copy | Unique identifier |
| Email | Email | Direct copy | Email address |
| PasswordHash | PasswordHash | Direct copy | Hashed password |
| FirstName | FirstName | Direct copy | User name |
| LastName | LastName | Direct copy | User name |
| Role | Role | Direct copy | CLIENT, PMI, SERVICE_MANAGER, etc. |
| IsActive | IsActive | Direct copy | Boolean flag |
| CreatedAt | CreatedAt | Direct copy | UTC timestamp |
| UpdatedAt | UpdatedAt | Direct copy | UTC timestamp |

**SQL**:
```sql
INSERT INTO MMGTDB2A.dbo.Users (
	Id, UserName, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt, UpdatedAt
)
SELECT 
	Id, UserName, Email, PasswordHash, FirstName, LastName, Role, IsActive, CreatedAt, UpdatedAt
FROM MMGTDBCOPY.dbo.Users
```

**Expected Result**: 110 rows

---

## Table 2: PMICompanies (24 records)

### Migration Strategy
**Direct Copy** - No transformations needed. All columns map 1:1.

| Source Column | Target Column | Transformation | Notes |
|---|---|---|---|
| Id | Id | Direct copy | GUID primary key |
| CompanyName | CompanyName | Direct copy | Company name |
| CompanyCode | CompanyCode | Direct copy | Unique company code |
| Email | Email | Direct copy | Company email |
| PhoneNumber | PhoneNumber | Direct copy | Contact number |
| IsActive | IsActive | Direct copy | Boolean flag |
| CreatedAt | CreatedAt | Direct copy | UTC timestamp |
| UpdatedAt | UpdatedAt | Direct copy | UTC timestamp |

**SQL**:
```sql
INSERT INTO MMGTDB2A.dbo.PMICompanies (
	Id, CompanyName, CompanyCode, Email, PhoneNumber, IsActive, CreatedAt, UpdatedAt
)
SELECT 
	Id, CompanyName, CompanyCode, Email, PhoneNumber, IsActive, CreatedAt, UpdatedAt
FROM MMGTDBCOPY.dbo.PMICompanies
```

**Expected Result**: 24 rows

---

## Table 3: Applications (13,789 records) - WITH TRANSFORMATIONS

### Migration Strategy
**Partial Copy + Transformation** - 10 columns copied directly, 12 new columns created with defaults/mappings, 1 column transformed (ApprovedAt → ApprovalDate).

### Copied Columns (Direct 1:1)

| Source | Target | Value | Notes |
|--------|--------|-------|-------|
| Id | Id | Direct | GUID primary key |
| ApplicationNumber | ApplicationNumber | Direct | Application ID |
| PMICompanyId | PMICompanyId | Direct | FK to PMICompanies |
| UserId | UserId | Direct | FK to Users (application owner) |
| Status | Status | Direct | draft, submitted, rejected, approved, etc. |
| SubmissionDate | SubmissionDate | Direct | Date app submitted |
| CreatedAt | CreatedAt | Direct | Creation timestamp |
| UpdatedAt | UpdatedAt | Direct | Last update timestamp |
| ApprovedAt | ApprovedAt | Direct | Approval timestamp (if approved) |
| RejectionReason | RejectionReason | Direct | Reason if rejected |
| TotalDocuments | TotalDocuments | Direct | Count of documents |
| ReviewedDocuments | ReviewedDocuments | Direct | Count of reviewed docs |
| PendingDocuments | PendingDocuments | Direct | Count pending review |

### Transformed Columns

| Source | Target | Transformation | Value | Rationale |
|--------|--------|---|---|---|
| ApprovedAt | ApprovalDate | Map value | Same as ApprovedAt | New column for approval date |

**Transformation Logic**:
```sql
ApprovedAt AS ApprovalDate  -- Copies timestamp if application approved, else NULL
```

### New Columns (Set to NULL)

| Target Column | Type | Value | Rationale |
|---|---|---|---|
| ApplicantId | uniqueidentifier | NULL | Future: link to Users for applicant info |
| LoanAmount | decimal(18,2) | NULL | Not tracked in legacy system |
| PropertyAddress | nvarchar(500) | NULL | Not tracked in legacy system |
| PropertyValue | decimal(18,2) | NULL | Not tracked in legacy system |
| LoanTerm | int | NULL | Not tracked in legacy system |
| InterestRate | decimal(10,4) | NULL | Not tracked in legacy system |
| MortgageType | nvarchar(50) | NULL | Not tracked in legacy system |
| ClosingDate | datetime2 | NULL | Not tracked in legacy system |
| Notes | nvarchar(max) | NULL | Future use |
| AssignedToUserId | uniqueidentifier | NULL | Future assignment feature |
| Priority | int | NULL | Future workload management |

### Complete SQL Transformation

```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	Id,
	ApplicationNumber,
	PMICompanyId,
	UserId,
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
	UserId,
	Status,
	SubmissionDate,
	CreatedAt,
	UpdatedAt,
	ApprovedAt,
	RejectionReason,
	TotalDocuments,
	ReviewedDocuments,
	PendingDocuments,
	NULL AS ApplicantId,
	NULL AS LoanAmount,
	NULL AS PropertyAddress,
	NULL AS PropertyValue,
	NULL AS LoanTerm,
	NULL AS InterestRate,
	NULL AS MortgageType,
	ApprovedAt AS ApprovalDate,
	NULL AS ClosingDate,
	NULL AS Notes,
	NULL AS AssignedToUserId,
	NULL AS Priority
FROM MMGTDBCOPY.dbo.Applications
```

**Expected Result**: 13,789 rows

---

## Table 4: ApplicationDocuments (14,047 records) - WITH CRITICAL TRANSFORMATIONS

### Migration Strategy
**Partial Copy + Critical Transformations** - 7 columns copied directly, 10 new columns created with defaults, 1 critical flag set (IsLegacy=1).

### Copied Columns (Direct 1:1)

| Source | Target | Value | Notes |
|--------|--------|-------|-------|
| Id | Id | Direct | GUID primary key |
| ApplicationId | ApplicationId | Direct | FK to Applications |
| FileName | FileName | Direct | Original file name |
| FilePath | FilePath | Direct | **CRITICAL: Disk path preserved** |
| FileType | FileType | Direct | MIME type (application/pdf) |
| FileSize | FileSize | Direct | File size in bytes |
| CreatedAt | CreatedAt | Direct | Upload timestamp |

### Transformed Columns - DocumentType Mapping

| Source | Target | Transformation | Strategy |
|--------|--------|---|---|
| DocumentType (string) | DocumentTypeId (GUID) | Match to DocumentTypes table | Query-based matching |

**Transformation Logic**:
```sql
CASE 
	-- Match source string to target DocumentType code/name
	WHEN ad.DocumentType LIKE '%NATIONAL%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%NATIONAL%')
	WHEN ad.DocumentType LIKE '%PASSPORT%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%PASSPORT%')
	WHEN ad.DocumentType LIKE '%TITLE%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%TITLE%')
	WHEN ad.DocumentType LIKE '%BANK%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%BANK%')
	WHEN ad.DocumentType LIKE '%INCOME%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%INCOME%')
	ELSE NULL  -- If no match, set to NULL (acceptable)
END AS DocumentTypeId
```

**Matching Examples**:
- Source: "National ID" → Target: NATIONAL_IDENTITY_CARD
- Source: "Passport" → Target: INTERNATIONAL_PASSPORT
- Source: "Title Deed" → Target: TITLE_DEEDS
- Source: "Proof of Income" → Target: PROOF_OF_INCOME
- Unmatched → NULL

### New/Default Columns

| Target Column | Type | Value | Strategy | Rationale |
|---|---|---|---|---|
| **FileContent** | varbinary(max) | NULL | Set NULL | BLOB storage only for new uploads; legacy uses FilePath |
| **Version** | int | 1 | Default | All migrated docs = version 1 |
| **IsLatestVersion** | bit | 1 (true) | Default | Only version migrated = latest |
| **ReviewStatus** | nvarchar(50) | 'Pending' | Default | No review status in legacy |
| **ReviewComments** | nvarchar(max) | NULL | Set NULL | No per-document comments |
| **ReviewedBy** | uniqueidentifier | NULL | Set NULL | No per-document reviewer |
| **ReviewedAt** | datetime2 | NULL | Set NULL | No per-document review timestamp |
| **UploadedBy** | uniqueidentifier | NULL | Set NULL | Upload user not tracked |
| **IsLegacy** | bit | **1 (true)** | **Set to 1** | **🔴 CRITICAL: Marks as legacy disk file** |

### CRITICAL DECISION: IsLegacy = 1

**All 14,047 migrated ApplicationDocuments are marked IsLegacy = 1**

**Why**:
- All source documents are disk-based (FilePath populated)
- New system supports BLOB storage (FileContent for new uploads)
- IsLegacy flag distinguishes old disk files from new BLOB files
- FileService protects legacy documents (read-only)
- Download logic transparently reads from FilePath

**Impact**:
- Existing files will be protected from modification (read-only)
- Users can still view/download legacy documents
- New uploads after migration: IsLegacy=0, FileContent populated

**Verification**:
```sql
SELECT 
	COUNT(*) as Total,
	SUM(CASE WHEN IsLegacy = 1 THEN 1 ELSE 0 END) as [IsLegacy=1],
	SUM(CASE WHEN IsLegacy = 0 THEN 1 ELSE 0 END) as [IsLegacy=0]
FROM MMGTDB2A.dbo.ApplicationDocuments
-- Expected: Total=14047, IsLegacy=1=14047, IsLegacy=0=0
```

### Complete SQL Transformation

```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	Id,
	ApplicationId,
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
	CreatedAt,
	UploadedBy,
	FileSize,
	IsLegacy
)
SELECT 
	ad.Id,
	ad.ApplicationId,
	ad.FileName,
	ad.FilePath,
	ad.FileType,
	-- DocumentType mapping: string → GUID
	CASE 
		WHEN dt.Id IS NOT NULL THEN dt.Id
		WHEN ad.DocumentType LIKE '%NATIONAL%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%NATIONAL%')
		WHEN ad.DocumentType LIKE '%PASSPORT%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%PASSPORT%')
		WHEN ad.DocumentType LIKE '%TITLE%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%TITLE%')
		WHEN ad.DocumentType LIKE '%BANK%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%BANK%')
		WHEN ad.DocumentType LIKE '%INCOME%' THEN (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes WHERE Code LIKE '%INCOME%')
		ELSE NULL
	END AS DocumentTypeId,
	NULL AS FileContent,
	1 AS Version,
	1 AS IsLatestVersion,
	'Pending' AS ReviewStatus,
	NULL AS ReviewComments,
	NULL AS ReviewedBy,
	NULL AS ReviewedAt,
	ad.CreatedAt,
	NULL AS UploadedBy,
	ad.FileSize,
	1 AS IsLegacy  -- 🔴 CRITICAL TRANSFORMATION
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON ad.DocumentType = dt.Code
```

**Expected Result**: 14,047 rows (all with IsLegacy=1)

---

## Table 5: ApplicationReviewHistory (31,135 records)

### Migration Strategy
**Direct Copy** - No transformations needed. All columns map 1:1.

| Source Column | Target Column | Transformation | Notes |
|---|---|---|---|
| Id | Id | Direct copy | GUID primary key |
| ApplicationId | ApplicationId | Direct copy | FK to Applications |
| UserId | UserId | Direct copy | FK to Users |
| ActionType | ActionType | Direct copy | Action performed |
| Notes | Notes | Direct copy | Action notes |
| CreatedAt | CreatedAt | Direct copy | UTC timestamp |

**SQL**:
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
	Id, ApplicationId, UserId, ActionType, Notes, CreatedAt
)
SELECT 
	Id, ApplicationId, UserId, ActionType, Notes, CreatedAt
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory
```

**Expected Result**: 31,135 rows

---

## Summary: Transformation Rules

### Rules Applied

| Rule # | Table | Rule | Impact |
|--------|-------|------|--------|
| 1 | Applications | 12 new columns set to NULL | Each application gets 12 new nullable fields |
| 2 | Applications | ApprovedAt → ApprovalDate | Approval date mapped to new column |
| 3 | ApplicationDocuments | DocumentType (string) → DocumentTypeId (GUID) | String type codes mapped to GUID references |
| 4 | ApplicationDocuments | FileContent = NULL | Legacy files use FilePath, not BLOB |
| 5 | ApplicationDocuments | Version = 1 | All migrated docs start at version 1 |
| 6 | ApplicationDocuments | IsLatestVersion = 1 | All are latest (only version migrated) |
| 7 | ApplicationDocuments | ReviewStatus = 'Pending' | Default review status |
| 8 | ApplicationDocuments | ReviewComments = NULL | No per-document review comments |
| 9 | ApplicationDocuments | ReviewedBy = NULL | No per-document reviewer tracking |
| 10 | ApplicationDocuments | ReviewedAt = NULL | No per-document review timestamp |
| 11 | ApplicationDocuments | UploadedBy = NULL | Upload user not available |
| 12 | ApplicationDocuments | **IsLegacy = 1** | **🔴 CRITICAL: All migrated docs are legacy** |

### Transformation Decision Matrix

```
If... Then...
───────────────────────────────────────────
Column exists in source → Copy directly
Column is new → Set to NULL or default
Column needs mapping → Use CASE statement or JOIN
Column is critical flag → Set IsLegacy=1 for all docs
Column is archive (ApprovedAt) → Map to new name (ApprovalDate)
Column has new domain (DocumentType) → Map string → GUID via JOIN
```

---

## Validation: Post-Migration Checks

After migration, verify transformations:

### Check 1: IsLegacy Flag
```sql
SELECT COUNT(*) as Total, 
	   SUM(CASE WHEN IsLegacy=1 THEN 1 ELSE 0 END) as [Expected=14047]
FROM MMGTDB2A.dbo.ApplicationDocuments
-- Should return: Total=14047, Expected=14047
```

### Check 2: FilePath Preserved
```sql
SELECT COUNT(*) as [Should_be_0]
FROM MMGTDB2A.dbo.ApplicationDocuments
WHERE IsLegacy=1 AND FilePath IS NULL
-- Should return: 0 rows
```

### Check 3: FileContent is NULL
```sql
SELECT COUNT(*) as [Should_be_0]
FROM MMGTDB2A.dbo.ApplicationDocuments
WHERE IsLegacy=1 AND FileContent IS NOT NULL
-- Should return: 0 rows
```

### Check 4: Version Tracking
```sql
SELECT COUNT(DISTINCT Version) as [Expected=1]
FROM MMGTDB2A.dbo.ApplicationDocuments
WHERE IsLegacy=1
-- Should return: 1 (all version 1)
```

### Check 5: DocumentType Mapping
```sql
SELECT 
	COUNT(*) as Total,
	COUNT(DocumentTypeId) as [With_ID],
	SUM(CASE WHEN DocumentTypeId IS NULL THEN 1 ELSE 0 END) as [Null_ID]
FROM MMGTDB2A.dbo.ApplicationDocuments
-- Review ratio of NULL values vs populated
```

### Check 6: Approval Date Transform
```sql
SELECT COUNT(*) as [Should_match]
FROM MMGTDB2A.dbo.Applications
WHERE ApprovalDate = ApprovedAt
-- Should return: count of approved applications
```

---

## Rollback Strategy

If migration fails or data inconsistent:

**Complete Rollback**:
```sql
-- Restore from backup (MMGTDB2A backup taken beforehand)
RESTORE DATABASE MMGTDB2A FROM DISK = '\\backup\MMGTDB2A_pre_phase2.bak'
```

**Partial Rollback** (if tables partially migrated):
```sql
-- Delete specific table
DELETE FROM MMGTDB2A.dbo.ApplicationDocuments WHERE IsLegacy=1
DELETE FROM MMGTDB2A.dbo.Applications
DELETE FROM MMGTDB2A.dbo.PMICompanies
DELETE FROM MMGTDB2A.dbo.Users
-- Then restart migration
```

---

## Migration Execution Checklist

**Pre-Migration**:
- [ ] MMGTDB2A backup exists
- [ ] IsLegacy column exists in ApplicationDocuments
- [ ] All 24 DocumentTypes seeded
- [ ] Source MMGTDBCOPY accessible
- [ ] Target disk space available (~500MB+)

**During Migration**:
- [ ] Execute Phase2_MigrationExecution.sql
- [ ] Monitor for FK constraint violations
- [ ] Check for orphaned records
- [ ] Verify row counts match

**Post-Migration**:
- [ ] Run 5 validation checks above
- [ ] Test legacy document download
- [ ] Test new document upload (BLOB)
- [ ] Review ApplicationReviewHistory integrity
- [ ] Compare source vs target record counts

---

**Transformation Document Status**: ✅ Ready for Execution  
**Total Records to Transform**: 59,105  
**Critical Transformation**: IsLegacy=1 for 14,047 ApplicationDocuments  
**Execution Script**: Phase2_MigrationExecution.sql
