# Phase 2 Migration - v2 Fixes & Root Cause Analysis

## Problem Statement
First migration attempt: **Only Applications and PMI tables migrated; Users, ApplicationDocuments, and ApplicationReviewHistory did not.**

## Root Cause Analysis

### Why PMI and Applications succeeded but Users failed:

1. **PMICompanies** - Direct insert from source with no FK dependencies ✓ Success expected
2. **Applications** - Depends only on `UserId` and `PmiCompanyId` foreign keys
   - However, migrated **without corresponding Users records**
   - This suggests either:
	 - Users insert failed silently before Applications ran, OR
	 - FK constraints were disabled (which they were in SECTION 2)
   - Since constraints disabled, Applications inserted even with dangling FK references

3. **Users** - Likely failed due to:
   - Duplicate Email or PMIEmail values
   - NULL values in required columns from target schema
   - FK constraint to PMICompanies (if PMICompanyId doesn't exist)
   - Script continued despite Users failing (no error handling in v1)

4. **ApplicationDocuments** - Failed due to:
   - `DocumentTypeId` is NOT NULL in target but script provided NULL
   - DocumentType mapping may have returned all NULLs for unmapped types
   - FK constraint to Applications (Applications records exist but orphaned from Users)

5. **ApplicationReviewHistory** - Failed due to:
   - FK to Applications with invalid `ReviewedById` (Users not migrated)
   - FK to Applications.Id (orphaned records)

## Changes in v2 Script

### 1. **Better Error Handling**
```sql
BEGIN TRY
	-- INSERT statement
	PRINT '✓ Table migrated: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR: ' + ERROR_MESSAGE()
	-- Continues to next table instead of stopping
END CATCH
```
- Each table migration wrapped in TRY-CATCH
- Errors are captured and printed, but execution continues
- No silent failures

### 2. **Corrected Migration Order**
**v1 order**: Users → PMI → Applications → Docs → History
**v2 order**: **PMI → Users → Applications → Docs → History**
- PMICompanies has no dependencies, migrate first
- Then Users (depends on PMI existing for FK)
- Then Applications (depends on Users + PMI)
- Then Documents (depends on Applications)
- Finally ReviewHistory (depends on Applications + Users)

### 3. **FK Constraint Management**
- Disable ALL constraints BEFORE any deletes
- Delete in reverse dependency order
- Re-enable constraints AFTER all inserts
- Prevents cascade deletes and phantom FK violations

### 4. **DocumentType Mapping with Diagnostics**
```sql
-- Show unmapped types
SELECT DISTINCT SourceType FROM #DocTypeMapping WHERE TargetTypeId IS NULL

-- Show available types
SELECT TOP 20 Code, Name, Id FROM MMGTDB2A.dbo.DocumentTypes

-- Assign unmapped to default (with GUID printed)
DECLARE @DefaultTypeId = SELECT TOP 1 Id FROM DocumentTypes WHERE LOWER(Code) = 'identity_document'
```
- Displays exactly which DocumentTypes couldn't be mapped
- Shows what's available in target
- Assigns unmapped to 'identity_document' (most common) or first available

### 5. **Detailed Post-Migration Validation**
```sql
-- Check IsLegacy flags
SELECT IsLegacy, COUNT(*) FROM ApplicationDocuments GROUP BY IsLegacy

-- Check for NULL DocumentTypeId (data quality issue)
SELECT COUNT(*) FROM ApplicationDocuments WHERE DocumentTypeId IS NULL

-- Verify FK integrity
SELECT COUNT(*) FROM Applications a 
WHERE NOT EXISTS (SELECT 1 FROM Users u WHERE u.Id = a.UserId)
```
- Shows exactly what migrated
- Identifies data quality issues immediately
- Confirms FK integrity

## Execution Instructions for v2

1. **Open SSMS** → Connect to `172.16.50.42` → Select `MMGTDB2A`
2. **Backup or ensure MMGTDB2A is backed up** (user confirmed already backed up)
3. **Open** `Analysis/Phase2_MigrationExecution_v2.sql`
4. **Execute** (Ctrl+A, F5)
5. **Read output** carefully:
   - Look for any `✗ ERROR` messages
   - Check section 5.1 record counts match source
   - Check section 5.3 shows all DocumentTypeId are non-NULL
   - Check section 5.4 shows valid FK references

## Expected Output (on success)

```
=== SECTION 5: POST-MIGRATION VALIDATION ===

5.1: Target (MMGTDB2A) AFTER migration:
Users        | 2 rows
PMICompanies | 2 rows  
Applications | 2 rows
ApplicationDocuments | 132 rows (estimated)
ApplicationReviewHistory | 56 rows (estimated)

5.3: Verify DocumentTypeId is NOT NULL:
Non-NULL DocumentTypeId  | 132 rows
NULL DocumentTypeId      | 0 rows

5.4: Verify FK integrity:
Valid FK   | 2 rows
Invalid FK | 0 rows
```

## If v2 Still Has Issues

1. **ApplicationDocuments still 0 rows:**
   - Check if source ApplicationDocuments table actually has data
   - Check if DocumentTypeId mapping shows NULL assignments
   - Verify FK constraint to Applications (maybe Applications insert failed)

2. **Users still 0 rows:**
   - Check if duplicate Emails exist in source
   - Check if PMICompanyId values in source don't exist in PMICompanies
   - Manually inspect first user record: `SELECT TOP 1 * FROM MMGTDBCOPY.dbo.Users`

3. **ApplicationReviewHistory still 0 rows:**
   - Verify Applications successfully migrated (check 5.1)
   - Check if ReviewedById values don't exist in migrated Users

## Next Steps After Successful Migration

1. Run `Analysis/Phase2_PostMigration_Validation.md` checks
2. Run `Analysis/Phase2_ApplicationConnectivity_Verification.md` endpoint tests
3. Test legacy file download: `GET /api/file/download/{documentId}`
4. Test new BLOB upload: `POST /api/file/upload`
5. Sign off on Phase 2 completion
