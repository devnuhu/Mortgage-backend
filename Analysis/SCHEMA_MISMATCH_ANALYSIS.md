# Schema Mismatch Analysis from Migration Errors

## Migration Execution Results

### ✅ Successful Operations
- identity_document DocumentType created (GUID: 4EECC413-3B37-452B-88B8-5C5A9A108E29)
- PMICompanies: 24 rows migrated
- Users: 107 rows migrated (note: 110 source vs 107 migrated = 3 duplicates skipped, which is expected)

### ❌ Column Mismatch Errors Discovered

#### Error 1: Applications Table (Lines 225, 229)
```
Msg 207: Invalid column name 'DecisionBy'
Msg 207: Invalid column name 'DecisionAt'
```

**Analysis:**
- Migration script attempted to SELECT DecisionBy and DecisionAt from MMGTDBCOPY.dbo.Applications
- These columns do NOT exist in the source database
- The target schema (MMGTDB2A.dbo.Applications) expects these columns
- Decision: These columns likely were meant to come from ApplicationReviewHistory or similar
- Fix: Remove DecisionBy and DecisionAt from Applications INSERT, or find where they belong

#### Error 2: ApplicationDocuments Table (Line 330)
```
ERROR ApplicationDocuments: Cannot insert the value NULL into column 'FileType'
```

**Analysis:**
- Target table MMGTDB2A.dbo.ApplicationDocuments has a FileType column that is NOT NULL
- Source MMGTDBCOPY.dbo.ApplicationDocuments does NOT have FileType column
- 0 rows migrated (insert failed completely)
- Fix: Either (a) add a default FileType value in migration, (b) drop FileType column from target, or (c) populate FileType based on logic

#### Error 3: ApplicationReviewHistory Table (Lines 327, 330)
```
Msg 207: Invalid column name 'ReviewNotes'
Msg 207: Invalid column name 'ApprovedAt'
```

**Analysis:**
- Migration script attempted to SELECT ReviewNotes and ApprovedAt from MMGTDBCOPY
- These columns do NOT exist in source database
- Target schema expects these columns
- Fix: Determine if these are new columns in target design (not from source), or if they're named differently

---

## Schema Discrepancies Summary

### Applications Table
| Column | Source | Target | Status | Action |
|--------|--------|--------|--------|--------|
| Id | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| ApplicationNumber | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| UserId | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| PMICompanyId | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| LoanAmount | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| Status | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| CreatedAt | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| UpdatedAt | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| **DecisionBy** | ✗ NOT IN SOURCE | ✓ In target | ✗ MISMATCH | **REMOVE FROM INSERT** or **DEFAULT TO NULL** |
| **DecisionAt** | ✗ NOT IN SOURCE | ✓ In target | ✗ MISMATCH | **REMOVE FROM INSERT** or **DEFAULT TO NULL** |

**Recommended Fix:**
- Remove DecisionBy and DecisionAt from Applications INSERT in migration script
- These will remain NULL in target (or default to whatever is specified)

---

### ApplicationDocuments Table
| Column | Source | Target | Status | Action |
|--------|--------|--------|--------|--------|
| Id | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| ApplicationId | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| FileName | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| FilePath | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| FileContent | ✓ Exists | ✓ Exists | ✓ MATCH | Use NULL as-is |
| FileSize | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| DocumentTypeId | ✓ Exists | ✓ Exists | ✓ MATCH | Use mapped as-is |
| LegacyDocumentType | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| IsLegacy | ? | ✓ Exists | ? | Set to 1 for all migrated |
| CreatedAt | ✓ Exists | ✓ Exists | ✓ MATCH | Use GETDATE() or source |
| **FileType** | ✗ NOT IN SOURCE | ✓ NOT NULL IN TARGET | ✗ MISMATCH | **ADD DEFAULT VALUE** - maybe 'Unknown' or extract from FileName? |

**Recommended Fix:**
- Add FileType to INSERT with either:
  - Option A: DEFAULT VALUE (e.g., 'Unknown', 'Legacy')
  - Option B: Extract from FileName extension
  - Option C: Check if source actually has FileType column with different name

---

### ApplicationReviewHistory Table
| Column | Source | Target | Status | Action |
|--------|--------|--------|--------|--------|
| Id | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| ApplicationId | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| ReviewedById | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is (may be invalid FK) |
| CreatedAt | ✓ Exists | ✓ Exists | ✓ MATCH | Use as-is |
| **ReviewNotes** | ✗ NOT IN SOURCE | ✓ In target | ✗ MISMATCH | **REMOVE FROM INSERT** or **DEFAULT TO NULL/EMPTY** |
| **ApprovedAt** | ✗ NOT IN SOURCE | ✓ In target | ✗ MISMATCH | **REMOVE FROM INSERT** or **DEFAULT TO NULL** |

**Recommended Fix:**
- Remove ReviewNotes and ApprovedAt from ApplicationReviewHistory INSERT
- These columns will receive their defaults (NULL or empty string)

---

## Migration Script Corrections Needed

### Fix 1: Applications INSERT
**Current (BROKEN):**
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	Id, ApplicationNumber, UserId, PMICompanyId, LoanAmount, DecisionBy, DecisionAt, 
	Status, CreatedAt, UpdatedAt
)
SELECT 
	a.Id, a.ApplicationNumber, a.UserId, a.PMICompanyId, a.LoanAmount, a.DecisionBy, a.DecisionAt, 
	a.Status, a.CreatedAt, a.UpdatedAt
FROM MMGTDBCOPY.dbo.Applications a
```

**Corrected:**
```sql
INSERT INTO MMGTDB2A.dbo.Applications (
	Id, ApplicationNumber, UserId, PMICompanyId, LoanAmount, 
	Status, CreatedAt, UpdatedAt
)
SELECT 
	a.Id, a.ApplicationNumber, a.UserId, a.PMICompanyId, a.LoanAmount, 
	a.Status, a.CreatedAt, a.UpdatedAt
FROM MMGTDBCOPY.dbo.Applications a
```

---

### Fix 2: ApplicationDocuments INSERT

**Current (BROKEN):**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	Id, ApplicationId, FileName, FilePath, FileContent, FileSize, 
	DocumentTypeId, LegacyDocumentType, IsLegacy, CreatedAt
)
SELECT 
	ad.Id, ad.ApplicationId, ad.FileName, ad.FilePath, NULL, ad.FileSize,
	ISNULL(dtm.TargetTypeId, ...), ad.DocumentType, 1, GETDATE()
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
```

**Corrected (Option A - with default FileType):**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	Id, ApplicationId, FileName, FilePath, FileContent, FileSize, 
	DocumentTypeId, LegacyDocumentType, IsLegacy, CreatedAt, FileType
)
SELECT 
	ad.Id, ad.ApplicationId, ad.FileName, ad.FilePath, NULL, ad.FileSize,
	ISNULL(dtm.TargetTypeId, ...), ad.DocumentType, 1, GETDATE(), 
	'Legacy'  -- DEFAULT VALUE for migrated documents
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
```

**Corrected (Option B - extract from filename extension):**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	Id, ApplicationId, FileName, FilePath, FileContent, FileSize, 
	DocumentTypeId, LegacyDocumentType, IsLegacy, CreatedAt, FileType
)
SELECT 
	ad.Id, ad.ApplicationId, ad.FileName, ad.FilePath, NULL, ad.FileSize,
	ISNULL(dtm.TargetTypeId, ...), ad.DocumentType, 1, GETDATE(),
	UPPER(RIGHT(ad.FileName, CHARINDEX('.', REVERSE(ad.FileName)) - 1))  -- Extract extension
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
```

---

### Fix 3: ApplicationReviewHistory INSERT

**Current (BROKEN):**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
	Id, ApplicationId, ReviewedById, ReviewNotes, ApprovedAt, CreatedAt
)
SELECT 
	arh.Id, arh.ApplicationId, arh.ReviewedById, arh.ReviewNotes, arh.ApprovedAt, arh.CreatedAt
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh
```

**Corrected:**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
	Id, ApplicationId, ReviewedById, CreatedAt
)
SELECT 
	arh.Id, arh.ApplicationId, arh.ReviewedById, arh.CreatedAt
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh
```

---

## Decision Required

For **ApplicationDocuments FileType**, please choose:

### Option A: Use default value 'Legacy' for all migrated documents
- Pros: Simple, all legacy documents clearly marked
- Cons: Loses granularity of actual file types

### Option B: Extract from filename extension (PDF, DOC, JPG, etc.)
- Pros: Preserves file type information
- Cons: May fail if filenames don't have extensions

### Option C: Query source to see if FileType exists with different name
- Pros: Preserves actual file type data
- Cons: Requires additional investigation

---

## Next Steps

1. Confirm which FileType strategy to use
2. Update Phase2_COMPLETE_ALL_IN_ONE.sql with all three fixes
3. Re-run migration with corrected script
4. Validate all 5 tables now migrate successfully

