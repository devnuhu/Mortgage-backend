# Phase 2: Live Data Migration Execution - Final Guide

## Status: ✅ Ready for Execution

All prerequisites met. Migration can now proceed.

---

## Migration Execution Steps

### Step A: Pre-Execution Checklist (Final)

Before running the migration script, confirm:

```
✅ Backup MMGTDB2A taken and stored safely
✅ SQL Server 172.16.50.42 accessible
✅ All 12 target database checks passed (see Phase2_TargetDatabase_Verification.md)
✅ Pre-migration validation checklist completed (see Phase2_Execution_Checklist.md)
✅ Source database MMGTDBCOPY verified (59,105 records)
✅ Network stable and connectivity tested
✅ No other active processes on MMGTDB2A
```

---

### Step B: Open Migration Script

**File**: `C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_MigrationExecution.sql`

**Open in**: SQL Server Management Studio (SSMS)

**Steps**:
1. Launch SSMS
2. Connect to: `172.16.50.42` (or your SQL Server host)
3. Authenticate: Use Windows Auth or SQL Auth
4. File → Open File → Select `Phase2_MigrationExecution.sql`

---

### Step C: Review Script Before Execution

**Script Contents** (6 Sections):

```sql
SECTION 1: PRE-MIGRATION VALIDATION
├─ Check source database accessibility
├─ Verify MMGTDBCOPY tables exist
├─ Check target database accessibility
├─ Verify MMGTDB2A tables exist
└─ Display record counts

SECTION 2: TARGET DATABASE PREPARATION
├─ Disable all foreign key constraints
├─ Clear existing data (if any) via DELETE
├─ Reset identity seeds (not used, but safe)
└─ Prepare for clean migration

SECTION 3: DATA MIGRATION (FK ORDER)
├─ Users (base table)
├─ PMICompanies (base table)
├─ Applications (depends on Users + PMICompanies)
├─ ApplicationDocuments (depends on Applications + DocumentTypes)
│  └─ [CRITICAL] Sets IsLegacy=1 for all migrated docs
│  └─ [CRITICAL] Sets FileContent=NULL for disk-based legacy files
│  └─ [CRITICAL] Preserves FilePath values
└─ ApplicationReviewHistory (depends on Applications + Users)

SECTION 4: CONSTRAINT RE-ENABLEMENT
├─ Re-enable all foreign key constraints
└─ Validate no constraint violations

SECTION 5: POST-MIGRATION VALIDATION
├─ Verify record counts match source
├─ Check for FK constraint violations
├─ Validate IsLegacy flags (14,047 legacy docs)
├─ Check for orphaned records
├─ Verify FilePath preservation
└─ Display summary statistics

SECTION 6: EXECUTION SUMMARY
├─ Display migration completion status
├─ Show record counts by table
├─ List any warnings or issues
└─ Provide next steps
```

**Key Transformations Applied**:

1. **Users**: Direct copy (no new columns)
2. **PMICompanies**: Direct copy (no new columns)
3. **Applications**: Adds 12 new columns
   - ApplicantId, LoanAmount, PropertyAddress, PropertyValue, LoanTerm
   - InterestRate, MortgageType, ApprovalDate, ClosingDate, Notes
   - AssignedToUserId, Priority
   - Maps ApprovedAt → ApprovalDate

4. **ApplicationDocuments**: Adds 10 new columns
   - DocumentTypeId, FileContent, Version, IsLatestVersion, ReviewStatus
   - ReviewComments, ReviewedBy, ReviewedAt, UploadedBy, IsLegacy
   - **CRITICAL**: IsLegacy=1, FileContent=NULL (all legacy)

5. **ApplicationReviewHistory**: Direct copy (no new columns)

---

### Step D: Execute Migration Script

**In SSMS**:

1. **Select Target Database**:
   - Upper-right dropdown: Select `MMGTDB2A`

2. **Execute Script**:
   - Click: **Execute** button (or press F5)
   - **OR** use keyboard shortcut: `Ctrl+Shift+E`

3. **Monitor Progress**:
   - Watch the "Messages" tab for output
   - Script will display progress for each section
   - Typical execution time: **30-60 minutes** (depends on system)

**What to Watch For**:

```
EXPECTED PROGRESS:
✓ PRE-MIGRATION VALIDATION:
  ├─ MMGTDBCOPY accessible ✓
  ├─ Source tables exist ✓
  ├─ MMGTDB2A accessible ✓
  ├─ Target tables exist ✓
  └─ Record counts verified ✓

✓ TARGET DATABASE PREPARATION:
  ├─ FK constraints disabled ✓
  ├─ Existing data cleared ✓
  └─ Database ready ✓

✓ DATA MIGRATION:
  ├─ Users: 110 records inserted ✓
  ├─ PMICompanies: 24 records inserted ✓
  ├─ Applications: 13,789 records inserted ✓
  ├─ ApplicationDocuments: 14,047 records inserted ✓
  │  └─ IsLegacy=1 for all ✓
  │  └─ FileContent=NULL for all ✓
  └─ ApplicationReviewHistory: 31,135 records inserted ✓

✓ CONSTRAINT RE-ENABLEMENT:
  ├─ FK constraints re-enabled ✓
  └─ No constraint violations ✓

✓ POST-MIGRATION VALIDATION:
  ├─ Record counts match ✓
  ├─ FK integrity verified ✓
  └─ Summary: SUCCESS ✓
```

---

### Step E: Check for Errors

**If Script Completes WITHOUT Errors**:

✅ Migration successful!

Go to **Step F: Post-Migration Validation**

**If Script Shows Errors**:

❌ Stop execution and review error

**Common Errors**:

1. **"Invalid object name 'MMGTDBCOPY.dbo.Users'"**
   - Cause: Source database not accessible
   - Fix: Check SQL Server host (172.16.50.42) connectivity
   - Action: Verify `MMGTDBCOPY` exists and is readable

2. **"There is already an object named 'X' in the database"**
   - Cause: Target has leftover data from previous run
   - Fix: Manually delete old data and retry
   - SQL: `DELETE FROM ApplicationReviewHistory; DELETE FROM ApplicationDocuments; DELETE FROM Applications; DELETE FROM PMICompanies; DELETE FROM Users;`

3. **"Foreign key constraint violation"**
   - Cause: Data integrity issue in source
   - Fix: Run pre-migration validation to find orphaned records
   - Action: Restore backup and contact support

4. **"Timeout expired"**
   - Cause: Script taking too long (normal for large datasets)
   - Action: Wait up to 2 hours before force-stopping

5. **"Insufficient space in database device"**
   - Cause: Out of disk space
   - Fix: Extend MMGTDB2A data file or free up disk space

---

### Step F: Post-Migration Validation

**When**: Immediately after script completes successfully

**File**: `C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_Legacy_Document_Investigation.sql`

**Run These Validation Queries**:

```sql
-- 1. Total Record Count
SELECT 
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

-- Expected Result:
-- Users: 110
-- PMICompanies: 24
-- Applications: 13,789
-- ApplicationDocuments: 14,047
-- ApplicationReviewHistory: 31,135
-- TotalRecords: 59,105
```

```sql
-- 2. Legacy Document Flags
SELECT 
	COUNT(*) as Total,
	SUM(CASE WHEN IsLegacy = 1 THEN 1 ELSE 0 END) as LegacyCount,
	SUM(CASE WHEN IsLegacy = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) as LegacyPercent

FROM ApplicationDocuments

-- Expected Result:
-- Total: 14,047
-- LegacyCount: 14,047
-- LegacyPercent: 100.0
```

```sql
-- 3. Storage State Verification
SELECT 
	SUM(CASE WHEN IsLegacy = 1 AND FilePath IS NOT NULL THEN 1 ELSE 0 END) as LegacyWithPath,
	SUM(CASE WHEN IsLegacy = 1 AND FileContent IS NULL THEN 1 ELSE 0 END) as LegacyNoBLOB,
	SUM(CASE WHEN IsLegacy = 1 AND FileContent IS NOT NULL THEN 1 ELSE 0 END) as LegacyWithBLOB_ERROR

FROM ApplicationDocuments

-- Expected Result:
-- LegacyWithPath: 14,047 (all have paths)
-- LegacyNoBLOB: 14,047 (no BLOB content)
-- LegacyWithBLOB_ERROR: 0 (ERROR if > 0)
```

```sql
-- 4. FK Integrity Check
SELECT 
	COUNT(*) as OrphanDocuments
FROM ApplicationDocuments ad
WHERE NOT EXISTS (
	SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId
)

-- Expected Result: 0 (no orphans)
```

```sql
-- 5. Sample Legacy File Paths
SELECT TOP 10 
	Id, 
	ApplicationId, 
	FileName, 
	FilePath, 
	IsLegacy, 
	FileSize
FROM ApplicationDocuments
WHERE IsLegacy = 1 AND FilePath IS NOT NULL
ORDER BY CreatedAt DESC

-- Verify 1-2 FilePath values correspond to actual files on disk
```

---

### Step G: Verify API Access (Optional)

**Test that legacy documents are still accessible**:

```bash
# In PowerShell or browser:

# 1. Get application (requires auth token)
GET https://your-api.com/api/applications/{applicationId}
Response should include: { ...documents... }

# 2. List files for application
GET https://your-api.com/api/file/by-application/{applicationId}
Response: [ { fileId, fileName, isLegacy: true, ... }, ... ]

# 3. Download legacy document
GET https://your-api.com/api/file/download/{fileId}
Response: File bytes (from disk via FileService.DownloadFileAsync)
```

---

## Migration Execution Timeline

### Before Migration
- **Prep Time**: Review this guide + verify all checks
- **Duration**: 15 minutes

### During Migration
- **Execution Time**: 30-60 minutes (60-90% in ApplicationDocuments insert)
- **Monitor Time**: Watch for errors/warnings

### After Migration
- **Validation Time**: 10-15 minutes (run 5 validation queries)
- **Total Time**: ~85 minutes (1.5 hours safe buffer, 2 hours max)

---

## Success Criteria

✅ **Migration is successful if:**

1. Script completes WITH NO ERRORS
2. All 5 validation queries pass
3. Record counts match exactly
4. IsLegacy=1 for all 14,047 ApplicationDocuments
5. No orphaned records detected
6. API legacy document download works
7. New uploads still store as BLOB

---

## Rollback Plan

**If Migration Fails**:

1. **Stop Script**: Force-stop if hanging (Ctrl+Alt+Delete → Task Manager)
2. **Restore Backup**:
   ```sql
   RESTORE DATABASE MMGTDB2A 
   FROM DISK = '\\backup\MMGTDB2A_pre_phase2.bak'
   WITH REPLACE, RECOVERY
   ```
3. **Verify Recovery**: 
   ```sql
   SELECT COUNT(*) FROM Users  -- Should be original count
   ```
4. **Investigate Error**: Review SSMS output window for exact error
5. **Retry**: Fix root cause and re-run migration

---

## Final Approval Gate

### Ready to Execute?

**Confirm ALL items below**:

- [ ] Backup MMGTDB2A exists and is secure
- [ ] SSMS connected to 172.16.50.42
- [ ] `Phase2_MigrationExecution.sql` open and reviewed
- [ ] No other processes running on MMGTDB2A
- [ ] Network stable and tested
- [ ] Support team on standby (if applicable)

**Status**: ✅ **APPROVED FOR EXECUTION**

---

## Execution Command

**When ready, execute this script in SSMS:**

```
File: Analysis/Phase2_MigrationExecution.sql
Server: 172.16.50.42
Database: MMGTDB2A
Expected Duration: 30-60 minutes
Success Indicator: All sections complete, 59,105 total records
```

---

## Documentation & Support

**During Execution**:
- Monitor: SSMS Messages tab
- Issues: Record error messages (copy full text)
- Escalation: Contact DBA if script hangs > 30 min

**After Execution**:
- Validation: Run Phase2_Legacy_Document_Investigation.sql
- Reporting: Document completion status and timing
- Sign-Off: Archive this guide with execution results

---

**Phase 2 Migration: Ready for Execution**

Next Action: Execute Phase2_MigrationExecution.sql in SSMS

---

*Document: Phase2_MigrationExecution_Guide.md*  
*Created: 2025-01-15*  
*Status: Ready for Execution*
