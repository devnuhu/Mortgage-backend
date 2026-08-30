# Phase 2 Migration: Third Wave of Schema Fixes Applied

## Date: 2026-07-08
## Status: READY FOR RE-EXECUTION

---

## Summary of All Fixes Applied to Phase2_COMPLETE_ALL_IN_ONE.sql

### Fix #1: Applications.Priority NOT NULL Column ✓
**Line 227, 231**
- Added `Priority` column to INSERT list
- Defaulted to `0` (Normal priority level)
- Source: No corresponding column in MMGTDBCOPY
- Target: Applications.Priority NOT NULL, no default

**Before:**
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	Id, ApplicationNumber, UserId, PMICompanyId, LoanAmount, 
	Status, CreatedAt, UpdatedAt
)
SELECT 
	a.Id, a.ApplicationNumber, a.UserId, a.PMICompanyId, a.LoanAmount, 
	a.Status, a.CreatedAt, a.UpdatedAt
```

**After:**
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	Id, ApplicationNumber, UserId, PMICompanyId, LoanAmount, 
	Status, CreatedAt, UpdatedAt, Priority
)
SELECT 
	a.Id, a.ApplicationNumber, a.UserId, a.PMICompanyId, a.LoanAmount, 
	a.Status, a.CreatedAt, a.UpdatedAt, 0 as Priority
```

---

### Fix #2: ApplicationReviewHistory.Status NOT NULL Column ✓
**Line 334, 337**
- Added `Status` column to INSERT list
- Defaulted to `'Completed'` (review history records are completed)
- Source: No corresponding column in MMGTDBCOPY
- Target: ApplicationReviewHistory.Status NOT NULL, no default

**Before:**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
	Id, ApplicationId, ReviewedById, CreatedAt
)
SELECT 
	arh.Id, arh.ApplicationId, arh.ReviewedById, arh.CreatedAt
```

**After:**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
	Id, ApplicationId, ReviewedById, CreatedAt, Status
)
SELECT 
	arh.Id, arh.ApplicationId, arh.ReviewedById, arh.CreatedAt, 'Completed' as Status
```

---

### Fix #3: Temp Table Idempotency ✓
**Line 245-250**
- Added `DROP TABLE IF EXISTS` checks before creating #UniqueDocTypes
- Added `DROP TABLE IF EXISTS` checks before creating #DocTypeMapping
- Prevents "already exists" error on reruns
- Added error handling to clean up on failure

**Before:**
```sql
SELECT DISTINCT ad.DocumentType as SourceType
INTO #UniqueDocTypes
```

**After:**
```sql
IF OBJECT_ID('tempdb..#UniqueDocTypes', 'U') IS NOT NULL 
	DROP TABLE #UniqueDocTypes
IF OBJECT_ID('tempdb..#DocTypeMapping', 'U') IS NOT NULL 
	DROP TABLE #DocTypeMapping

SELECT DISTINCT ad.DocumentType as SourceType
INTO #UniqueDocTypes
```

---

### Fix #4: FK Constraints Handling ✓
**Lines 150 (NOCHECK), 366 (CHECK)**
- Added `DocumentTypes` table to NOCHECK CONSTRAINT ALL
- Added `DocumentTypes` table to CHECK CONSTRAINT ALL
- Prevents FK conflict when re-enabling constraints after DocumentTypes.CreatedBy updates
- Ensures clean constraint re-enable after migration

**Before (NOCHECK):**
```sql
ALTER TABLE MMGTDB2A.dbo.ApplicationDocuments NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationReviewHistory NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Applications NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Users NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.PMICompanies NOCHECK CONSTRAINT ALL
```

**After (NOCHECK):**
```sql
ALTER TABLE MMGTDB2A.dbo.ApplicationDocuments NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationReviewHistory NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Applications NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Users NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.PMICompanies NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.DocumentTypes NOCHECK CONSTRAINT ALL
```

---

### Fix #5: Users Deletion Removed ✓
**Line 167-168**
- REMOVED `DELETE FROM Users` statement
- Preserves seeded admin accounts (superadmin@pfa.com, admin@pfa.com)
- Users migration uses duplicate-check so existing users are preserved & new ones inserted
- Prevents circular FK dependency: DocumentTypes.CreatedBy → Users.Id

**Before:**
```sql
DELETE FROM MMGTDB2A.dbo.Users WHERE 1=1
PRINT 'Users: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'
```

**After:**
```sql
-- Skip Users delete to preserve seeded admin accounts (superadmin@pfa.com, admin@pfa.com)
-- Users migration will use duplicate checking to preserve existing seeded users
PRINT 'Users: Skipping delete to preserve seeded admin accounts'
```

---

## Expected Behavior After These Fixes

### Section 2: PREPARE TARGET TABLES
- FK constraints disabled on 6 tables (was 5)
- Users NOT deleted - seeded accounts preserved
- PMICompanies deleted and ready for reload
- Temp tables cleaned up if present from prior runs

### Section 3: MIGRATE DATA
- **3.1 PMICompanies**: 24 rows migrated successfully
- **3.2 Users**: 0 rows (duplicates skipped) or new users inserted, no errors
- **3.3 Applications**: Should migrate all rows with Priority = 0, NO NULL errors
- **3.4 DocumentType mapping**: Temp tables created cleanly, mapping built, unmapped → identity_document
- **3.5 ApplicationDocuments**: Should migrate all rows, NO #DocTypeMapping missing error
- **3.6 ApplicationReviewHistory**: Should migrate all rows with Status = 'Completed', NO NULL errors

### Section 4: RE-ENABLE CONSTRAINTS
- FK constraints re-enabled on 6 tables (was 5)
- DocumentTypes constraints properly re-enabled

### Section 5: POST-MIGRATION VALIDATION
- Should see complete row counts for all migrated data
- No foreign key integrity violations

---

## Previous Fixes Still in Place

### From First Wave:
1. ✓ identity_document creation with proper CreatedBy
2. ✓ Applications: removed invalid DecisionBy/DecisionAt columns
3. ✓ ApplicationDocuments: added FileType='Legacy' column
4. ✓ ApplicationReviewHistory: removed invalid ReviewNotes/ApprovedAt columns

---

## Migration Readiness Checklist

- [x] Applications.Priority handled (default 0)
- [x] ApplicationReviewHistory.Status handled (default 'Completed')
- [x] Temp tables cleaned up on rerun (#UniqueDocTypes, #DocTypeMapping)
- [x] FK constraints include DocumentTypes
- [x] Users deletion removed (preserve seeded admins)
- [x] All previous schema mismatches fixed
- [x] Script verified for syntax correctness

---

## Next Step

**Execute the migration**:
```powershell
$scriptPath = "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"
sqlcmd -S 172.16.50.42 -d MMGTDB2A -U sa -P [password] -i $scriptPath
```

Expected output:
- identity_document already exists (from prior run)
- PMICompanies: 24 migrated
- Users: 0-N migrated (depending on existing duplicates)
- Applications: [N] migrated (with Priority=0)
- ApplicationDocuments: [14000+] migrated
- ApplicationReviewHistory: [N] migrated (with Status='Completed')
- Section 5 validation shows complete record counts

---

## Troubleshooting

If errors still occur:
1. **"Cannot insert NULL into Priority"**: Check that line 227 includes Priority and line 231 has `0 as Priority`
2. **"Already exists #UniqueDocTypes"**: Verify lines 245-250 have DROP IF EXISTS checks
3. **"Invalid object #DocTypeMapping"**: Verify temp table cleanup and error handling in lines 245-298
4. **"FK conflict on DocumentTypes"**: Verify DocumentTypes is in NOCHECK (line 150) and CHECK (line 366) lists
5. **Users delete errors**: Verify Users delete is REMOVED (no DELETE statement, only PRINT skip message)

---

## Files Modified

- `Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql` - All 5 fixes applied
- `Analysis/ADDITIONAL_SCHEMA_ISSUES_FOUND.md` - Documentation of issues and fixes

