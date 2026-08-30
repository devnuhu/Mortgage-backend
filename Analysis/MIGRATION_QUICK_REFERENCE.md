# MIGRATION QUICK REFERENCE CARD

## 📋 ONE-PAGE SUMMARY

**Migration Type:** Data copy from MMGTDBCOPY → MMGTDB2A  
**Tables:** Users, PMICompanies, Applications, ApplicationDocuments, ApplicationReviewHistory  
**Duration:** ~30-40 minutes (including manual steps)  
**Risk:** LOW  
**Rollback:** Available (full backup restore)

---

## 🚀 QUICK EXECUTION PATH

### Phase 1: Validation (5 min)
```sql
-- Against MMGTDBCOPY
-- Run STEP 1 from MIGRATION_SCRIPTS_COMPLETE.sql
-- Check: All data valid? No orphans? No duplicates?
```

### Phase 2: Migration (10 min)
```sql
-- Against MMGTDB2A
-- Run STEPS 2, 3, 4 from MIGRATION_SCRIPTS_COMPLETE.sql
-- This: Backs up reference data → Clears existing → Inserts 5 tables
```

### Phase 3: Validation (5 min)
```sql
-- Against MMGTDB2A
-- Run STEP 6 from MIGRATION_SCRIPTS_COMPLETE.sql
-- Check: Row counts match? FK integrity? All fields populated?
```

### Phase 4: Manual Steps (10-15 min)
```sql
-- Against MMGTDB2A
-- Manual Step 1: Map ApplicationDocuments.LegacyDocumentType → DocumentTypeId
-- Manual Step 2: Recalculate MandatoryDocumentsComplete
-- Manual Step 3: Validate RsaPin uniqueness
-- Manual Step 4: Test .NET application connectivity
```

---

## 📊 WHAT GETS COPIED

| Table | Rows from Source | Default Behavior |
|-------|------------------|------------------|
| **Users** | All | Direct copy, all columns match |
| **PMICompanies** | All | Direct copy, all columns match |
| **Applications** | All | Copy + 4 new column defaults: DocumentStorageType='FileSystem', MandatoryDocumentsComplete=0, CompletedAt=NULL/UpdatedAt, PhoneNumber=NULL |
| **ApplicationDocuments** | All | Copy + 8 new column defaults: FileContent=NULL, DocumentTypeId=NULL, Version=1, IsLatestVersion=1, ReviewStatus='pending', ReviewComments=NULL, ReviewedBy=NULL, ReviewedAt=NULL, UploadedBy=(from ReviewHistory), IsLegacy=1 |
| **ApplicationReviewHistory** | All | Direct copy, all columns match |

---

## ⚠️ CRITICAL THINGS TO CHECK FIRST

```sql
USE MMGTDBCOPY;
GO

-- 1. Orphaned records?
SELECT COUNT(*) FROM ApplicationDocuments WHERE ApplicationId NOT IN (SELECT Id FROM Applications);
-- Expected: 0

-- 2. Foreign key violations?
SELECT COUNT(*) FROM Applications WHERE UserId NOT IN (SELECT Id FROM Users);
-- Expected: 0

-- 3. Duplicate RsaPin in active statuses?
SELECT COUNT(*) FROM (
  SELECT RsaPin FROM Applications 
  WHERE Status IN ('draft','submitted','under_review','returned','resubmitted') 
  GROUP BY RsaPin HAVING COUNT(*) > 1
) x;
-- Expected: 0

-- 4. NULL FileSize?
SELECT COUNT(*) FROM ApplicationDocuments WHERE FileSize IS NULL;
-- Expected: 0

-- 5. What documents exist? (for post-migration mapping)
SELECT DISTINCT DocumentType FROM ApplicationDocuments;
-- Compare to DocumentTypes table in MMGTDB2A
```

**If ANY of the above return non-zero values → FIX FIRST before migration.**

---

## 🔧 MANUAL MAPPINGS AFTER MIGRATION

### Map DocumentTypeId (REQUIRED)

```sql
USE MMGTDB2A;
GO

-- First, see what legacy types were migrated:
SELECT DISTINCT LegacyDocumentType FROM ApplicationDocuments ORDER BY 1;

-- See what document types exist in target:
SELECT Id, Code, Name FROM DocumentTypes ORDER BY DisplayOrder;

-- Then map them (choose one approach):

-- OPTION A: Map by Name
UPDATE ApplicationDocuments
SET DocumentTypeId = dt.Id
FROM DocumentDocuments ad
JOIN DocumentTypes dt ON ad.LegacyDocumentType = dt.Name
WHERE ad.DocumentTypeId IS NULL;

-- OPTION B: Map by Code
UPDATE ApplicationDocuments
SET DocumentTypeId = dt.Id
FROM ApplicationDocuments ad
JOIN DocumentTypes dt ON ad.LegacyDocumentType = dt.Code
WHERE ad.DocumentTypeId IS NULL;

-- OPTION C: Manual case statement
UPDATE ApplicationDocuments
SET DocumentTypeId = CASE 
	WHEN LegacyDocumentType = 'IdentityDoc' THEN (SELECT Id FROM DocumentTypes WHERE Code = 'ID')
	WHEN LegacyDocumentType = 'Payslip' THEN (SELECT Id FROM DocumentTypes WHERE Code = 'INCOME')
	-- Add more...
	ELSE NULL
END
WHERE DocumentTypeId IS NULL;

-- Verify
SELECT COUNT(*) as StillNull FROM ApplicationDocuments WHERE DocumentTypeId IS NULL;
-- Should return 0
```

### Recalculate MandatoryDocumentsComplete (RECOMMENDED)

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

## ✅ SUCCESS CHECKLIST

Run these after migration to verify success:

```sql
USE MMGTDB2A;
GO

-- 1. Row counts
SELECT 'Users' t, COUNT(*) c FROM Users UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM PMICompanies UNION ALL
SELECT 'Applications', COUNT(*) FROM Applications UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory;

-- 2. FK Integrity (all should return 0)
SELECT COUNT(*) as Orphaned FROM Applications WHERE UserId NOT IN (SELECT Id FROM Users);
SELECT COUNT(*) as Orphaned FROM Applications WHERE PmiCompanyId NOT IN (SELECT Id FROM PMICompanies);
SELECT COUNT(*) as Orphaned FROM ApplicationDocuments WHERE ApplicationId NOT IN (SELECT Id FROM Applications);

-- 3. New column defaults (all should return expected values)
SELECT COUNT(*) c, DocumentStorageType FROM Applications GROUP BY DocumentStorageType;
-- Should show: c=<number>, DocumentStorageType='FileSystem'

SELECT COUNT(*) c, ReviewStatus FROM ApplicationDocuments GROUP BY ReviewStatus;
-- Should show: c=<number>, ReviewStatus='pending'

SELECT COUNT(*) c, IsLegacy FROM ApplicationDocuments GROUP BY IsLegacy;
-- Should show: c=<number>, IsLegacy=1

-- 4. Constraints working
SELECT name FROM sys.foreign_keys WHERE is_disabled = 1;
-- Should return empty (no disabled constraints)

-- 5. RsaPin duplicate check (all should return 0)
SELECT COUNT(*) from (
  SELECT RsaPin FROM Applications 
  WHERE Status IN ('draft','submitted','under_review','returned','resubmitted')
  GROUP BY RsaPin HAVING COUNT(*) > 1
) x;
```

**If ALL checks pass → Migration is SUCCESSFUL**

---

## 🔄 ROLLBACK (If Needed)

```sql
-- Option 1: Restore from full backup (cleanest)
RESTORE DATABASE MMGTDB2A FROM DISK = 'path\to\backup.bak' WITH REPLACE, RECOVERY;

-- Option 2: Restore reference data backups created during Step 2
DELETE FROM DocumentTypes; INSERT INTO DocumentTypes SELECT * FROM DocumentTypes_BACKUP;
DELETE FROM EscalationRules; INSERT INTO EscalationRules SELECT * FROM EscalationRules_BACKUP;
DELETE FROM SystemConfigurations; INSERT INTO SystemConfigurations SELECT * FROM SystemConfigurations_BACKUP;

TRUNCATE TABLE ApplicationReviewHistory;
TRUNCATE TABLE ApplicationDocuments;
TRUNCATE TABLE Applications;
DELETE FROM Users;
DELETE FROM PMICompanies;
```

---

## 📂 FILE LOCATIONS

- **Full migration script:** `Analysis/MIGRATION_SCRIPTS_COMPLETE.sql`
- **Detailed guide:** `Analysis/MIGRATION_EXECUTION_GUIDE.md`
- **This quick ref:** `Analysis/MIGRATION_QUICK_REFERENCE.md`

---

## ⏱️ TIMELINE AT A GLANCE

| Step | Action | Time | Risk |
|------|--------|------|------|
| 1 | Run validation against MMGTDBCOPY | 2 min | 🟢 None |
| 2 | Backup reference data in MMGTDB2A | 1 min | 🟢 None |
| 3 | Clear existing data in MMGTDB2A | 2 min | 🟠 Stops app briefly |
| 4 | Insert 5 tables (migration) | 5 min | 🟠 Most critical |
| 5 | Re-enable constraints | 1 min | 🟢 None |
| 6 | Validation checks | 2 min | 🟢 None |
| **Manual 1** | **DocumentTypeId mapping** | **5-10 min** | 🟠 Required |
| **Manual 2** | **Recalc MandatoryDocs** | **2 min** | 🟢 Optional but recommended |
| **Manual 3** | **RsaPin validation** | **1 min** | 🟢 Verification only |
| **Manual 4** | **App connectivity test** | **5 min** | 🟠 Verifies success |
| | **TOTAL** | **~30-40 min** | ⚠️ Plan downtime |

---

## 🎯 GO/NO-GO DECISION

**PROCEED with migration IF:**
- ✅ Backed up MMGTDB2A
- ✅ Validation queries all pass (no orphans, no violations)
- ✅ Reference data (DocumentTypes, EscalationRules) exists in MMGTDB2A
- ✅ Application stopped/no user activity planned during migration
- ✅ All stakeholders notified of ~45 min downtime

**DO NOT PROCEED IF:**
- ❌ Validation queries show any failures
- ❌ No backup exists
- ❌ Users need access to MMGTDB2A during migration
- ❌ DocumentTypes table missing or empty in MMGTDB2A

---

## 📞 DURING MIGRATION

If you encounter errors, collect:
1. Exact error message
2. Which step failed (validation, backup, clear, migrate, or post-validation)
3. Row counts from validation step 1
4. Last print statement before error

Contact database architect with this information.
