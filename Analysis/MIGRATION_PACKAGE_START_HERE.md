# MIGRATION PACKAGE - START HERE

## 📦 What You've Received

Complete, production-ready data migration from MMGTDBCOPY to MMGTDB2A including:

1. **MIGRATION_SCRIPTS_COMPLETE.sql** - All executable T-SQL scripts (6 steps)
2. **MIGRATION_EXECUTION_GUIDE.md** - Detailed step-by-step instructions with screenshots conceptually
3. **MIGRATION_QUICK_REFERENCE.md** - One-page cheat sheet for during execution
4. **MIGRATION_READINESS_ASSESSMENT_FINAL_REPORT.md** - Full technical assessment

---

## 🎯 Quick Start (3 Steps)

### Step 1: Read the Assessment
Open: **MIGRATION_READINESS_ASSESSMENT_FINAL_REPORT.md**  
Time: 10 minutes  
Purpose: Understand what's being migrated and risks

### Step 2: Run Pre-Migration Validation
Execute: **STEP 1** from **MIGRATION_SCRIPTS_COMPLETE.sql** against MMGTDBCOPY  
Time: 5 minutes  
Purpose: Verify source data is clean

### Step 3: Execute Migration
Execute: **STEPS 2-6** from **MIGRATION_SCRIPTS_COMPLETE.sql** against MMGTDB2A  
Time: 20-30 minutes  
Purpose: Copy all data, validate, complete manual mappings

---

## 📋 Pre-Migration Checklist

Before you start, ensure:

- [ ] MMGTDB2A is fully backed up
- [ ] MMGTDBCOPY is fully backed up
- [ ] .NET application is STOPPED
- [ ] You have SQL Server Management Studio access
- [ ] You can connect to: 172.16.50.42 with sa / Sig123456#
- [ ] You understand DocumentType mapping (see Manual Step 1)

---

## 🚀 Execution Flow

```
1. READ: MIGRATION_READINESS_ASSESSMENT_FINAL_REPORT.md (10 min)
   ↓
2. BACKUP: Both databases (already done? ✓)
   ↓
3. STEP 1: Run validation against MMGTDBCOPY (5 min)
   - Check: All pass? No errors? ✓
   ↓
4. STEP 2: Backup reference data in MMGTDB2A (1 min)
   - Creates: DocumentTypes_BACKUP, EscalationRules_BACKUP, etc.
   ↓
5. STEP 3: Clear existing data in MMGTDB2A (2 min)
   - Truncates: Users, Applications, ApplicationDocuments, etc.
   ↓
6. STEP 4: Migrate data (5 min)
   - Inserts: All 5 tables with defaults for new columns
   ↓
7. STEP 6: Post-migration validation (5 min)
   - Verify: Row counts match, FK integrity OK
   ↓
8. MANUAL STEP 1: Map DocumentTypeId (10 min)
   - CRITICAL: Map LegacyDocumentType → DocumentTypes.Id
   ↓
9. MANUAL STEP 2: Recalculate MandatoryDocumentsComplete (2 min)
   ↓
10. MANUAL STEP 3: Validate RsaPin uniqueness (1 min)
   ↓
11. MANUAL STEP 4: Test .NET application (5 min)
   - Start app, verify data loads correctly
   ↓
12. DONE! ✅
```

**Total Time: ~45 minutes**

---

## 📊 What Gets Copied

**5 Core Tables:**
- Users (all rows)
- PMICompanies (all rows)
- Applications (all rows + 4 new column defaults)
- ApplicationDocuments (all rows + 9 new column defaults)
- ApplicationReviewHistory (all rows)

**Reference Data Preserved (NOT cleared):**
- DocumentTypes
- EscalationRules
- SystemConfigurations

**New Empty Tables (auto-created by MMGTDB2A):**
- ApplicationAssignments
- ReviewerPerformanceMetrics
- ReviewerAvailabilities
- AssignmentEscalations
- ReassignmentHistories
- ApplicationDocumentHistory
- DocumentTypeAuditLogs

---

## ⚠️ Critical Steps You'll Need to Run Manually

After the automated migration, you MUST complete these:

### Manual Step 1: Map DocumentTypeId (CRITICAL)

The ApplicationDocuments table will have NULL DocumentTypeId values. You must map these based on the LegacyDocumentType column.

```sql
-- See what types exist
SELECT DISTINCT LegacyDocumentType FROM ApplicationDocuments;

-- See what types are available in target
SELECT Id, Code, Name FROM DocumentTypes;

-- Then map them (one of these approaches):
UPDATE ApplicationDocuments
SET DocumentTypeId = dt.Id
FROM ApplicationDocuments ad
JOIN DocumentTypes dt ON ad.LegacyDocumentType = dt.Name
WHERE ad.DocumentTypeId IS NULL;
```

See **MANUAL STEP 1** section in MIGRATION_EXECUTION_GUIDE.md for detailed steps.

### Manual Step 2: Recalculate MandatoryDocumentsComplete

Ensure Applications table shows correct mandatory document completion status:

```sql
UPDATE Applications
SET MandatoryDocumentsComplete = CASE 
	WHEN EXISTS (
		SELECT 1 FROM ApplicationDocuments ad
		WHERE ad.ApplicationId = Applications.Id
		AND ad.DocumentTypeId IN (SELECT Id FROM DocumentTypes WHERE IsMandatory = 1)
	) THEN 1
	ELSE 0
END;
```

---

## 🔍 Verification

After everything is done, run these to confirm success:

```sql
USE MMGTDB2A;

-- 1. Row counts should match MMGTDBCOPY
SELECT 'Users', COUNT(*) FROM Users
UNION ALL SELECT 'PMICompanies', COUNT(*) FROM PMICompanies
UNION ALL SELECT 'Applications', COUNT(*) FROM Applications
UNION ALL SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory;

-- 2. Should all return 0 (no orphaned records)
SELECT COUNT(*) FROM Applications WHERE UserId NOT IN (SELECT Id FROM Users);
SELECT COUNT(*) FROM ApplicationDocuments WHERE ApplicationId NOT IN (SELECT Id FROM Applications);

-- 3. Should have no NULL DocumentTypeId after Manual Step 1
SELECT COUNT(*) FROM ApplicationDocuments WHERE DocumentTypeId IS NULL;
-- Expected: 0

-- 4. Application should start without errors
-- Start your .NET app and verify it loads data
```

---

## 📁 File Locations

All migration files are in: `Analysis/` directory

```
Analysis/
├── MIGRATION_SCRIPTS_COMPLETE.sql                    ← Main execution file
├── MIGRATION_EXECUTION_GUIDE.md                      ← Detailed instructions
├── MIGRATION_QUICK_REFERENCE.md                      ← Cheat sheet
├── MIGRATION_READINESS_ASSESSMENT_FINAL_REPORT.md    ← Full assessment
└── MIGRATION_PACKAGE_START_HERE.md                   ← This file
```

---

## ⏱️ Timeline Summary

| Phase | Duration | Critical? |
|-------|----------|-----------|
| Pre-migration setup | 10 min | Yes - backup MMGTDB2A |
| Validation (STEP 1) | 5 min | Yes - find data issues early |
| Backup ref data (STEP 2) | 1 min | Yes - preserve reference tables |
| Clear data (STEP 3) | 2 min | ⚠️ Most critical step |
| Migrate (STEP 4) | 5 min | ⚠️ Data transfer, most critical |
| Validate (STEP 6) | 5 min | Yes - verify success |
| **Manual Steps 1-4** | **20 min** | ⚠️ Manual Step 1 is critical |
| **TOTAL** | **~50 min**| - |

**User-Facing Downtime:** ~10-15 minutes (during STEPS 3-5)

---

## 🛑 If Something Goes Wrong

### Immediate Rollback (5 minutes)

```sql
-- Restore from backup
RESTORE DATABASE MMGTDB2A 
FROM DISK = 'path\to\MMGTDB2A_BACKUP.bak'
WITH REPLACE, RECOVERY;

-- Then investigate the issue and try again
```

### Troubleshooting Guide

See **Troubleshooting** section in MIGRATION_EXECUTION_GUIDE.md for:
- Common error messages and solutions
- Pre-flight validation failures
- FK constraint violations
- DocumentType mapping issues

---

## ✅ Success Criteria

You'll know migration is successful when:

1. ✅ All row counts match between source and target
2. ✅ No foreign key violations (all checks return 0)
3. ✅ Applications.DocumentStorageType = 'FileSystem' for all rows
4. ✅ ApplicationDocuments.DocumentTypeId is NOT NULL (after Manual Step 1)
5. ✅ ApplicationDocuments.ReviewStatus = 'pending' for all rows
6. ✅ ApplicationDocuments.IsLegacy = 1 for all rows
7. ✅ .NET application starts and loads data correctly
8. ✅ No application errors in logs

---

## 📞 Getting Help

**During Migration:**
1. Check error message carefully
2. Look up error in TROUBLESHOOTING section of MIGRATION_EXECUTION_GUIDE.md
3. If data quality issue: Review PRE-MIGRATION VALIDATION results
4. If technical issue: Contact Database Architect with exact error and step number

**Common Issues:**
- **FK Violation** → Source data has orphaned records → Pre-flight validation should catch this
- **DocumentTypeId NULL** → You haven't completed Manual Step 1 yet → Expected
- **App won't start** → Likely connection string still pointing to MMGTDBCOPY → Update appsettings.json
- **Constraint violation on RsaPin** → Duplicate active applications with same PIN → Pre-flight validation should catch

---

## 🎓 Understanding the Migration

### Why This Approach?

- ✅ Direct copy minimizes complexity
- ✅ New columns get sensible defaults
- ✅ No custom transformation logic needed
- ✅ Fast (5-10 minutes for actual data migration)
- ✅ Safe (full rollback available)

### What Are These New Columns?

**In Applications:**
- `DocumentStorageType` = 'FileSystem' (legacy) vs 'Database' (new) - indicates where files are stored
- `MandatoryDocumentsComplete` = 1/0 - whether all required docs are uploaded for this app
- `CompletedAt` = When the application review finished
- `PhoneNumber` = Borrower phone (new field, set to NULL initially)

**In ApplicationDocuments:**
- `FileContent` = Binary file stored in database (legacy uses FilePath instead)
- `DocumentTypeId` = Link to DocumentTypes table (YOU must map this - Manual Step 1)
- `Version` = 1 for all migrated (supports multi-version document tracking)
- `IsLatestVersion` = 1 for all migrated (assumes all current versions)
- `ReviewStatus` = 'pending' waiting for review
- `IsLegacy` = 1 - marks as migrated from old system
- Other review fields = NULL (populated as docs are reviewed)

---

## 🎯 Next Steps

1. **Read** MIGRATION_READINESS_ASSESSMENT_FINAL_REPORT.md (understand the migration)
2. **Backup** MMGTDB2A (critical safety step)
3. **Execute** STEP 1 from MIGRATION_SCRIPTS_COMPLETE.sql (validate source data)
4. **Execute** STEPS 2-6 from MIGRATION_SCRIPTS_COMPLETE.sql (run migration)
5. **Complete** Manual Steps 1-4 (finalize migration)
6. **Test** .NET application (verify it works)

---

## 📝 Sign-Off Template

When migration is complete, fill in:

```
MIGRATION SIGN-OFF
==================

Database: MMGTDB2A
Source: MMGTDBCOPY
Date Completed: ______________
Completed By: ______________

✓ Pre-migration backup taken: YES / NO
✓ STEP 1 validation completed: YES / NO  
✓ All validation checks passed: YES / NO
✓ STEP 2-6 migration completed: YES / NO
✓ Post-migration validation passed: YES / NO
✓ Manual Step 1 (DocumentTypeId mapping): COMPLETE / INCOMPLETE
✓ Manual Step 2 (MandatoryDocumentsComplete): COMPLETE / INCOMPLETE
✓ Application connectivity verified: YES / NO
✓ All tests passing: YES / NO

Total Row Counts (after migration):
- Users: ________
- PMICompanies: ________
- Applications: ________
- ApplicationDocuments: ________
- ApplicationReviewHistory: ________

Notes:
________________________________________________________________________
________________________________________________________________________

Approval: ________________________  Date: ______________
```

---

## 🚀 Ready?

Everything you need is in this package. Follow the execution guide, and you'll have your data migrated in ~45 minutes.

**Start here:** Open **MIGRATION_READINESS_ASSESSMENT_FINAL_REPORT.md**

Then execute **MIGRATION_SCRIPTS_COMPLETE.sql** following **MIGRATION_EXECUTION_GUIDE.md**

Good luck! 🎯
