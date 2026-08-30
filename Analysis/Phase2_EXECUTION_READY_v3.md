# Phase 2 Migration - Current Status & Action Plan

## Current Database State (After v1 Attempt)

### Migration Results from Diagnostics
```
Users:                    5 / 110   (4.5% success)  ❌ FAILED
PMICompanies:            24 / 24   (100% success)  ✅ OK
Applications:        13,789 / 13,789 (100% success) ✅ OK (but orphaned)
ApplicationDocuments:  0 / ~100K   (0% success)    ❌ FAILED
ReviewHistory:    31,135 / ~31K   (partial)       ⚠️ PROBLEMATIC
```

### Key Issues Identified

1. **Users Only 5/110 Migrated**
   - 105 user records failed to insert
   - No error diagnostics from v1 script
   - Likely causes: constraint violations or FK errors

2. **ApplicationDocuments 0 Records**
   - v1 script had SQL syntax error in mapping query
   - DocumentType mapping never built
   - Insert used NULL mapping → NULL DocumentTypeId → INSERT FAILED

3. **Data Integrity Problems**
   - 13,784 Applications tied to non-existent Users (orphaned)
   - ReviewHistory referencing invalid Users/Applications
   - Constraints disabled, so violations not detected

---

## Solution: v3 Migration Script

### What v3 Fixes

✅ **Corrected DocumentType Mapping**
- Fixed SQL syntax error in query
- Shows all source DocumentType values
- Maps to target Code/Name with 3-level fallback
- Reports unmapped types clearly
- Assigns unmapped to 'identity_document' default

✅ **Complete Error Handling**
- Each table wrapped in TRY-CATCH
- Errors reported but don't stop migration
- Invalid FK counts reported per table
- No silent failures

✅ **Correct Migration Order**
1. PMICompanies first (no dependencies)
2. Users (needs PMI FK)
3. Applications (needs Users + PMI)
4. Build DocumentType mapping
5. ApplicationDocuments (uses mapping)
6. ReviewHistory (needs Apps + Users)

✅ **Comprehensive Validation**
- Pre-migration checks (source accessible, schema ready)
- Per-table FK integrity validation
- Post-migration count comparison
- DocumentTypeId NULL checks
- IsLegacy flag verification

---

## How to Execute v3

### Files You Need
1. **`Analysis/Phase2_MigrationExecution_v3_FINAL.sql`** ← Main migration script
2. **`Analysis/Phase2_MigrationExecution_v3_README.md`** ← Explanation
3. **`Analysis/Phase2_DocumentType_Mapping_Analysis.sql`** ← Optional pre-check

### Step-by-Step Execution

**Step 1: Optional - Analyze DocumentType Mapping First**
```
1. Open SSMS → 172.16.50.42 → MMGTDB2A
2. Open Analysis/Phase2_DocumentType_Mapping_Analysis.sql
3. Execute (F5)
4. Review output to understand source DocumentType values
5. See if any source types might not map to target
```

**Step 2: Execute v3 Migration**
```
1. Keep SSMS connected to MMGTDB2A
2. Open Analysis/Phase2_MigrationExecution_v3_FINAL.sql
3. Recommend: Read through script first (understand flow)
4. Execute entire script (Ctrl+A → F5)
5. Do NOT close SSMS - monitor output as it runs
6. Script will take 5-15 minutes for 13,789+ applications
```

**Step 3: Monitor Output**
- Look for `✓` symbols (success) and `✗ ERROR` (problems)
- Review DocumentType mapping section (shows what matched)
- Check warnings about orphaned FKs
- Review final validation section (section 5)

**Step 4: Verify Results**
- Check section 5.1 counts match expected
- Confirm section 5.2 shows IsLegacy=1 for all documents
- Verify section 5.3 shows NO NULL DocumentTypeId
- Validate section 5.4 shows valid FK relationships

### Expected Output (Success Case)

```
5.1: RECORD COUNTS COMPARISON:
Users                       | 110 | 110 | ✓ MATCH
PMICompanies                | 24  | 24  | ✓ MATCH
Applications                | 13789 | 13789 | ✓ MATCH
ApplicationDocuments        | ~100K+ | ~100K+ | ✓ MATCH
ApplicationReviewHistory    | 31135+ | 31135+ | ✓ MATCH

5.2: DATA QUALITY CHECKS:
ApplicationDocuments - IsLegacy distribution:
1 (legacy)  | ~100K+  ✓

ApplicationDocuments - DocumentTypeId NULL check:
Non-NULL    | ~100K+  ✓
NULL (ERROR)| 0       ✓
```

---

## What to Do If Issues Occur

### Scenario 1: "ApplicationDocuments still 0 rows"

**Diagnosis:**
1. Check v3 output for DocumentType mapping
   - Look for "Unmapped types detected" or mapping errors
2. Run: `Analysis/Phase2_DocumentType_Mapping_Analysis.sql`
   - This shows exact source types vs target types available

**Solution:**
- Source DocumentType values may be different than expected
- Mapping fallback assigns to 'identity_document'
- Check: Do those ~100K documents exist in MMGTDBCOPY?
  - `SELECT COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments`

### Scenario 2: "Users still low (not 110)"

**Diagnosis:**
1. Check v3 error output under section 3.2
2. Look for error message after Users insert
3. Check count of orphaned FK issues reported

**Investigation:**
```sql
-- In SSMS, run these to understand the issue:
SELECT TOP 5 Email, PMICompanyId FROM MMGTDBCOPY.dbo.Users
SELECT TOP 5 Id FROM MMGTDB2A.dbo.PMICompanies
-- If PMICompanyId doesn't exist in PMI table, FK will fail
```

### Scenario 3: "ReviewHistory lower than expected"

**Likely cause:** Applications incomplete so ReviewHistory can't link

**Check:**
- What was Applications count in section 5.1?
- If Applications = 13,789, ReviewHistory issues are FK validation
- If Applications < 13,789, then ReviewHistory count will be low too

---

## After Successful v3 Migration

1. **Run Post-Migration Validation**
   - File: `Analysis/Phase2_PostMigration_Validation.md`
   - This validates data quality and completeness

2. **Run Application Connectivity Tests**
   - File: `Analysis/Phase2_ApplicationConnectivity_Verification.md`
   - Tests API endpoints for file upload/download

3. **Test Legacy File Download**
   - Use Postman: `GET http://localhost:5000/api/file/download?fileId={id}`
   - Should return BLOB from disk (FilePath)

4. **Test New File Upload**
   - Use Postman: `POST http://localhost:5000/api/file/upload`
   - Should store as BLOB (FileContent)

5. **Verify Constraints** on ApplicationDocuments table:
   - `DELETE` should fail if IsLegacy=1
   - `PUT (replace)` should fail if IsLegacy=1
   - `POST (new)` should succeed and set FileContent (not FilePath)

---

## Important Notes

### Data Size: Very Large Dataset
- 110 Users
- 24 PMI Companies  
- **13,789 Applications** ← Large
- **~100K+ ApplicationDocuments** ← Very Large
- **31K+ ReviewHistory** ← Large
- **Total migration time: 5-15 minutes** (normal for this size)

### Hybrid Storage Model
All migrated documents will have:
- `IsLegacy = 1` (protected flag)
- `FileContent = NULL` (no BLOB)
- `FilePath = [path to original file]` (on disk)
- `LegacyDocumentType = [original source type string]`
- `DocumentTypeId = [mapped GUID]` (required for integrity)

### File Download Flow After Migration
User requests: `GET /api/file/download/{documentId}`
1. FileService checks `IsLegacy` flag
2. If legacy, reads from `FilePath` on disk
3. If non-legacy, reads from `FileContent` BLOB
4. Returns binary content with proper headers

### New Upload Flow After Migration
User uploads: `POST /api/file/upload`
New documents will have:
- `IsLegacy = 0` (new flag)
- `FileContent = [binary BLOB data]` (stored in DB)
- `FilePath = NULL` (no disk path needed)
- `DocumentTypeId = [matched GUID]`

---

## Ready to Proceed?

✅ **All files prepared:**
- `Analysis/Phase2_MigrationExecution_v3_FINAL.sql` (main migration)
- `Analysis/Phase2_MigrationExecution_v3_README.md` (detailed docs)
- `Analysis/Phase2_DocumentType_Mapping_Analysis.sql` (pre-check)
- `Analysis/Phase2_Detailed_Diagnostics.sql` (current state)

**Next action:** Execute v3 migration script in SSMS and monitor output for success.

Would you like me to:
1. ✅ Prepare to execute v3 (you'll run in SSMS) 
2. 🔍 First run DocumentType mapping analysis to see what will match
3. ❓ Something else?
