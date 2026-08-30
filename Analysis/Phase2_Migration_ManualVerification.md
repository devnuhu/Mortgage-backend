# Phase 2 Migration - Manual Verification & Next Steps

## Current Status
- ✅ Migration script executed via PowerShell SqlServer module
- ⏳ Awaiting completion (estimated 30-60 minutes total)
- ❌ Remote database connection timing out (SSL issue)

---

## Manual Verification in SQL Server Management Studio

### Step 1: Connect to Database
1. Open **SQL Server Management Studio**
2. **Server name**: 172.16.50.42
3. **Authentication**: Windows Auth (or SQL Auth if configured)
4. Click **Connect**

### Step 2: Check Record Counts

Execute this query in SSMS to verify migration:

```sql
USE MMGTDB2A
GO

SELECT 
	'Users' as [Table],
	COUNT(*) as RecordCount,
	'Expected: 110' as Expected
FROM Users
UNION ALL
SELECT 'PMICompanies', COUNT(*), 'Expected: 24' FROM PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*), 'Expected: 13,789' FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*), 'Expected: 14,047' FROM ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*), 'Expected: 31,135' FROM ApplicationReviewHistory
GO
```

**Expected Results**:
```
Users:                      110
PMICompanies:               24
Applications:              13,789
ApplicationDocuments:      14,047
ApplicationReviewHistory:  31,135
────────────────────────────────
TOTAL:                     59,105
```

---

## Step 3: Verify Critical Transformations

If records are found, verify transformations with these checks:

### Check 1: IsLegacy Flags

```sql
USE MMGTDB2A
GO

SELECT 
	COUNT(*) as TotalDocuments,
	SUM(CASE WHEN IsLegacy = 1 THEN 1 ELSE 0 END) as LegacyCount,
	SUM(CASE WHEN IsLegacy = 0 THEN 1 ELSE 0 END) as NewCount
FROM ApplicationDocuments
GO
```

**Expected**:
```
TotalDocuments: 14,047
LegacyCount: 14,047
NewCount: 0
```

### Check 2: Storage State

```sql
USE MMGTDB2A
GO

SELECT 
	SUM(CASE WHEN IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NULL THEN 1 ELSE 0 END) as CorrectLegacy,
	SUM(CASE WHEN IsLegacy = 1 AND FileContent IS NOT NULL THEN 1 ELSE 0 END) as ErrorWithBLOB,
	SUM(CASE WHEN IsLegacy = 1 AND FilePath IS NULL THEN 1 ELSE 0 END) as ErrorNoPath
FROM ApplicationDocuments
GO
```

**Expected**:
```
CorrectLegacy: 14,047
ErrorWithBLOB: 0
ErrorNoPath: 0
```

### Check 3: FK Integrity

```sql
USE MMGTDB2A
GO

SELECT 
	'Orphaned ApplicationDocuments' as Check,
	COUNT(*) as Count
FROM ApplicationDocuments ad
WHERE NOT EXISTS (SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId)
UNION ALL
SELECT 'Orphaned ApplicationReviewHistory', COUNT(*)
FROM ApplicationReviewHistory arh
WHERE NOT EXISTS (SELECT 1 FROM Applications a WHERE a.Id = arh.ApplicationId)
GO
```

**Expected**: Both should return 0

---

## If Migration NOT Complete

### Check Migration Errors

```sql
USE MMGTDB2A
GO

-- Check if script is still running (lock wait)
EXEC sp_who2
GO

-- Check for recent errors
SELECT TOP 50 * FROM sys.dm_os_waiting_tasks
GO
```

### If Migration Failed or Stuck

#### Option 1: Restore Backup
```sql
RESTORE DATABASE MMGTDB2A 
FROM DISK = '[backup location]'
WITH REPLACE, RECOVERY
```

#### Option 2: Re-run Migration
1. Delete problematic data or restore
2. Run Phase2_MigrationExecution.sql again
3. Monitor execution in Query Output pane

---

## If Migration Complete

### Step 4: Run Post-Migration Validation

Open file: `Analysis/Phase2_PostMigration_Validation.md`

Run the 6-step validation framework:
1. Record count verification
2. IsLegacy flag verification
3. Storage state verification
4. Referential integrity check
5. Data transformation verification
6. Sample file path integrity

---

## Step 5: Test Application Connectivity

Open file: `Analysis/Phase2_ApplicationConnectivity_Verification.md`

Tests to run:
1. Application startup (verify MMGTDB2A connection)
2. Health endpoint
3. List applications endpoint
4. Get files for application endpoint
5. Download legacy document endpoint (CRITICAL)
6. Verify replace legacy blocked (should error)
7. Verify delete legacy blocked (should error)
8. Upload new document (should create BLOB)

---

## Next Document to Review

👉 **After migration completes:**

1. `Analysis/Phase2_PostMigration_Validation.md` - Database validation
2. `Analysis/Phase2_ApplicationConnectivity_Verification.md` - Application testing
3. `Analysis/Phase2_CompletionSummary.md` - Final sign-off

---

## Summary

| Task | Status |
|------|--------|
| Migration script created | ✅ Complete |
| Migration execution started | ✅ Running |
| Manual verification needed | ⏳ Next |
| Post-migration validation | ⏳ After |
| Application testing | ⏳ After |
| Final sign-off | ⏳ After |

---

**Current Time**: Awaiting manual verification

**Next Action**: Check SSMS record counts against expected values

---

*Generated: 2025-01-15*  
*Migration Status: In Progress or Completed*  
*Verification Method: Manual SSMS Queries*
