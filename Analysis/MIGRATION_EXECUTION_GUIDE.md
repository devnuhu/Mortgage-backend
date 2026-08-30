# DATA MIGRATION EXECUTION GUIDE
## MMGTDBCOPY → MMGTDB2A

**Status:** Ready to Execute | **Risk Level:** LOW | **Tables Migrating:** 5 Core Tables | **Duration:** ~5-10 minutes

---

## TABLES BEING MIGRATED

| # | Table | Source Rows | Action | Risk |
|---|-------|------------|--------|------|
| 1 | Users | ? | Copy all | ✅ Low |
| 2 | PMICompanies | ? | Copy all | ✅ Low |
| 3 | Applications | ? | Copy + set defaults | ✅ Low |
| 4 | ApplicationDocuments | ? | Copy + set defaults | ✅ Low |
| 5 | ApplicationReviewHistory | ? | Copy all | ✅ Low |

**NOT Being Migrated (Reference Data Preserved in MMGTDB2A):**
- DocumentTypes
- EscalationRules
- SystemConfigurations

---

## PRE-EXECUTION CHECKLIST

Before running the migration scripts, **verify these conditions:**

- [ ] **Backed up MMGTDB2A** (full database backup)
- [ ] **Backed up MMGTDBCOPY** (for historical reference)
- [ ] **Have access to both databases** via SQL Server Management Studio
- [ ] **Network connectivity** confirmed to SQL Server (172.16.50.42)
- [ ] **No user activity** on MMGTDB2A during migration (application stopped)
- [ ] **Executed validation queries** against MMGTDBCOPY (see step below)

### Run Pre-Migration Validation First

Execute **STEP 1** from `MIGRATION_SCRIPTS_COMPLETE.sql` against **MMGTDBCOPY**:

```sql
USE MMGTDBCOPY;
GO
-- [Run all validation queries from STEP 1]
```

**Expected Results:**
- All row counts should show data
- All orphaned/FK checks should return 0
- No duplicate RsaPin in active statuses
- No NULL FileSize or FileName

**If any validation FAILS:** Do NOT proceed with migration. Fix the data issue first.

---

## STEP-BY-STEP EXECUTION

### STEP 1: Connection Setup

1. Open **SQL Server Management Studio**
2. Connect to: `172.16.50.42` with `sa` / `Sig123456#`
3. Verify both databases are accessible:
   ```sql
   USE MMGTDBCOPY;
   SELECT @@VERSION;

   USE MMGTDB2A;
   SELECT @@VERSION;
   ```

### STEP 2: Run Pre-Migration Validation

Execute the **STEP 1 validation script** from `MIGRATION_SCRIPTS_COMPLETE.sql` against MMGTDBCOPY.

**Document the output** - you'll need these row counts to verify success.

**Example Expected Output:**
```
Users: 25
PMICompanies: 5
Applications: 150
ApplicationDocuments: 450
ApplicationReviewHistory: 800
```

**If YOU SEE ERRORS:** STOP and investigate. Do NOT proceed to Step 3.

### STEP 3: Backup Reference Data

Execute **STEP 2** from `MIGRATION_SCRIPTS_COMPLETE.sql` against **MMGTDB2A**:

```sql
USE MMGTDB2A;
GO
-- [Run backup commands from STEP 2]
```

This creates:
- `DocumentTypes_BACKUP`
- `EscalationRules_BACKUP`
- `SystemConfigurations_BACKUP`

**Purpose:** If something goes wrong, you can restore these reference tables.

### STEP 4: Clear Existing Data

Execute **STEP 3** from `MIGRATION_SCRIPTS_COMPLETE.sql` against **MMGTDB2A**:

```sql
USE MMGTDB2A;
GO
-- [Run clear data commands from STEP 3]
```

**What happens:**
- Temporarily disables all foreign key constraints
- Deletes data from all 11 tables in reverse dependency order
- Leaves reference data intact (DocumentTypes, EscalationRules, SystemConfigurations)

**Expected:** No errors. All DELETE commands complete successfully.

### STEP 5: Migrate Data

Execute **STEP 4** from `MIGRATION_SCRIPTS_COMPLETE.sql` against **MMGTDB2A**:

```sql
USE MMGTDB2A;
GO
-- [Run migration inserts from STEP 4 - MIGRATION 1 through MIGRATION 5]
```

**This runs 5 INSERT statements in order:**
1. **MIGRATION 1:** Users
2. **MIGRATION 2:** PMICompanies
3. **MIGRATION 3:** Applications (with new column defaults)
4. **MIGRATION 4:** ApplicationDocuments (with new column defaults)
5. **MIGRATION 5:** ApplicationReviewHistory

**Expected Output (example):**
```
MIGRATION 1: Copying Users from MMGTDBCOPY...
Users migration completed.

MIGRATION 2: Copying PMICompanies from MMGTDBCOPY...
PMICompanies migration completed.

MIGRATION 3: Copying Applications from MMGTDBCOPY...
Applications migration completed.

MIGRATION 4: Copying ApplicationDocuments from MMGTDBCOPY...
ApplicationDocuments migration completed.

MIGRATION 5: Copying ApplicationReviewHistory from MMGTDBCOPY...
ApplicationReviewHistory migration completed.

Re-enabling constraints...
Constraints re-enabled.

===== DATA MIGRATION COMPLETED SUCCESSFULLY =====
```

**If YOU GET ERRORS:**
- Note the exact error message
- Check the FK constraint error details
- Review PRE-MIGRATION VALIDATION results - likely a data integrity issue
- **Do NOT proceed** - escalate to database architect

### STEP 6: Re-Enable Constraints

**Already included in STEP 4**, but verify by running:

```sql
USE MMGTDB2A;
GO

-- Verify constraints are active
SELECT name, is_disabled
FROM sys.foreign_keys
WHERE parent_object_id IN (
	OBJECT_ID('Applications'),
	OBJECT_ID('ApplicationDocuments'),
	OBJECT_ID('ApplicationReviewHistory')
)
ORDER BY name;
-- Expected: All should show is_disabled = 0 (false)
```

### STEP 7: Post-Migration Validation

Execute **STEP 6** from `MIGRATION_SCRIPTS_COMPLETE.sql` against **MMGTDB2A**:

```sql
USE MMGTDB2A;
GO
-- [Run validation queries from STEP 6]
```

**Verify:**
- [ ] Row counts match MMGTDBCOPY (from Step 2)
- [ ] No orphaned foreign keys (all checks return 0)
- [ ] No NULL values in critical fields
- [ ] DocumentTypeId is NULL (expected - requires manual mapping)

**Example Expected:**
```
Users: 25
PMICompanies: 5
Applications: 150
ApplicationDocuments: 450
ApplicationReviewHistory: 800

FK Integrity: 0 orphaned records
NULL checks: All return 0
DocumentTypeId NULL count: 450 (expected - see manual steps)
```

---

## MANUAL STEPS AFTER MIGRATION

### MANUAL STEP 1: Map ApplicationDocuments to DocumentTypes

**Problem:** ApplicationDocuments.DocumentTypeId is NULL. It needs to reference DocumentTypes.Id.

**See what you have:**

```sql
USE MMGTDB2A;
GO

-- Check legacy document types that were migrated
SELECT DISTINCT LegacyDocumentType 
FROM ApplicationDocuments
ORDER BY LegacyDocumentType;

-- Check available document types in target
SELECT Id, Code, Name, IsMandatory
FROM DocumentTypes
ORDER BY DisplayOrder;
```

**Then map them:**

**Option A: If LegacyDocumentType matches DocumentTypes.Name**
```sql
UPDATE ApplicationDocuments
SET DocumentTypeId = dt.Id
FROM ApplicationDocuments ad
JOIN DocumentTypes dt ON ad.LegacyDocumentType = dt.Name
WHERE ad.DocumentTypeId IS NULL;
```

**Option B: If LegacyDocumentType matches DocumentTypes.Code**
```sql
UPDATE ApplicationDocuments
SET DocumentTypeId = dt.Id
FROM ApplicationDocuments ad
JOIN DocumentTypes dt ON ad.LegacyDocumentType = dt.Code
WHERE ad.DocumentTypeId IS NULL;
```

**Option C: Manual mapping table (if no automatic match)**
```sql
-- Create mapping if needed
-- Then use case statement to populate based on specific matches
UPDATE ApplicationDocuments
SET DocumentTypeId = CASE 
	WHEN LegacyDocumentType = 'IdentityDocument' THEN (SELECT Id FROM DocumentTypes WHERE Code = 'ID_CARD')
	WHEN LegacyDocumentType = 'ProofOfIncome' THEN (SELECT Id FROM DocumentTypes WHERE Code = 'PAYSLIP')
	-- Add more cases as needed
	ELSE NULL
END
WHERE DocumentTypeId IS NULL;
```

**Verify the mapping worked:**
```sql
SELECT COUNT(*) AS StillNullDocTypeId
FROM ApplicationDocuments
WHERE DocumentTypeId IS NULL;
-- Should return 0 after successful mapping
```

### MANUAL STEP 2: Recalculate MandatoryDocumentsComplete

```sql
USE MMGTDB2A;
GO

-- Update Applications.MandatoryDocumentsComplete based on actual documents
UPDATE Applications
SET MandatoryDocumentsComplete = CASE 
	WHEN EXISTS (
		SELECT 1 
		FROM ApplicationDocuments ad
		WHERE ad.ApplicationId = Applications.Id
		  AND ad.DocumentTypeId IN (SELECT Id FROM DocumentTypes WHERE IsMandatory = 1)
	) THEN 1
	ELSE 0
END;

-- Verify
SELECT Status, MandatoryDocumentsComplete, COUNT(*) as Count
FROM Applications
GROUP BY Status, MandatoryDocumentsComplete;
```

### MANUAL STEP 3: Validate RsaPin Uniqueness

```sql
USE MMGTDB2A;
GO

-- Check for any duplicate RsaPin in active statuses (should be 0)
SELECT RsaPin, COUNT(*) as DupCount
FROM Applications
WHERE Status IN ('draft', 'submitted', 'under_review', 'returned', 'resubmitted')
GROUP BY RsaPin
HAVING COUNT(*) > 1;

-- Expected: (No rows returned)
```

### MANUAL STEP 4: Test Application Connectivity

1. **Stop the .NET application** (if running)
2. **Update appsettings.json** to ensure it's pointing to MMGTDB2A:
   ```json
   "DefaultConnection": "Server=172.16.50.42;Database=MMGTDB2A;User Id=sa;Password=Sig123456#;TrustServerCertificate=True;MultipleActiveResultSets=True;"
   ```
3. **Start the application**
4. **Run these tests:**
   - [ ] Login with a migrated user
   - [ ] View all applications list
   - [ ] View specific application details
   - [ ] Check that documents are loaded for applications
   - [ ] Check review history displays correctly
   - [ ] Run any existing tests in your test suite

**If you see errors:**
- Check the error message
- Review the database logs
- Likely issues:
  - Missing DocumentTypeId values (didn't complete Manual Step 1)
  - Constraint violations (data integrity issue)
  - Connection string still pointing to old database

---

## ROLLBACK PROCEDURE (If Something Goes Wrong)

**If migration fails and you need to rollback:**

### Option 1: Restore from Backup (Cleanest)

```sql
-- Stop application
-- Restore from backup taken before migration
RESTORE DATABASE MMGTDB2A 
FROM DISK = 'C:\path\to\MMGTDB2A_BACKUP_BEFORE_MIGRATION.bak'
WITH REPLACE, RECOVERY;

-- Restart application
```

### Option 2: Restore Specific Tables from STEP 2 Backups

```sql
USE MMGTDB2A;
GO

-- Clear migrated data
TRUNCATE TABLE ApplicationReviewHistory;
TRUNCATE TABLE ApplicationDocuments;
TRUNCATE TABLE Applications;
DELETE FROM Users;
DELETE FROM PMICompanies;

-- Restore reference data backups
DELETE FROM DocumentTypes;
INSERT INTO DocumentTypes SELECT * FROM DocumentTypes_BACKUP;

DELETE FROM EscalationRules;
INSERT INTO EscalationRules SELECT * FROM EscalationRules_BACKUP;

DELETE FROM SystemConfigurations;
INSERT INTO SystemConfigurations SELECT * FROM SystemConfigurations_BACKUP;
```

---

## MONITORING & LOGGING

### During Migration

Monitor SQL Server for:
- Transaction log growth
- Lock contention
- Query execution time

### Check Migration Status

```sql
USE MMGTDB2A;
GO

-- Get row counts
SELECT 'Users' AS Table_Name, COUNT(*) AS Migrated_Rows FROM Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory;

-- Check for any NULL DocumentTypeId (should manually map these)
SELECT COUNT(*) as UnmappedDocTypes FROM ApplicationDocuments WHERE DocumentTypeId IS NULL;

-- Check constraint violations
SELECT * FROM sys.dm_server_audit_status;
```

---

## SUCCESS CRITERIA

Migration is **SUCCESSFUL** when:

- ✅ All 5 tables have matching row counts to MMGTDBCOPY
- ✅ All FK integrity checks return 0 orphaned records
- ✅ No NULL values in critical non-nullable fields
- ✅ Applications.DocumentStorageType = 'FileSystem' for all rows
- ✅ ApplicationDocuments.ReviewStatus = 'pending' for all rows
- ✅ ApplicationDocuments.IsLegacy = 1 for all rows
- ✅ Manual Step 1 (DocumentTypeId mapping) completed
- ✅ Manual Step 2 (MandatoryDocumentsComplete recalc) completed
- ✅ .NET application loads data correctly
- ✅ All tests pass

---

## ESTIMATED TIMELINE

| Phase | Duration | Notes |
|-------|----------|-------|
| Pre-migration validation | 2 min | Quick integrity checks |
| Reference data backup | 1 min | Create backup tables |
| Clear existing data | 2 min | Truncate target tables |
| Data migration | 5 min | Insert 5 tables |
| Constraint re-enable | 1 min | Verify FK constraints |
| Post-migration validation | 2 min | Verify row counts & integrity |
| **Manual Steps 1-2** | **10-15 min** | DocumentTypeId mapping, recalculate flags |
| Application testing | 5-10 min | Run connectivity tests |
| **TOTAL** | **~30-40 minutes** | End-to-end with all manual steps |

---

## SUPPORT & TROUBLESHOOTING

### Common Issues & Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| FK constraint violation | Orphaned records in source | Review PRE-MIGRATION validation results |
| NULL FileName/FileSize | Data quality issue | Check ApplicationDocuments table in MMGTDBCOPY |
| Duplicate RsaPin error | Multiple active apps same PIN | Query MMGTDBCOPY for duplicates, resolve before migration |
| DocumentTypeId still NULL after mapping | Mismatch between LegacyDocumentType and DocumentTypes | Manually inspect and create mapping |
| Application won't connect after migration | Connection string or database access | Verify appsettings.json, verify user has DB permissions |
| Constraint violations after Step 5 | Data integrity issue | Check with Step 6 validation queries |

### Getting Help

1. **Check the error message** - Copy exact error code
2. **Review validation results** - Are source data valid?
3. **Check the SQL Server error logs** - `SQL Server Management Studio → View Error Logs`
4. **Verify FK constraints** - `SELECT * FROM sys.foreign_keys WHERE ...`
5. **If stuck** - Restore from backup and investigate with team

---

## FINAL SIGN-OFF CHECKLIST

Before declaring migration **COMPLETE**:

- [ ] Backed up MMGTDB2A before migration
- [ ] Ran STEP 1 PRE-MIGRATION VALIDATION
- [ ] Ran STEP 2 BACKUP REFERENCE DATA
- [ ] Ran STEP 3 CLEAR DATA
- [ ] Ran STEP 4 MIGRATE DATA (no errors)
- [ ] Ran STEP 5 RE-ENABLE CONSTRAINTS (already in Step 4)
- [ ] Ran STEP 6 POST-MIGRATION VALIDATION
- [ ] Row counts match (compared to MMGTDBCOPY)
- [ ] All FK integrity checks = 0
- [ ] Completed MANUAL STEP 1 (DocumentTypeId mapping)
- [ ] Completed MANUAL STEP 2 (MandatoryDocumentsComplete)
- [ ] Completed MANUAL STEP 3 (RsaPin validation)
- [ ] Completed MANUAL STEP 4 (Application connectivity test)
- [ ] All application tests pass
- [ ] Team agrees migration is complete

---

**Ready to migrate?** Start with STEP 1: Pre-Migration Validation from `MIGRATION_SCRIPTS_COMPLETE.sql`

All scripts are in: `Analysis/MIGRATION_SCRIPTS_COMPLETE.sql`
