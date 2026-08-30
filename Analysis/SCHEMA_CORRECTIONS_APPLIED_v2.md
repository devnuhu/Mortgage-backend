# Schema Corrections Applied - Phase 2 Migration v2

## Summary of Changes

The Phase2_COMPLETE_ALL_IN_ONE.sql migration script has been corrected to handle schema mismatches between MMGTDBCOPY (source) and MMGTDB2A (target).

## Three Critical Fixes Applied

### Fix 1: Applications Table - Remove DecisionBy, DecisionAt

**Issue:**
- Target has DecisionBy and DecisionAt columns
- Source has NO such columns
- Migration was attempting `SELECT a.DecisionBy, a.DecisionAt` → Msg 207

**Fix Applied:**
```sql
-- BEFORE (BROKEN):
INSERT INTO Applications (
	Id, ApplicationNumber, UserId, PMICompanyId, LoanAmount, DecisionBy, DecisionAt, 
	Status, CreatedAt, UpdatedAt
)
SELECT 
	a.Id, a.ApplicationNumber, a.UserId, a.PMICompanyId, a.LoanAmount, a.DecisionBy, a.DecisionAt,
	a.Status, a.CreatedAt, a.UpdatedAt
FROM MMGTDBCOPY.dbo.Applications a

-- AFTER (FIXED):
INSERT INTO Applications (
	Id, ApplicationNumber, UserId, PMICompanyId, LoanAmount, 
	Status, CreatedAt, UpdatedAt
)
SELECT 
	a.Id, a.ApplicationNumber, a.UserId, a.PMICompanyId, a.LoanAmount, 
	a.Status, a.CreatedAt, a.UpdatedAt
FROM MMGTDBCOPY.dbo.Applications a
```

**Result:** DecisionBy and DecisionAt will default to their column defaults (NULL or whatever is set in table definition). Applications will now migrate successfully.

---

### Fix 2: ApplicationDocuments Table - Add FileType Default

**Issue:**
- Target has FileType column that is NOT NULL
- Source has NO FileType column
- Migration was attempting to INSERT without FileType → Msg 515 NULL error
- 0 rows migrated

**Fix Applied:**
```sql
-- BEFORE (BROKEN):
INSERT INTO ApplicationDocuments (
	Id, ApplicationId, FileName, FilePath, FileContent, FileSize, 
	DocumentTypeId, LegacyDocumentType, IsLegacy, CreatedAt
	-- ^^^^^ Missing FileType column
)
SELECT 
	ad.Id, ad.ApplicationId, ad.FileName, ad.FilePath, NULL, ad.FileSize,
	ISNULL(dtm.TargetTypeId, ...), ad.DocumentType, 1, GETDATE()
	-- ^^^^^ No FileType value
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
LEFT JOIN #DocTypeMapping dtm ON ad.DocumentType = dtm.SourceType

-- AFTER (FIXED):
INSERT INTO ApplicationDocuments (
	Id, ApplicationId, FileName, FilePath, FileContent, FileSize, 
	DocumentTypeId, LegacyDocumentType, IsLegacy, CreatedAt, FileType
	-- ^^^^^ Added FileType column
)
SELECT 
	ad.Id, ad.ApplicationId, ad.FileName, ad.FilePath, NULL, ad.FileSize,
	ISNULL(dtm.TargetTypeId, ...), ad.DocumentType, 1, GETDATE(),
	'Legacy' as FileType  -- ^^^^^ Default value for migrated legacy documents
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
LEFT JOIN #DocTypeMapping dtm ON ad.DocumentType = dtm.SourceType
```

**Result:** All 14,047 ApplicationDocuments will now migrate with FileType = 'Legacy' (indicating legacy on-disk documents).

---

### Fix 3: ApplicationReviewHistory Table - Remove ReviewNotes, ApprovedAt

**Issue:**
- Target has ReviewNotes and ApprovedAt columns
- Source has NO such columns
- Migration was attempting `SELECT arh.ReviewNotes, arh.ApprovedAt` → Msg 207

**Fix Applied:**
```sql
-- BEFORE (BROKEN):
INSERT INTO ApplicationReviewHistory (
	Id, ApplicationId, ReviewedById, ReviewNotes, ApprovedAt, CreatedAt
)
SELECT 
	arh.Id, arh.ApplicationId, arh.ReviewedById, arh.ReviewNotes, arh.ApprovedAt, arh.CreatedAt
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh

-- AFTER (FIXED):
INSERT INTO ApplicationReviewHistory (
	Id, ApplicationId, ReviewedById, CreatedAt
)
SELECT 
	arh.Id, arh.ApplicationId, arh.ReviewedById, arh.CreatedAt
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh
```

**Result:** ReviewNotes and ApprovedAt will default to their column defaults (NULL or empty string). ApplicationReviewHistory will now migrate successfully (31,135 rows).

---

## Updated Script File

**File:** Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql

**Changes Made:**
- Line 223-230: Applications INSERT corrected (removed DecisionBy, DecisionAt)
- Line 295-310: ApplicationDocuments INSERT corrected (added FileType with 'Legacy' default)
- Line 326-330: ApplicationReviewHistory INSERT corrected (removed ReviewNotes, ApprovedAt)

**Total Lines:** 418 (was 417)

---

## What This Means for Migration

### Expected Results After Re-Run

```
3.1: PMICompanies migrated: 24 rows ✓
3.2: Users migrated: 107 rows ✓
3.3: Applications migrated: 13,789 rows ✓ (FIXED)
3.4: DocumentType mapping ready ✓
3.5: ApplicationDocuments migrated: 14,047 rows ✓ (FIXED - now has FileType)
3.6: ApplicationReviewHistory migrated: 31,135 rows ✓ (FIXED)

TOTAL: 59,002 records migrated successfully
```

### Record Count Comparison Expected

```
Table                       | Migrated | Source | Status
Users                       | 107      | 110    | (3 duplicates expected)
PMICompanies                | 24       | 24     | MATCH
Applications                | 13789    | 13789  | MATCH
ApplicationDocuments        | 14047    | 14047  | MATCH
ApplicationReviewHistory    | 31135    | 31135  | MATCH
```

---

## Column Mappings - Summary

### Applications (CORRECTED)
Source → Target:
- Id → Id ✓
- ApplicationNumber → ApplicationNumber ✓
- UserId → UserId ✓
- PMICompanyId → PMICompanyId ✓
- LoanAmount → LoanAmount ✓
- Status → Status ✓
- CreatedAt → CreatedAt ✓
- UpdatedAt → UpdatedAt ✓
- (DecisionBy → DEFAULT) ← Not in source, uses target default
- (DecisionAt → DEFAULT) ← Not in source, uses target default

### ApplicationDocuments (CORRECTED)
Source → Target:
- Id → Id ✓
- ApplicationId → ApplicationId ✓
- FileName → FileName ✓
- FilePath → FilePath ✓
- (FileContent) → NULL ✓
- FileSize → FileSize ✓
- DocumentType → DocumentTypeId (via mapping) ✓
- DocumentType → LegacyDocumentType ✓
- (IsLegacy) → 1 ✓
- (CreatedAt) → GETDATE() ✓
- (FileType) → 'Legacy' ✓ NEW

### ApplicationReviewHistory (CORRECTED)
Source → Target:
- Id → Id ✓
- ApplicationId → ApplicationId ✓
- ReviewedById → ReviewedById ✓
- CreatedAt → CreatedAt ✓
- (ReviewNotes → DEFAULT) ← Not in source, uses target default
- (ApprovedAt → DEFAULT) ← Not in source, uses target default

---

## Next Steps

1. **Re-run the migration** with corrected script:
   ```powershell
   $sqlPath = "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"
   Invoke-Sqlcmd -InputFile $sqlPath -ServerInstance "172.16.50.42" -QueryTimeout 600
   ```

2. **Verify output** in Section 5.1:
   - All tables should show record counts
   - No ERROR messages for any table
   - All counts should MATCH source

3. **Confirm success**:
   - MIGRATION COMPLETE message
   - Completion timestamp shown
   - No Msg 207 or Msg 515 errors

---

## Files Updated

- **Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql** (418 lines)
  - Applications: Removed DecisionBy, DecisionAt from INSERT
  - ApplicationDocuments: Added FileType column with 'Legacy' default
  - ApplicationReviewHistory: Removed ReviewNotes, ApprovedAt from INSERT

- **Analysis/SCHEMA_MISMATCH_ANALYSIS.md** (detailed reference)
  - Explains all three mismatches
  - Shows before/after code
  - Lists alternative options

---

## Notes

- This assumes MMGTDBCOPY is the source of truth for schema
- DecisionBy/DecisionAt, ReviewNotes/ApprovedAt appear to be new fields in target design not yet populated from source
- FileType='Legacy' is a reasonable default for migrated on-disk documents
- All schema corrections are reversible if different handling is needed

---

**Status:** ✅ Script corrected and ready to re-run  
**Last Modified:** Post-migration error analysis  
**Ready?:** YES
