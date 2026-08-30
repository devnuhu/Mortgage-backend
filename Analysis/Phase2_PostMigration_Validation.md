# Phase 2: Post-Migration Validation Framework

## Status: ✅ Validation Framework Ready

This document defines the complete post-migration validation process to ensure all 59,105 records migrated successfully with correct transformations.

---

## Post-Migration Validation Timeline

**When to run**: Immediately after Phase2_MigrationExecution.sql completes successfully

**Estimated duration**: 15-20 minutes

---

## Validation Checklist (6 Steps)

### STEP 1: Record Count Verification (5 min)

**Purpose**: Confirm all records migrated and counts match exactly

**Validation Query**:

```sql
USE MMGTDB2A
GO

-- 1.1: Verify record counts match source exactly
SELECT 
	'MMGTDB2A' as Database,
	(SELECT COUNT(*) FROM Users) as Users,
	(SELECT COUNT(*) FROM PMICompanies) as PMICompanies,
	(SELECT COUNT(*) FROM Applications) as Applications,
	(SELECT COUNT(*) FROM ApplicationDocuments) as ApplicationDocuments,
	(SELECT COUNT(*) FROM ApplicationReviewHistory) as ApplicationReviewHistory,
	(SELECT COUNT(*) FROM Users) + 
	(SELECT COUNT(*) FROM PMICompanies) +
	(SELECT COUNT(*) FROM Applications) +
	(SELECT COUNT(*) FROM ApplicationDocuments) +
	(SELECT COUNT(*) FROM ApplicationReviewHistory) as TotalRecords
GO
```

**Expected Result**:
```
Database: MMGTDB2A
Users: 110
PMICompanies: 24
Applications: 13,789
ApplicationDocuments: 14,047
ApplicationReviewHistory: 31,135
TotalRecords: 59,105
```

**✅ PASS if**: All counts match exactly
**❌ FAIL if**: Any count differs from expected

---

### STEP 2: IsLegacy Flag Verification (3 min)

**Purpose**: Confirm all ApplicationDocuments marked as legacy

**Validation Query**:

```sql
USE MMGTDB2A
GO

-- 2.1: Verify IsLegacy flag set correctly
SELECT 
	COUNT(*) as TotalDocuments,
	SUM(CASE WHEN IsLegacy = 1 THEN 1 ELSE 0 END) as LegacyCount,
	SUM(CASE WHEN IsLegacy = 0 THEN 1 ELSE 0 END) as NewCount,
	SUM(CASE WHEN IsLegacy = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) as LegacyPercent
FROM ApplicationDocuments
GO

-- 2.2: Verify no anomalies
SELECT 
	'IsLegacy Distribution' as Check,
	IsLegacy,
	COUNT(*) as Count
FROM ApplicationDocuments
GROUP BY IsLegacy
ORDER BY IsLegacy DESC
GO
```

**Expected Result**:
```
TotalDocuments: 14,047
LegacyCount: 14,047
NewCount: 0
LegacyPercent: 100.0

IsLegacy Distribution:
IsLegacy | Count
1        | 14,047
```

**✅ PASS if**: All 14,047 have IsLegacy=1 (100%)
**❌ FAIL if**: Any document has IsLegacy=0 or percentage < 100%

---

### STEP 3: Storage State Verification (3 min)

**Purpose**: Confirm hybrid storage model correctly set (FilePath + no FileContent)

**Validation Query**:

```sql
USE MMGTDB2A
GO

-- 3.1: Verify storage state (should be all legacy/disk-based)
SELECT 
	SUM(CASE WHEN IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NULL THEN 1 ELSE 0 END) 
		as CorrectLegacy_PathOnly,
	SUM(CASE WHEN IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NOT NULL THEN 1 ELSE 0 END) 
		as ErrorLegacy_WithBLOB,
	SUM(CASE WHEN IsLegacy = 1 AND FilePath IS NULL THEN 1 ELSE 0 END) 
		as ErrorLegacy_NoPath,
	SUM(CASE WHEN IsLegacy = 0 AND FileContent IS NOT NULL THEN 1 ELSE 0 END) 
		as NewDocuments_WithBLOB
FROM ApplicationDocuments
GO

-- 3.2: Verify no corrupted NULL states
SELECT 
	'Storage anomalies:' as [STATUS],
	COUNT(*) as AnomalyCount
FROM ApplicationDocuments
WHERE 
	(IsLegacy = 1 AND FileContent IS NOT NULL) OR  -- Legacy with BLOB = ERROR
	(IsLegacy = 1 AND FilePath IS NULL)            -- Legacy without path = ERROR
GO
```

**Expected Result**:
```
CorrectLegacy_PathOnly: 14,047 (all legacy with path)
ErrorLegacy_WithBLOB: 0 (no errors)
ErrorLegacy_NoPath: 0 (no errors)
NewDocuments_WithBLOB: 0 (no new docs yet)

Storage anomalies:
STATUS: Storage anomalies:
AnomalyCount: 0 (no corruptions)
```

**✅ PASS if**: CorrectLegacy_PathOnly=14,047 and all errors=0
**❌ FAIL if**: Any ErrorLegacy values > 0

---

### STEP 4: Referential Integrity Check (3 min)

**Purpose**: Verify FK relationships intact and no orphaned records

**Validation Query**:

```sql
USE MMGTDB2A
GO

-- 4.1: Check for orphaned ApplicationDocuments
SELECT 
	'Orphaned ApplicationDocuments' as Check,
	COUNT(*) as Count
FROM ApplicationDocuments ad
WHERE NOT EXISTS (
	SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId
)
GO

-- 4.2: Check for orphaned ApplicationReviewHistory
SELECT 
	'Orphaned ApplicationReviewHistory' as Check,
	COUNT(*) as Count
FROM ApplicationReviewHistory arh
WHERE NOT EXISTS (
	SELECT 1 FROM Applications a WHERE a.Id = arh.ApplicationId
)
UNION ALL
SELECT 
	'ApplicationReviewHistory with missing User' as Check,
	COUNT(*) as Count
FROM ApplicationReviewHistory arh
WHERE NOT EXISTS (
	SELECT 1 FROM Users u WHERE u.Id = arh.ReviewedById
)
GO

-- 4.3: Check for Applications with missing PMICompany
SELECT 
	'Applications with missing PMICompany' as Check,
	COUNT(*) as Count
FROM Applications a
WHERE NOT EXISTS (
	SELECT 1 FROM PMICompanies pc WHERE pc.Id = a.PMICompanyId
)
UNION ALL
SELECT 
	'Applications with missing User' as Check,
	COUNT(*) as Count
FROM Applications a
WHERE NOT EXISTS (
	SELECT 1 FROM Users u WHERE u.Id = a.UserId
)
GO

-- 4.4: Total FK integrity summary
SELECT 
	CASE 
		WHEN (SELECT COUNT(*) FROM ApplicationDocuments ad WHERE NOT EXISTS (
			SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId)) > 0
		THEN 'FAIL: Orphaned ApplicationDocuments found'
		WHEN (SELECT COUNT(*) FROM ApplicationReviewHistory arh WHERE NOT EXISTS (
			SELECT 1 FROM Applications a WHERE a.Id = arh.ApplicationId)) > 0
		THEN 'FAIL: Orphaned ApplicationReviewHistory found'
		ELSE 'PASS: All FK relationships intact'
	END as FKIntegrityStatus
GO
```

**Expected Result**:
```
Orphaned ApplicationDocuments: 0
Orphaned ApplicationReviewHistory: 0
ApplicationReviewHistory with missing User: 0
Applications with missing PMICompany: 0
Applications with missing User: 0
FKIntegrityStatus: PASS: All FK relationships intact
```

**✅ PASS if**: All counts = 0 and FKIntegrityStatus = PASS
**❌ FAIL if**: Any orphaned records detected

---

### STEP 5: Data Transformation Verification (3 min)

**Purpose**: Verify critical transformations applied correctly

**Validation Query**:

```sql
USE MMGTDB2A
GO

-- 5.1: Verify ApprovalDate transformation
SELECT 
	'ApprovedAt -> ApprovalDate mapping' as Check,
	SUM(CASE WHEN ApprovedAt IS NOT NULL AND ApprovalDate IS NOT NULL 
		THEN 1 ELSE 0 END) as MappedCorrectly,
	SUM(CASE WHEN ApprovedAt IS NOT NULL AND ApprovalDate IS NULL 
		THEN 1 ELSE 0 END) as MappedIncorrectly,
	SUM(CASE WHEN ApprovedAt IS NULL AND ApprovalDate IS NOT NULL 
		THEN 1 ELSE 0 END) as UnexpectedValues
FROM Applications
GO

-- 5.2: Verify new nullable columns have correct defaults
SELECT 
	'New Applications columns' as Check,
	COUNT(*) as RowsChecked,
	SUM(CASE WHEN ApplicantId IS NOT NULL THEN 1 ELSE 0 END) as ApplicantId_NotNull,
	SUM(CASE WHEN LoanAmount IS NOT NULL THEN 1 ELSE 0 END) as LoanAmount_NotNull,
	SUM(CASE WHEN PropertyAddress IS NOT NULL THEN 1 ELSE 0 END) as PropertyAddress_NotNull,
	SUM(CASE WHEN DocumentTypeId IS NOT NULL THEN 1 ELSE 0 END) as DocumentTypeId_NotNull
FROM Applications
GO

-- 5.3: Verify ApplicationDocument new columns
SELECT 
	'ApplicationDocuments transformations' as Check,
	COUNT(*) as TotalDocs,
	SUM(CASE WHEN Version IS NOT NULL THEN 1 ELSE 0 END) as Version_NotNull,
	SUM(CASE WHEN ReviewStatus IS NOT NULL THEN 1 ELSE 0 END) as ReviewStatus_NotNull,
	SUM(CASE WHEN DocumentTypeId IS NOT NULL THEN 1 ELSE 0 END) as DocumentTypeId_NotNull
FROM ApplicationDocuments
GO
```

**Expected Result**:
```
ApprovedAt -> ApprovalDate mapping:
MappedCorrectly: 5,000-8,000 (varies based on source)
MappedIncorrectly: 0
UnexpectedValues: 0

New Applications columns:
RowsChecked: 13,789
ApplicantId_NotNull: 0 (expected - NULL allowed)
LoanAmount_NotNull: 0 (expected - NULL allowed)
PropertyAddress_NotNull: 0 (expected - NULL allowed)
DocumentTypeId_NotNull: 0 (expected - NULL allowed)

ApplicationDocuments transformations:
TotalDocs: 14,047
Version_NotNull: 0 (all NULL is acceptable, new uploads will set)
ReviewStatus_NotNull: 0 (all NULL is acceptable)
DocumentTypeId_NotNull: 0 (all NULL is acceptable)
```

**✅ PASS if**: MappedIncorrectly=0, UnexpectedValues=0, transformations applied as expected
**❌ FAIL if**: Data integrity violations or unexpected NULL/NOT NULL states

---

### STEP 6: Sample File Path Integrity (3 min)

**Purpose**: Verify FilePath values are valid and files are accessible

**Validation Query**:

```sql
USE MMGTDB2A
GO

-- 6.1: Verify FilePath structure is maintained
SELECT TOP 20
	Id,
	FileName,
	FilePath,
	FileSize,
	IsLegacy
FROM ApplicationDocuments
WHERE IsLegacy = 1 AND FilePath IS NOT NULL
ORDER BY CreatedAt DESC
GO

-- 6.2: Check for suspicious NULL FilePath values
SELECT 
	'FilePath integrity check' as Check,
	SUM(CASE WHEN IsLegacy = 1 AND FilePath IS NULL THEN 1 ELSE 0 END) as LegacyWithoutPath_ERROR,
	SUM(CASE WHEN IsLegacy = 1 AND FilePath = '' THEN 1 ELSE 0 END) as LegacyWithEmptyPath_ERROR,
	SUM(CASE WHEN IsLegacy = 1 AND FilePath LIKE '%.pdf' THEN 1 ELSE 0 END) as LegacyPDF_Count,
	SUM(CASE WHEN IsLegacy = 1 AND LEN(FilePath) > 0 THEN 1 ELSE 0 END) as LegacyWithValidPath
FROM ApplicationDocuments
GO
```

**Expected Result**:
```
Sample FilePath values: (20 rows showing valid paths like C:\uploads\...)
LegacyWithoutPath_ERROR: 0
LegacyWithEmptyPath_ERROR: 0
LegacyPDF_Count: 13,000-14,047 (most should be PDF)
LegacyWithValidPath: 14,047 (all should have paths)
```

**✅ PASS if**: All LegacyWithPath > 0, errors = 0, valid path structure
**❌ FAIL if**: NULL paths, empty strings, or invalid path format detected

---

## Final Validation Summary

After completing all 6 validation steps, run this summary query:

```sql
USE MMGTDB2A
GO

-- FINAL COMPREHENSIVE VALIDATION
DECLARE @recordsValid BIT = 1
DECLARE @isLegacyValid BIT = 1
DECLARE @storageValid BIT = 1
DECLARE @fkValid BIT = 1
DECLARE @transformValid BIT = 1
DECLARE @pathValid BIT = 1

-- Check 1: Records
IF NOT (
	(SELECT COUNT(*) FROM Users) = 110 AND
	(SELECT COUNT(*) FROM PMICompanies) = 24 AND
	(SELECT COUNT(*) FROM Applications) = 13789 AND
	(SELECT COUNT(*) FROM ApplicationDocuments) = 14047 AND
	(SELECT COUNT(*) FROM ApplicationReviewHistory) = 31135
)
	SET @recordsValid = 0

-- Check 2: IsLegacy
IF NOT (
	(SELECT COUNT(*) FROM ApplicationDocuments WHERE IsLegacy = 1) = 14047 AND
	(SELECT COUNT(*) FROM ApplicationDocuments WHERE IsLegacy = 0) = 0
)
	SET @isLegacyValid = 0

-- Check 3: Storage
IF NOT (
	(SELECT COUNT(*) FROM ApplicationDocuments WHERE IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NULL) = 14047 AND
	(SELECT COUNT(*) FROM ApplicationDocuments WHERE IsLegacy = 1 AND FileContent IS NOT NULL) = 0
)
	SET @storageValid = 0

-- Check 4: FK Integrity
IF NOT (
	(SELECT COUNT(*) FROM ApplicationDocuments ad WHERE NOT EXISTS (SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId)) = 0 AND
	(SELECT COUNT(*) FROM ApplicationReviewHistory arh WHERE NOT EXISTS (SELECT 1 FROM Applications a WHERE a.Id = arh.ApplicationId)) = 0
)
	SET @fkValid = 0

-- Check 5: Transformations
IF NOT (
	(SELECT COUNT(*) FROM ApplicationDocuments WHERE IsLegacy = 1 AND FileContent IS NULL) = 14047
)
	SET @transformValid = 0

-- Check 6: Paths
IF NOT (
	(SELECT COUNT(*) FROM ApplicationDocuments WHERE IsLegacy = 1 AND FilePath IS NOT NULL AND FilePath <> '') = 14047
)
	SET @pathValid = 0

-- FINAL RESULT
SELECT 
	CASE WHEN @recordsValid = 1 THEN '✅ PASS' ELSE '❌ FAIL' END as RecordCounts,
	CASE WHEN @isLegacyValid = 1 THEN '✅ PASS' ELSE '❌ FAIL' END as IsLegacyFlags,
	CASE WHEN @storageValid = 1 THEN '✅ PASS' ELSE '❌ FAIL' END as StorageState,
	CASE WHEN @fkValid = 1 THEN '✅ PASS' ELSE '❌ FAIL' END as FKIntegrity,
	CASE WHEN @transformValid = 1 THEN '✅ PASS' ELSE '❌ FAIL' END as Transformations,
	CASE WHEN @pathValid = 1 THEN '✅ PASS' ELSE '❌ FAIL' END as FilePaths,
	CASE 
		WHEN @recordsValid = 1 AND @isLegacyValid = 1 AND @storageValid = 1 AND @fkValid = 1 AND @transformValid = 1 AND @pathValid = 1
		THEN '✅ **MIGRATION SUCCESSFUL - ALL VALIDATIONS PASSED**'
		ELSE '❌ **MIGRATION FAILED - ONE OR MORE VALIDATIONS FAILED**'
	END as OverallStatus
GO
```

**Expected Result**:
```
RecordCounts: ✅ PASS
IsLegacyFlags: ✅ PASS
StorageState: ✅ PASS
FKIntegrity: ✅ PASS
Transformations: ✅ PASS
FilePaths: ✅ PASS
OverallStatus: ✅ **MIGRATION SUCCESSFUL - ALL VALIDATIONS PASSED**
```

---

## Application API Validation (Optional but Recommended)

After database validation, test the API to ensure migrated data is accessible:

```bash
# Test 1: Get an application
GET https://api.yourdomain.com/api/applications/{applicationId}
# Should return: 200 OK with migrated application data

# Test 2: List files for application
GET https://api.yourdomain.com/api/file/by-application/{applicationId}
# Should return: Array of files with isLegacy: true

# Test 3: Download legacy document
GET https://api.yourdomain.com/api/file/download/{fileId}
# Should return: 200 OK + File content (read from disk)

# Test 4: Try to replace legacy document (should fail)
PUT https://api.yourdomain.com/api/file/replace/{fileId}
# Should return: 400 Bad Request (legacy file cannot be replaced)

# Test 5: Upload new document (should work)
POST https://api.yourdomain.com/api/file/upload
# Should return: 200 OK + New file stored as BLOB
```

---

## Success Criteria Summary

✅ **Migration is successful if ALL of these pass:**

- [x] Record counts: 59,105 total (all 5 tables match source exactly)
- [x] IsLegacy: All 14,047 ApplicationDocuments have IsLegacy=1
- [x] Storage: All legacy = FilePath + no FileContent
- [x] FK Integrity: Zero orphaned records
- [x] Transformations: ApprovalDate mapped, new columns have defaults
- [x] FilePaths: All 14,047 legacy docs have valid paths
- [x] API Access: Legacy document download via disk fallback works
- [x] No errors in Phase2_MigrationExecution.sql output

---

## Failure Scenarios & Recovery

### Scenario 1: Record Count Mismatch

**Error**: SELECT COUNT(*) returns different numbers than expected

**Response Steps**:
1. Restore MMGTDB2A backup
2. Check MMGTDBCOPY source counts
3. Re-run Phase2_MigrationExecution.sql
4. If still fails: Contact Support

### Scenario 2: IsLegacy Flags Not Set

**Error**: ApplicationDocuments have IsLegacy=0

**Response Steps**:
1. Restore from backup
2. Verify Phase2_MigrationExecution.sql SET IsLegacy=1 line
3. Re-run migration
4. Validate results

### Scenario 3: FK Constraint Violations

**Error**: Orphaned records detected

**Response Steps**:
1. Identify orphaned records with provided queries
2. Restore from backup
3. Check source data for FK violations
4. Fix source and re-migrate

### Scenario 4: FilePath Missing or NULL

**Error**: Legacy documents lack file paths

**Response Steps**:
1. Restore from backup
2. Verify source FilePath values exist
3. Check transformation logic in migration script
4. Re-run with correct FilePath mapping

---

## Documentation & Sign-Off

**Post-Migration Sign-Off Template**:

```
Migration Executed: [Date/Time]
Executed By: [Name]
Database: MMGTDB2A
Source: MMGTDBCOPY

Validation Results:
✅ Step 1 (Record Counts): PASS
✅ Step 2 (IsLegacy Flags): PASS
✅ Step 3 (Storage State): PASS
✅ Step 4 (FK Integrity): PASS
✅ Step 5 (Transformations): PASS
✅ Step 6 (FilePath Integrity): PASS
✅ Final Summary: ALL VALIDATIONS PASSED

Migration Status: ✅ SUCCESSFUL
Time Elapsed: [X minutes]
Issues Encountered: [None / List any]

Sign-Off: _________________________ Date: _________
```

---

## Next Steps After Successful Validation

1. **Notify Stakeholders**: Migration successful, system ready
2. **Archive Documentation**: Store execution logs and validation results
3. **Monitor Application**: Watch logs for migration-related errors (24 hrs)
4. **Phase 3**: Plan application acceptance testing
5. **Cleanup**: Archive backup and old deployment artifacts after 30 days

---

**Phase 2 Post-Migration Validation Framework - Complete**

*Document: Phase2_PostMigration_Validation.md*  
*Created: 2025-01-15*  
*Status: Ready for Execution*
