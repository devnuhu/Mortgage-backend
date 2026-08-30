# Phase 2 Execution Checklist - Pre-Migration Validation

## Status: Ready to Execute

All Phase 2 migration scripts are prepared and ready for execution.

---

## Pre-Migration Validation (Step 3)

### Prerequisite Checks

#### ✓ Check 1: SQL Server Connectivity
- **Test Server**: 172.16.50.42
- **Source Database**: MMGTDBCOPY
- **Target Database**: MMGTDB2A
- **Tool**: SQL Server Management Studio or sqlcmd

**Validation Command**:
```bash
sqlcmd -S 172.16.50.42 -d MMGTDBCOPY -Q "SELECT COUNT(*) as Users FROM dbo.Users"
```

Expected: Should return count of Users (should be ~110)

#### ✓ Check 2: Target Database Backup Confirmed
- **Status**: ✅ User mentioned backup already taken
- **Backup Location**: Not specified (user to confirm)
- **Recovery Plan**: Restore from backup if migration fails

#### ✓ Check 3: Required Files Present
- [x] Phase2_MigrationExecution.sql
- [x] Phase2_DataTransformation_Complete.md
- [x] Phase2_Legacy_Document_Investigation.sql (validation queries)
- [x] Phase1_DataTransformation_Strategy.md (reference)

#### ✓ Check 4: Disk Space Available
- **Estimated Space Needed**: ~500 MB (for 59,105 records)
- **MMGTDB2A Location**: Confirm available space
- **Temporary Space**: For transaction logs during migration

---

## Step-by-Step Execution Guide

### Phase 2a: Pre-Migration Validation (Current Step)

**Actions**:

1. **Verify Source Data Integrity**
   ```sql
   -- Run against MMGTDBCOPY
   SELECT 
	   (SELECT COUNT(*) FROM Users) as Users,
	   (SELECT COUNT(*) FROM PMICompanies) as PMICompanies,
	   (SELECT COUNT(*) FROM Applications) as Applications,
	   (SELECT COUNT(*) FROM ApplicationDocuments) as ApplicationDocuments,
	   (SELECT COUNT(*) FROM ApplicationReviewHistory) as ApplicationReviewHistory
   ```

   Expected:
   ```
   Users: 110
   PMICompanies: 24
   Applications: 13789
   ApplicationDocuments: 14047
   ApplicationReviewHistory: 31135
   Total: 59105
   ```

2. **Verify FK Integrity**
   ```sql
   -- Run against MMGTDBCOPY
   SELECT COUNT(*) as OrphanDocuments
   FROM ApplicationDocuments ad
   WHERE NOT EXISTS (SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId)
   -- Should return: 0
   ```

3. **Verify Physical Files Accessible**
   ```sql
   -- Run against MMGTDBCOPY
   SELECT 
	   COUNT(*) as LegacyDocs,
	   COUNT(DISTINCT FilePath) as UniquePaths
   FROM ApplicationDocuments
   WHERE FilePath IS NOT NULL
   ```

4. **Sample FilePath Validation**
   ```sql
   -- Run against MMGTDBCOPY
   SELECT TOP 10 FilePath FROM ApplicationDocuments WHERE FilePath IS NOT NULL
   -- Manually verify 1-2 files exist at these paths
   ```

---

### Phase 2b: Execute Migration (Next Steps)

**When to Run**: After pre-migration validation passes

**File**: `Analysis/Phase2_MigrationExecution.sql`

**Environment**: 
- Run in: SQL Server Management Studio (SSMS) or sqlcmd
- Server: 172.16.50.42
- Database: MMGTDB2A
- Mode: Interactive (to monitor progress)

**Steps**:
1. Open SSMS
2. Connect to: 172.16.50.42 → MMGTDB2A
3. Open: `Analysis/Phase2_MigrationExecution.sql`
4. Click: Execute (or F5)
5. Monitor: Output window (shows progress)
6. Review: Validation results

---

### Phase 2c: Post-Migration Validation

**Run After**: Migration script completes successfully

**File**: `Analysis/Phase2_Legacy_Document_Investigation.sql` (queries 1-12)

**Quick Checks**:
```sql
-- Expected: 14047 legacy documents
SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments WHERE IsLegacy = 1

-- Expected: 0 (all have FilePath)
SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments 
WHERE IsLegacy = 1 AND FilePath IS NULL

-- Expected: 0 (no BLOB content for legacy)
SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments
WHERE IsLegacy = 1 AND FileContent IS NOT NULL
```

---

## Pre-Migration Validation Checklist

### Infrastructure
- [ ] SQL Server 172.16.50.42 accessible via SSMS
- [ ] MMGTDBCOPY database accessible
- [ ] MMGTDB2A database accessible
- [ ] Database backup taken anddocumented
- [ ] Sufficient disk space available (~500 MB)
- [ ] Network connectivity stable

### Source Data
- [ ] Users count: 110 ✓
- [ ] PMICompanies count: 24 ✓
- [ ] Applications count: 13,789 ✓
- [ ] ApplicationDocuments count: 14,047 ✓
- [ ] ApplicationReviewHistory count: 31,135 ✓
- [ ] Total: 59,105 ✓
- [ ] FK integrity verified (no orphans) ✓
- [ ] Sample FilePath entries accessible ✓

### Target Database
- [ ] MMGTDB2A schema complete (migrations applied)
- [ ] IsLegacy column exists
- [ ] All constraints syntax correct
- [ ] Sufficient space for 59,105 records
- [ ] No active transactions blocking

### Migration Files
- [ ] Phase2_MigrationExecution.sql ready
- [ ] Phase2_DataTransformation_Complete.md reviewed
- [ ] Phase2_Legacy_Document_Investigation.sql available
- [ ] Transformation rules understood
- [ ] isLegacy=1 transformation confirmed

### Team Readiness
- [ ] DBA available for execution
- [ ] Monitoring capacity during migration
- [ ] Rollback plan understood
- [ ] Success criteria defined

---

## Go/No-Go Decision Gate

**Ready to Proceed if:**
- ✅ All prerequisite checks passed
- ✅ Source data validated (59,105 records confirmed)
- ✅ FK integrity verified
- ✅ Target database ready
- ✅ Backup confirmed
- ✅ All migration files in place

**Do NOT Proceed if:**
- ❌ SQL Server not accessible
- ❌ Source data counts don't match
- ❌ FK orphans detected
- ❌ Target database not ready
- ❌ Backup not confirmed
- ❌ Insufficient disk space

---

## Execution Timeline

### Pre-Execution (Now)
- [ ] Run pre-migration validation checks (5-10 min)
- [ ] Verify all checks pass (green lights)
- [ ] Get go-ahead from stakeholders

### Execution (Main Migration)
- [ ] Start Phase2_MigrationExecution.sql
- [ ] Monitor progress (30-60 min estimated)
- [ ] Watch for errors in output
- [ ] Do NOT interrupt mid-execution

### Post-Execution (Validation)
- [ ] Run post-migration validation (10-15 min)
- [ ] Verify all 59,105 records copied
- [ ] Confirm IsLegacy flags correct
- [ ] Test legacy document download
- [ ] Document completion

### Total Estimated Time
- Pre-migration checks: 10 min
- Migration execution: 60 min
- Post-migration validation: 15 min
- **Total: ~85 minutes (~1.5 hours)**

---

## Success Criteria

**Migration is successful if:**

✅ Pre-migration validation passes  
✅ All 59,105 records copied (0 errors)  
✅ All IsLegacy=1 for ApplicationDocuments  
✅ All FK relationships intact  
✅ Post-migration validation passes  
✅ Legacy documents downloadable via API  
✅ New applications can upload BLOB documents  

**Migration fails if:**

❌ FK constraint violation on re-enable  
❌ Record counts don't match  
❌ IsLegacy flag not set correctly  
❌ Orphaned records detected  
❌ FilePath values corrupted  
❌ DocumentType mapping failures  

---

## Rollback Plan

**If Migration Fails:**

1. **Stop Execution**: Cancel script if still running
2. **Restore Backup**: Restore MMGTDB2A from pre-migration backup
3. **Investigate**: Review error messages in script output
4. **Fix Issues**: Correct data/schema problems
5. **Re-execute**: Run Phase2_MigrationExecution.sql again

**Restore Command** (if needed):
```sql
RESTORE DATABASE MMGTDB2A 
FROM DISK = '\\backup\MMGTDB2A_pre_phase2.bak'
WITH REPLACE, RECOVERY
```

---

## Contact & Escalation

**During Execution**:
- If script hangs: Wait 30 min before force-stop (large INSERTs take time)
- If FK errors: Check source data FK integrity (Step 2c)
- If space errors: Extend database or move temp files

---

## Approval Gate - Ready to Proceed?

**Status**: ✅ All preparation complete

**Awaiting**: 
- [ ] User/Stakeholder confirmation to proceed
- [ ] Pre-migration validation results
- [ ] Final go-ahead signal

---

**Documents Ready for Execution:**

1. **Phase2_MigrationExecution.sql** - Main migration script (6 sections)
2. **Phase2_DataTransformation_Complete.md** - Transformation details
3. **Phase2_Legacy_Document_Investigation.sql** - Validation queries
4. **Phase1_DataTransformation_Strategy.md** - Reference guide

**Next Action**: Execute pre-migration validation, then run main migration

---

*Phase 2: Live Data Migration Execution - Ready for Approval*
