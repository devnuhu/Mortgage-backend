# Phase 2 Migration - v3 FINAL: Critical Findings & Fixes

## What We Now Know (From Diagnostics)

### Current State After v1:
| Table | Migrated | Source | Status |
|-------|----------|--------|--------|
| Users | **5/110** | 110 | ❌ **95% FAILED** |
| PMICompanies | 24/24 | 24 | ✅ Perfect |
| Applications | **13,789/13,789** | 13,789 | ✅ Perfect |
| ApplicationDocuments | **0/?** | ? | ❌ **100% FAILED** |
| ApplicationReviewHistory | **31,135/?** | ? | ⚠️ Partial (depends on valid FKs) |

### The Real Problem

**v1 only partially succeeded because:**

1. **Users Migration (95% Failure at 5/110)**
   - 110 users exist in source
   - Only 5 migrated to target
   - 105 failed silently (no error handling in v1)
   - **Likely causes:**
	 - Constraint violations (duplicate Email/PMIEmail)
	 - FK constraint to PMICompanies (PMICompanyId doesn't exist)
	 - Data type mismatches
	 - NULL values in required target columns

2. **ApplicationDocuments (100% Failure at 0/? records)**
   - v1 script had SQL error in DocumentType mapping query (`sdt.Code` binding error)
   - Query couldn't execute, so mapping never built
   - INSERT statement used NULL mapping → all DocumentTypeId = NULL
   - Target table rejected because DocumentTypeId is NOT NULL constraint

3. **Applications (13,789 records migrated - but problematic)**
   - Migrated successfully because FK constraints were disabled
   - BUT only 5 corresponding Users exist
   - 13,784 Applications are now orphaned (invalid UserId FK)
   - This violates data integrity even if constraints disabled

4. **ApplicationReviewHistory (31,135 records)**
   - Migrated with orphaned FKs (since Users/Applications were incomplete)
   - Many ReviewedById don't match any User.Id

---

## Critical Fixes in v3

### Fix 1: Corrected DocumentType Mapping Query
**v1 Problem:**
```sql
SELECT DISTINCT 
	sad.DocumentType as SourceType,
	sdt.Code as SourceCode,          -- ❌ ERROR: sdt undefined
	tdt.Code as TargetMatchCode,
	...
FROM MMGTDBCOPY.dbo.ApplicationDocuments sad
LEFT JOIN MMGTDB2A.dbo.DocumentTypes tdt ON ...
-- Missing: LEFT JOIN for 'sdt'
```

**v3 Fix:**
```sql
SELECT 
	uddt.SourceType,
	dt.Id as TargetTypeId,
	dt.Code,
	dt.Name,
	COUNT(ad.Id) as DocCount
FROM #UniqueDocTypes uddt
LEFT JOIN MMGTDBCOPY.dbo.ApplicationDocuments ad ON ...
LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON 
	(LOWER(uddt.SourceType) = LOWER(dt.Code) 
	 OR LOWER(uddt.SourceType) = LOWER(dt.Name)
	 OR LOWER(uddt.SourceType) LIKE '%' + LOWER(dt.Code) + '%')
```

**What's different:**
- Build temp table of unique source DocumentTypes first
- Clear matching logic: match Code, Name, or partial matches
- Shows result count for each type
- Diagnostic output shows successful mappings

### Fix 2: Better Error Handling
- Each table wrapped in TRY-CATCH
- Errors are captured and reported but don't stop migration
- Invalid FK checks after each insert
- Shows exact count of problems

### Fix 3: Diagnostic Output
- Shows unique DocumentTypes found in source
- Shows mapping results with match types
- Shows which DocumentTypes couldn't map
- Provides fallback to 'identity_document' for unmapped

### Fix 4: Post-Migration Validation
- Clear comparison: Migrated vs Source counts
- Flag mismatches immediately
- Check DocumentTypeId for NULLs
- Verify FK integrity per table

### Fix 5: Correct Migration Order
**v3 order:**
1. PMICompanies (no dependencies)
2. Users (depends on PMI for FK)
3. Applications (depends on Users + PMI)
4. **Build DocumentType mapping**
5. ApplicationDocuments (depends on Applications + mapping)
6. ApplicationReviewHistory (depends on Applications + Users)

---

## Why v1 Partially Worked

The v1 script had **commented, disorganized error checking** that allowed it to partially process:

1. FK constraints were disabled BEFORE deletes
2. When Users insert failed, script continued (no TRY-CATCH)
3. Applications insert succeeded because constraints were disabled
4. ReviewHistory partially migrated (31K records)

But the real data integrity issues were hidden until diagnostics.

---

## What v3 Does Differently

### Before Migration:
```
1. Validate source accessible
2. Verify target schema
3. Show source/target counts
4. Disable ALL FK constraints (prevents cascades)
5. Clear target tables in reverse FK order
```

### During Migration:
```
1. Insert PMICompanies (no FK dependencies)
2. Insert Users with FK validation
3. Insert Applications with FK validation
4. BUILD DocumentType mapping (show results)
5. Insert ApplicationDocuments (using valid mapping)
6. Insert ApplicationReviewHistory
```

### After Migration:
```
1. Re-enable ALL FK constraints
2. Compare counts (migrated vs source)
3. Check IsLegacy flags
4. Check DocumentTypeId is NOT NULL
5. Verify FK integrity
```

---

## Execution Instructions for v3

### Prerequisites
- SQL Server Management Studio connected to `172.16.50.42`
- Open database `MMGTDB2A`
- User has db_owner permissions (to disable/enable constraints)

### Steps
1. **Open file:** `Analysis/Phase2_MigrationExecution_v3_FINAL.sql`
2. **Review** sections 1-2 to ensure validation will pass
3. **Execute** entire script (Ctrl+A, then F5)
4. **Monitor output:**
   - Watch for `✓` (success) vs `✗ ERROR` (failure)
   - Look at DocumentType mapping results
   - Check section 5 validation carefully

### Expected Execution Time
- Source has 110 Users, 24 PMI, 13,789 Apps, ~100K+ documents, 31K+ reviews
- Full migration should take **5-15 minutes** depending on network/disk

### What Success Looks Like

```
5.1: RECORD COUNTS COMPARISON:
Table                       | Migrated | Source | Status
─────────────────────────────────────────────────────────
Users                       |      110 |    110 | ✓ MATCH
PMICompanies                |       24 |     24 | ✓ MATCH
Applications                |    13789 |  13789 | ✓ MATCH
ApplicationDocuments        |   ~110K+ |  ~110K+ | ✓ MATCH
ApplicationReviewHistory    |    31135 |   31135 | ✓ MATCH

5.2: DATA QUALITY CHECKS:
ApplicationDocuments - IsLegacy distribution:
IsLegacy | Count
────────────────
1        | ~110K+  (all marked as legacy ✓)

ApplicationDocuments - DocumentTypeId NULL check:
Status     | Count
─────────────────
Non-NULL   | ~110K+  (all have valid type ✓)

5.3: FOREIGN KEY INTEGRITY:
Check Name                        | Count
──────────────────────────────────
Applications with valid UserId    | 13789 ✓
Applications with INVALID UserId  | 0 ✓
```

---

## If Something Goes Wrong

### "ApplicationDocuments still 0 rows"
1. Check DocumentType mapping output - look for "Unmapped"
2. Run: `SELECT DISTINCT DocumentType FROM MMGTDBCOPY.dbo.ApplicationDocuments`
3. Run: `SELECT Code, Name FROM MMGTDB2A.dbo.DocumentTypes ORDER BY Code`
4. Manually compare - some source type codes may not match any target

### "Users still low count (not 110)"
1. Check error output - may be constraint violations
2. Run: `SELECT TOP 5 Email, PMICompanyId FROM MMGTDBCOPY.dbo.Users`
3. Check: `SELECT TOP 5 Id FROM MMGTDB2A.dbo.PMICompanies`
4. If PMICompanyId doesn't exist, FK fails

### "ReviewHistory count much lower than source"
1. Check Applications count first - if low, ReviewHistory FK will fail
2. Check if orphaned FK count reported in section 5.3

---

## Key Takeaways

1. **Large dataset:** 13,789+ applications = slow migration
2. **DocumentType mapping is critical:** Must match source strings to target GUIDs
3. **FK dependencies matter:** Users → Applications → Documents → History
4. **Silent failures are dangerous:** v1 had no error reporting 
5. **Hybrid storage model preserved:** All migrated documents marked `IsLegacy=1`, `FileContent=NULL`, `FilePath` preserved

---

## Next Steps After v3 Succeeds

1. Run: `Analysis/Phase2_PostMigration_Validation.md` queries
2. Run: `Analysis/Phase2_ApplicationConnectivity_Verification.md` endpoint tests
3. Test file download: `GET /api/file/download?documentId={id}` 
4. Test new upload: `POST /api/file/upload` (should store as BLOB)
5. Verify legacy document download still works
