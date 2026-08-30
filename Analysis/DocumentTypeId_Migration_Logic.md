# DocumentType Migration Logic - Detailed Explanation

## Question: Will ApplicationDocuments.DocumentTypeId populate with identity_document reference?

**Answer: YES! Here's exactly how it works:**

---

## The Complete Flow

### Step 1: identity_document DocumentType Created
```sql
Before migration:
MMGTDB2A.dbo.DocumentTypes table

INSERT INTO DocumentTypes (
	Id,           ← Auto-generated GUID (example: A1B2C3D4...)
	Code,         ← 'identity_document'
	Name,         ← 'identity_document'
	...
)
```

**Result:** 
- New GUID record in DocumentTypes table
- Example ID: `A1B2C3D4-E5F6-47G8-H9I0-J1K2L3M4N5O6`

---

### Step 2: Migration Builds DocumentType Mapping (Lines 242-277)

**Source:** MMGTDBCOPY.dbo.ApplicationDocuments
- Column: `DocumentType` (string)
- Example values: "identity_document", "other_type", etc.

**Target:** MMGTDB2A.dbo.DocumentTypes
- Column: `Id` (GUID)
- Column: `Code` (string)
- Column: `Name` (string)

**Mapping Logic:**
```sql
SELECT 
	uddt.SourceType,              -- e.g., "identity_document"
	dt.Id as TargetTypeId,        -- e.g., A1B2C3D4...
	dt.Code,                      -- e.g., "identity_document"
	dt.Name,                      -- e.g., "identity_document"
	COUNT(ad.Id) as DocCount      -- e.g., 98,765 documents
INTO #DocTypeMapping
FROM #UniqueDocTypes uddt
LEFT JOIN MMGTDBCOPY.dbo.ApplicationDocuments ad 
	ON ad.DocumentType = uddt.SourceType
LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON 
	(LOWER(uddt.SourceType) = LOWER(dt.Code)      -- Match by Code
	 OR LOWER(uddt.SourceType) = LOWER(dt.Name)   -- Match by Name
	 OR LOWER(uddt.SourceType) LIKE '%...%')      -- Partial match
```

**Result of Mapping:**
```
#DocTypeMapping temp table:

SourceType          | TargetTypeId              | Code              | DocCount
─────────────────────────────────────────────────────────────────────────
identity_document   | A1B2C3D4-E5F6-47G8-...   | identity_document | 98,765
other_type          | X1Y2Z3A4-B5C6-47D8-...   | other_document    | 1,235
unmapped_type       | NULL                      | NULL              | 100
```

---

### Step 3: Handle Unmapped Types (Lines 261-278)

If any source DocumentTypes don't match:
```sql
IF unmapped source types exist:
	SET @DefaultTypeId = (
		SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes 
		WHERE LOWER(Code) = 'identity_document'  ← Uses YOUR identity_document!
	)

	UPDATE #DocTypeMapping 
	SET TargetTypeId = @DefaultTypeId
	WHERE TargetTypeId IS NULL
```

**Result:** All unmapped types get assigned `identity_document` GUID

```
#DocTypeMapping AFTER update:

SourceType          | TargetTypeId              | Code              | DocCount
─────────────────────────────────────────────────────────────────────────
identity_document   | A1B2C3D4-E5F6-47G8-...   | identity_document | 98,765
other_type          | X1Y2Z3A4-B5C6-47D8-...   | other_document    | 1,235
unmapped_type       | A1B2C3D4-E5F6-47G8-...   | identity_document | 100 (fallback)
```

---

### Step 4: Insert ApplicationDocuments with Mapped DocumentTypeId (Lines 296-317)

**The Magic Happens Here:**
```sql
INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	Id, 
	FileName, 
	FilePath, 
	FileContent, 
	FileType, 
	DocumentTypeId,        ← THIS COLUMN!
	LegacyDocumentType, 
	FileSize, 
	Version, 
	IsLatestVersion, 
	...
)
SELECT 
	ad.Id,                                    -- Source document ID
	ad.FileName,                              -- Source file name
	ad.FilePath,                              -- Source file path
	NULL as FileContent,                      -- NULL for legacy
	ad.FileType,                              -- Source file type
	ISNULL(dtm.TargetTypeId,                 -- Use mapping...
		   (SELECT TOP 1 Id FROM 
			MMGTDB2A.dbo.DocumentTypes 
			ORDER BY Id)) as DocumentTypeId,  -- Or fallback default
	ad.DocumentType as LegacyDocumentType,   -- Store original string
	ad.FileSize,
	1 as Version,
	1 as IsLatestVersion,
	...
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
LEFT JOIN #DocTypeMapping dtm 
	ON ad.DocumentType = dtm.SourceType      -- Match on source type
```

**Result:**
```
MMGTDB2A.dbo.ApplicationDocuments (after insert):

Id              | FileName    | DocumentTypeId            | LegacyDocumentType | IsLegacy
────────────────────────────────────────────────────────────────────────────────────
ABC123...       | passport.pdf| A1B2C3D4-E5F6-...        | identity_document | 1
DEF456...       | license.jpg | A1B2C3D4-E5F6-...        | identity_document | 1
GHI789...       | birth.pdf   | X1Y2Z3A4-B5C6-...        | other_type        | 1
JKL012...       | (unmapped)  | A1B2C3D4-E5F6-...        | unmapped_type     | 1
```

✅ **DocumentTypeId is NOW POPULATED with GUID references!**

---

## Visual Flow Diagram

```
SOURCE (MMGTDBCOPY)
├─ ApplicationDocuments
│  ├─ DocumentType: "identity_document" (string)
│  ├─ DocumentType: "other_type" (string)
│  ├─ DocumentType: "unmapped_type" (string)
│  └─ ~100K documents
│
		↓ MAPPING PHASE

#DocTypeMapping (temp table)
├─ "identity_document"   → A1B2C3D4... (identity_document GUID)
├─ "other_type"          → X1Y2Z3A4... (other_document GUID)  
├─ "unmapped_type"       → A1B2C3D4... (identity_document GUID - fallback)
│
		↓ INSERT PHASE

TARGET (MMGTDB2A)
└─ ApplicationDocuments
   ├─ DocumentTypeId: A1B2C3D4... (GUID) ✓
   ├─ DocumentTypeId: A1B2C3D4... (GUID) ✓
   ├─ DocumentTypeId: X1Y2Z3A4... (GUID) ✓
   ├─ DocumentTypeId: A1B2C3D4... (GUID) ✓
   └─ All have VALID GUID references
```

---

## What Gets Populated in Each Column

### ApplicationDocuments After Migration:

| Column | Source | Value | Example |
|--------|--------|-------|---------|
| **Id** | MMGTDBCOPY | Source document GUID | `ABC123...` |
| **FileName** | MMGTDBCOPY | Source file name | `passport.pdf` |
| **FilePath** | MMGTDBCOPY | Preserved disk path | `C:\files\...` |
| **FileContent** | Migration | NULL (legacy) | `NULL` |
| **FileType** | MMGTDBCOPY | Source type | `application/pdf` |
| **DocumentTypeId** | **Mapping** | **Mapped GUID** | **A1B2C3D4-...** |
| **LegacyDocumentType** | MMGTDBCOPY | Original source string | `identity_document` |
| **FileSize** | MMGTDBCOPY | Source size | `5540958` |
| **Version** | Migration | Always 1 | `1` |
| **IsLatestVersion** | Migration | Always 1 | `1` |
| **ReviewStatus** | Migration | Default | `pending` |
| **IsLegacy** | Migration | Always 1 | `1` ← Protected flag |
| **CreatedAt** | MMGTDBCOPY | Source date | `2026-06-22 17:28:20...` |
| **UploadedBy** | Migration | NULL (no user) | `NULL` |
| **ApplicationId** | MMGTDBCOPY | Source app | `7D78CFF0-...` |

---

## Three Possible Scenarios

### Scenario 1: Source Type Matches identity_document Code ✅
```
Source: "identity_document"
Mapping logic: LOWER("identity_document") = LOWER(dt.Code)
Result: TargetTypeId = A1B2C3D4... (identity_document GUID)
PopulatedDocumentTypeId: A1B2C3D4... ✓
```

### Scenario 2: Source Type Matches Other Target Type ✅
```
Source: "other_type"
Mapping logic: LOWER("other_type") = LOWER(dt.Code)
Result: TargetTypeId = X1Y2Z3A4... (other_document GUID)
Populated DocumentTypeId: X1Y2Z3A4... ✓
```

### Scenario 3: Source Type Doesn't Match Any ⚠️ → Fallback
```
Source: "unmapped_type"
Mapping logic: No match found (TargetTypeId = NULL)
Fallback: Assign to identity_document GUID
Result: TargetTypeId = A1B2C3D4... (identity_document GUID)
Populated DocumentTypeId: A1B2C3D4... ✓
```

---

## The Mapping Script Output Will Show

When the migration runs, you'll see:
```
3.4: Building DocumentType mapping...

Unique DocumentTypes in source:
SourceType
─────────────────────────────
identity_document
other_type
unmapped_type

DocumentType mapping results:
SourceType          | TargetTypeId              | Code              | DocCount
─────────────────────────────────────────────────────────────────────────
identity_document   | A1B2C3D4-E5F6-47G8-...   | identity_document | 98765
other_type          | X1Y2Z3A4-B5C6-47D8-...   | other_type        | 1235
unmapped_type       | A1B2C3D4-E5F6-47G8-...   | identity_document | 100

⚠ Warning: 1 DocumentTypes could not be mapped
Unmapped types:
unmapped_type

Assigning unmapped to default DocumentType: A1B2C3D4-E5F6-47G8-...
✓ DocumentType mapping ready
```

---

## Post-Migration Verification

After migration, you can verify DocumentTypeId is populated:

```sql
-- Check ApplicationDocuments DocumentTypeId
SELECT 
	COUNT(*) as Total,
	COUNT(CASE WHEN DocumentTypeId IS NOT NULL THEN 1 END) as WithTypeId,
	COUNT(CASE WHEN DocumentTypeId IS NULL THEN 1 END) as WithoutTypeId
FROM MMGTDB2A.dbo.ApplicationDocuments

Result:
Total    | WithTypeId | WithoutTypeId
─────────────────────────────────────
100000   | 100000     | 0  ✓ ALL POPULATED!
```

```sql
-- See DocumentTypeId distribution
SELECT 
	dt.Code,
	dt.Name,
	COUNT(ad.Id) as DocumentCount
FROM MMGTDB2A.dbo.ApplicationDocuments ad
LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON dt.Id = ad.DocumentTypeId
GROUP BY dt.Code, dt.Name
ORDER BY DocumentCount DESC

Result:
Code                | Name                | DocumentCount
────────────────────────────────────────────────────────
identity_document   | identity_document   | 98765 (all mapped + unmapped)
other_type          | other_type          | 1235
```

---

## Summary: YES, DocumentTypeId Gets Populated!

✅ **Before Migration:**
- ApplicationDocuments.DocumentType = string ("identity_document", etc.)
- ApplicationDocuments.DocumentTypeId = doesn't exist yet

✅ **After Migration:**
- ApplicationDocuments.DocumentType = still string (source value)
- ApplicationDocuments.DocumentTypeId = **POPULATED with GUID from DocumentTypes table**
- All documents have valid, non-null references

✅ **Mapping Works Like This:**
```
Source "identity_document" (string)
		↓
Mapping: Find matching DocumentTypes.Code or Name
		↓
Result: DocumentTypeId = A1B2C3D4-... (GUID)
		↓
Inserted into: ApplicationDocuments.DocumentTypeId
```

✅ **If Source Type Not Found:**
- Falls back to `identity_document` GUID
- STILL gets populated with valid reference
- NO NULL values result

---

## Key Points

1. **Column Mapping:**
   - Source: `DocumentType` (string)
   - Target: `DocumentTypeId` (GUID) + `LegacyDocumentType` (string)

2. **Population Guarantee:**
   - ✓ 100% of ApplicationDocuments get DocumentTypeId populated
   - ✓ 0 documents with NULL DocumentTypeId
   - ✓ All references valid

3. **Data Retention:**
   - ✓ Original source type string saved in `LegacyDocumentType`
   - ✓ GUID reference in `DocumentTypeId`
   - ✓ Both available for reference

4. **Three-Level Matching:**
   - Level 1: Exact Code match
   - Level 2: Exact Name match  
   - Level 3: Partial match or fallback to `identity_document`

5. **Result:**
   - ✅ All ~100K documents migrated
   - ✅ All have valid DocumentTypeId
   - ✅ All referenced types exist in DocumentTypes table
   - ✅ Foreign key integrity maintained
