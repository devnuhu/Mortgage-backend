# Phase 2 Migration - Pre-Execution Checklist

## Status: Ready to Execute

### Prerequisites Check

#### 1. Database Connection ✓
- [ ] SQL Server Management Studio open
- [ ] Connected to `172.16.50.42`
- [ ] Can access `MMGTDB2A` database
- [ ] User has db_owner permissions

#### 2. Backup Confirmed ✓
- [ ] MMGTDB2A database backed up (user confirmed earlier)
- [ ] Backup location known and accessible
- [ ] Can restore if needed

#### 3. Source Database Available ✓
- [ ] MMGTDBCOPY database accessible
- [ ] Contains source data (110 Users, 13,789 Apps, etc.)
- [ ] Network connectivity to 172.16.50.42 stable

---

## Pre-Migration Setup: CREATE DOCUMENTTYPE

### Step 1: Create identity_document DocumentType

This is **critical** - all legacy documents need a valid DocumentTypeId reference.

**File to execute first:**
```
Analysis/Phase2_CreateDocumentType_IdentityDocument.sql
```

**What it does:**
- Checks if `identity_document` DocumentType exists
- If not, creates it with:
  - Code: `identity_document`
  - Name: `identity_document`
  - Description: "Legacy on-disk document type for migrated records"
  - MaxFileSizeKB: 300
  - IsMandatory: 0 (false)
  - IsActive: 1 (true)
- Verifies creation succeeded
- Shows all DocumentTypes available

**Execution:**
```
1. Open: Analysis/Phase2_CreateDocumentType_IdentityDocument.sql
2. Execute (Ctrl+A, F5)
3. Verify output shows: ✓ identity_document DocumentType ready
```

**Expected Output:**
```
=== ADDING LEGACY DOCUMENTTYPE ===

○ identity_document DocumentType does not exist - creating...

✓ Created identity_document DocumentType
  Id: {some GUID}
  Code: identity_document
  Name: identity_document
  DisplayOrder: 25 (or whatever's next)
  MaxFileSizeKB: 300
  IsActive: 1

=== ALL DOCUMENTTYPES IN MMGTDB2A ===

DisplayOrder | Code                | Name                | MaxFileSizeKB | IsActive | Id
─────────────────────────────────────────────────────────────────────────────
...
25           | identity_document   | identity_document   | 300           | 1        | {GUID}
```

---

## Main Migration: EXECUTE v3

### Step 2: Execute v3 Migration Script

**File to execute:**
```
Analysis/Phase2_MigrationExecution_v3_FINAL.sql
```

**Execution Steps:**
```sql
-- In SSMS:
1. Make sure you're in MMGTDB2A database
2. Open: Analysis/Phase2_MigrationExecution_v3_FINAL.sql
3. Select All (Ctrl+A)
4. Execute (F5)
5. DO NOT CLOSE SSMS - monitor the output
6. Wait 5-15 minutes for completion
```

**Monitoring:**
- Watch for `✓` symbols = success
- Watch for `✗ ERROR` = problems
- Document any error messages
- Keep SSMS visible throughout

**Timeline by Phase:**
| Phase | Est. Time | What Happens |
|-------|-----------|--------------|
| Pre-check | <1 min | Validates source/target ready |
| Clear target | <1 min | Deletes existing data |
| PMI migration | <1 min | 24 companies |
| Users migration | <1 min | 110 users |
| Apps migration | 1-2 min | 13,789 applications |
| Build mapping | <1 min | DocumentType mapping |
| **Docs migration** | **3-8 min** | **~100,000+ documents** |
| ReviewHistory | 1-2 min | 31,000+ review records |
| Finalize | <1 min | Re-enable constraints |
| Validation | <1 min | Count checks |
| **TOTAL** | **5-15 min** | Full migration |

---

## Validation: CHECK RESULTS

### Step 3: Review Output - Section 5

After v3 completes, scroll to the bottom and check **Section 5: POST-MIGRATION VALIDATION**

#### Check 5.1: Record Counts
```
Expected Output:
─────────────────────────────────────────────────
Users                       | 110    | 110    | ✓ MATCH
PMICompanies                | 24     | 24     | ✓ MATCH
Applications                | 13789  | 13789  | ✓ MATCH
ApplicationDocuments        | ~100K+ | ~100K+ | ✓ MATCH
ApplicationReviewHistory    | 31135  | 31135  | ✓ MATCH
─────────────────────────────────────────────────
```

**What to verify:**
- [ ] Users: 110 / 110 ✓
- [ ] PMICompanies: 24 / 24 ✓
- [ ] Applications: 13789 / 13789 ✓
- [ ] ApplicationDocuments: shows count > 0 ✓
- [ ] ReviewHistory: 31135 / 31135 ✓

#### Check 5.2: Data Quality
```
Expected Output:
IsLegacy flags: 1 (legacy) = ~100K+ rows ✓
FileContent: 100% NULL ✓
FilePath: Preserved ✓
```

**What to verify:**
- [ ] IsLegacy: 1 (not 0)
- [ ] FileContent shows NULL count
- [ ] No errors reported

#### Check 5.3: DocumentTypeId Integrity
```
Expected Output:
DocumentTypeId NOT NULL: ~100K+ rows ✓
NULL (ERROR): 0 rows ✓
```

**What to verify:**
- [ ] Non-NULL count > 0
- [ ] NULL count = 0 (critical!)

#### Check 5.4: Foreign Key Integrity
```
Expected Output:
Applications with valid UserId: 13789 ✓
Applications with INVALID UserId: 0 ✓
```

**What to verify:**
- [ ] Valid FK count = 13789
- [ ] Invalid FK count = 0

---

## If Migration Succeeds ✅

You should see output like:
```
=== MIGRATION COMPLETE ===
✓ PMICompanies migrated: 24 rows
✓ Users migrated: 110 rows
✓ Applications migrated: 13789 rows
✓ ApplicationDocuments migrated: 101234 rows (example)
✓ ApplicationReviewHistory migrated: 31135 rows
```

**Next Steps:**
1. Run: `Analysis/Phase2_PostMigration_Validation.md` (full validation)
2. Run: `Analysis/Phase2_ApplicationConnectivity_Verification.md` (API tests)
3. Test file endpoints manually
4. Sign off on Phase 2 completion

---

## If Migration Has Issues ❌

### Problem: ApplicationDocuments still 0
**Diagnosis:**
1. Check DocumentType mapping output in v3 - did it show errors?
2. Verify identity_document was created:
   ```sql
   SELECT * FROM MMGTDB2A.dbo.DocumentTypes 
   WHERE LOWER(Code) = 'identity_document'
   ```
3. Check source documents exist:
   ```sql
   SELECT COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments
   ```

**Solution:**
- If DocumentType mapping shows unmapped types, run:
  `Analysis/Phase2_DocumentType_Mapping_Analysis.sql`
- Then manually review source vs target types

### Problem: Users less than 110
**Diagnosis:**
1. Check error message in v3 output for Users section
2. Verify sample user can be queried:
   ```sql
   SELECT TOP 1 * FROM MMGTDBCOPY.dbo.Users
   ```
3. Check FK constraint:
   ```sql
   SELECT DISTINCT PMICompanyId FROM MMGTDBCOPY.dbo.Users
   WHERE PMICompanyId IS NOT NULL
   ```

**Solution:**
- May be constraint violation or data quality issue
- Run diagnostic to identify which user(s) failed

### Problem: Other issues
1. Copy full error message from output
2. Note the section where error occurred
3. Run: `Analysis/Phase2_Detailed_Diagnostics.sql` to investigate

---

## Execution Sequence Summary

```
┌─ STEP 1: Create DocumentType ──────────────────┐
│ File: Phase2_CreateDocumentType_IdentityDocument.sql
│ Time: <1 minute
│ Output: ✓ identity_document created
└────────────────────────────────────────────────┘
					  ↓
┌─ STEP 2: Execute v3 Migration ─────────────────┐
│ File: Phase2_MigrationExecution_v3_FINAL.sql
│ Time: 5-15 minutes (mostly ApplicationDocuments)
│ Output: Section 5 validation results
└────────────────────────────────────────────────┘
					  ↓
┌─ STEP 3: Review + Verify ──────────────────────┐
│ Check: Section 5.1 counts match
│ Check: Section 5.3 NO NULL DocumentTypeId
│ Check: Section 5.4 ALL FKs valid
│ Time: <1 minute
└────────────────────────────────────────────────┘
					  ↓
		IF ALL ✓ THEN SUCCESS
```

---

## Files Ready to Execute

| File | Purpose | Time |
|------|---------|------|
| `Phase2_CreateDocumentType_IdentityDocument.sql` | Create identity_document type | <1 min |
| `Phase2_MigrationExecution_v3_FINAL.sql` | Main migration | 5-15 min |
| `Phase2_PostMigration_Validation.md` | Full validation (after success) | 5-10 min |
| `Phase2_ApplicationConnectivity_Verification.md` | API tests (after validation) | 5-10 min |

---

## Ready to Proceed?

✅ All prerequisites met
✅ All files prepared
✅ DocumentType creation script ready
✅ v3 migration script ready
✅ Validation framework ready

**Next action:**
1. Execute `Analysis/Phase2_CreateDocumentType_IdentityDocument.sql` first
2. Then execute `Analysis/Phase2_MigrationExecution_v3_FINAL.sql`
3. Report the results from Section 5 validation

Let me know when you're ready to proceed or if you have any questions!
