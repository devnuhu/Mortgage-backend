# DocumentTypeId Migration - SQL Logic Explained

## Your Question with SQL Code

**Q: When migrating to ApplicationDocuments, does the DocumentTypeId column populate with identity_document as referenced from DocumentTypes?**

**A: YES! Here's the exact SQL logic:**

---

## The SQL Logic (Simplified)

### What Happens in the Script:

```sql
-- ============================================================================
-- PHASE 1: CREATE identity_document DOCUMENTTYPE
-- ============================================================================

-- This creates a new row in the DocumentTypes table:
INSERT INTO MMGTDB2A.dbo.DocumentTypes 
(Id, Code, Name, Description, MaxFileSizeKB, IsActive, ...)
VALUES 
(NEWID(),  -- Auto-generated GUID, e.g., A1B2C3D4-E5F6-47G8-H9I0-J1K2L3M4N5O6
 'identity_document',
 'identity_document',
 'Legacy on-disk document type for migrated records',
 300,
 1,
 ...)

-- Result: New DocumentTypes record with GUID:
-- Id: A1B2C3D4-E5F6-47G8-H9I0-J1K2L3M4N5O6
-- Code: identity_document
```

---

### What Happens During Migration:

```sql
-- ============================================================================
-- PHASE 2: BUILD MAPPING (Lines 242-256)
-- ============================================================================

-- Get unique DocumentTypes from source:
SELECT DISTINCT ad.DocumentType as SourceType
INTO #UniqueDocTypes
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad

-- Result temp table:
-- SourceType
-- ─────────────────────
-- identity_document
-- other_type
-- unmapped_type

-- ============================================================================
-- PHASE 3: MAP SOURCE TYPES TO TARGET GUIDS (Lines 242-256)
-- ============================================================================

-- Create mapping temp table:
SELECT 
	uddt.SourceType,                    -- e.g., "identity_document"
	dt.Id as TargetTypeId,              -- e.g., A1B2C3D4-... (GUID!)
	dt.Code,
	dt.Name,
	COUNT(ad.Id) as DocCount
INTO #DocTypeMapping
FROM #UniqueDocTypes uddt
LEFT JOIN MMGTDBCOPY.dbo.ApplicationDocuments ad 
	ON ad.DocumentType = uddt.SourceType
LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON 
	(LOWER(uddt.SourceType) = LOWER(dt.Code)       -- Case-insensitive match
	 OR LOWER(uddt.SourceType) = LOWER(dt.Name))
GROUP BY uddt.SourceType, dt.Id, dt.Code, dt.Name

-- Result mapping table:
-- ┌──────────────────────────┬──────────────────────────────────┐
-- │ SourceType               │ TargetTypeId (GUID from DocTypes)│
-- ├──────────────────────────┼──────────────────────────────────┤
-- │ identity_document        │ A1B2C3D4-E5F6-47G8-H9I0-...     │
-- │ other_type               │ X1Y2Z3A4-B5C6-47D8-E0F1-...     │
-- │ unmapped_type            │ NULL  (no match found)           │
-- └──────────────────────────┴──────────────────────────────────┘

-- ============================================================================
-- PHASE 4: HANDLE UNMAPPED TYPES (Lines 261-278)
-- ============================================================================

-- If any source types didn't match:
DECLARE @UnmappedCount INT = (
	SELECT COUNT(*) FROM #DocTypeMapping WHERE TargetTypeId IS NULL
)

IF @UnmappedCount > 0
BEGIN
	-- Find the identity_document GUID:
	DECLARE @DefaultTypeId UNIQUEIDENTIFIER = (
		SELECT TOP 1 Id 
		FROM MMGTDB2A.dbo.DocumentTypes 
		WHERE LOWER(Code) = 'identity_document'  -- ← Finds A1B2C3D4-...
	)

	-- Assign unmapped types to identity_document:
	UPDATE #DocTypeMapping 
	SET TargetTypeId = @DefaultTypeId        -- ← Set to A1B2C3D4-...
	WHERE TargetTypeId IS NULL
END

-- Result AFTER update:
-- ┌──────────────────────────┬──────────────────────────────────┐
-- │ SourceType               │ TargetTypeId (GUID)              │
-- ├──────────────────────────┼──────────────────────────────────┤
-- │ identity_document        │ A1B2C3D4-E5F6-47G8-H9I0-...     │
-- │ other_type               │ X1Y2Z3A4-B5C6-47D8-E0F1-...     │
-- │ unmapped_type            │ A1B2C3D4-E5F6-47G8-H9I0-... ←  │
-- └──────────────────────────┴──────────────────────────────────┘
--                                    ↑ Now has GUID (identity_document)!

-- ============================================================================
-- PHASE 5: INSERT WITH MAPPED DOCUMENTTYPEID (Lines 296-317)
-- ============================================================================

INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
	Id, 
	FileName, 
	FilePath, 
	FileContent, 
	FileType, 
	DocumentTypeId,          -- ← THIS IS THE KEY COLUMN!
	LegacyDocumentType, 
	FileSize, 
	...
)
SELECT 
	ad.Id,                   -- From source
	ad.FileName,             -- From source
	ad.FilePath,             -- From source (preserved)
	NULL as FileContent,     -- Legacy docs = NULL
	ad.FileType,             -- From source
	ISNULL(dtm.TargetTypeId, -- ← Use mapped GUID from #DocTypeMapping
		   (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes 
			ORDER BY Id)) as DocumentTypeId,  -- ← Or fallback to any DocType
	ad.DocumentType as LegacyDocumentType,    -- Store original string
	ad.FileSize,             -- From source
	...
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
LEFT JOIN #DocTypeMapping dtm                 -- The mapping temp table!
	ON ad.DocumentType = dtm.SourceType       -- Match on source type string
```

---

## The Exact Mapping Logic for DocumentTypeId

### For Each Document Being Inserted:

```
Step 1: Get source DocumentType
	Example: ad.DocumentType = "identity_document"

Step 2: Look up in #DocTypeMapping
	Lookup: WHERE #DocTypeMapping.SourceType = "identity_document"

Step 3: Get the mapped GUID
	Result: dtm.TargetTypeId = A1B2C3D4-E5F6-47G8-H9I0-...

Step 4: Insert that GUID into DocumentTypeId
	INSERT: ApplicationDocuments.DocumentTypeId = A1B2C3D4-...

✓ RESULT: DocumentTypeId is now populated with GUID!
```

---

## Three Examples

### Example 1: Source is "identity_document"
```
Source Document:
├─ Id: 1234567...
├─ DocumentType: "identity_document"  ← Source column (string)
└─ FileName: passport.pdf

Mapping Lookup:
├─ Find: #DocTypeMapping.SourceType = "identity_document"
└─ Get: dtm.TargetTypeId = A1B2C3D4-E5F6-47G8-...

Result in Target:
├─ Id: 1234567...
├─ DocumentTypeId: A1B2C3D4-E5F6-47G8-... ✓ POPULATED (identity_document GUID)
├─ LegacyDocumentType: "identity_document" (original string preserved)
└─ FileName: passport.pdf
```

### Example 2: Source is "other_type"
```
Source Document:
├─ Id: 2345678...
├─ DocumentType: "other_type"  ← Different source type
└─ FileName: license.jpg

Mapping Lookup:
├─ Find: #DocTypeMapping.SourceType = "other_type"
└─ Get: dtm.TargetTypeId = X1Y2Z3A4-B5C6-47D8-...

Result in Target:
├─ Id: 2345678...
├─ DocumentTypeId: X1Y2Z3A4-B5C6-47D8-... ✓ POPULATED (other_type GUID)
├─ LegacyDocumentType: "other_type" (original preserved)
└─ FileName: license.jpg
```

### Example 3: Source is "unmapped_type"
```
Source Document:
├─ Id: 3456789...
├─ DocumentType: "unmapped_type"  ← Doesn't exist in target
└─ FileName: unknown.pdf

Mapping Lookup:
├─ Find: #DocTypeMapping.SourceType = "unmapped_type"
└─ Get: dtm.TargetTypeId = A1B2C3D4-E5F6-47G8-... (fallback to identity_document)

Fallback Logic (Lines 266-277):
├─ Check: Is TargetTypeId NULL?
├─ Yes! So: SELECT identity_document GUID
└─ Update: Set TargetTypeId = A1B2C3D4-... (identity_document GUID)

Result in Target:
├─ Id: 3456789...
├─ DocumentTypeId: A1B2C3D4-E5F6-47G8-... ✓ POPULATED (identity_document as fallback)
├─ LegacyDocumentType: "unmapped_type" (original preserved)
└─ FileName: unknown.pdf
```

---

## Guaranteed Outcomes

### ✅ BEFORE INSERT (Migration checks):
```
-- Count unmapped types:
DECLARE @UnmappedCount INT = (
	SELECT COUNT(*) FROM #DocTypeMapping WHERE TargetTypeId IS NULL
)

-- If > 0, assign to identity_document
IF @UnmappedCount > 0
	UPDATE #DocTypeMapping 
	SET TargetTypeId = (SELECT Id FROM DocumentTypes WHERE Code = 'identity_document')
	WHERE TargetTypeId IS NULL
```

### ✅ DURING INSERT (Every document):
```
-- Every single document:
SELECT 
	...,
	ISNULL(dtm.TargetTypeId,  -- Use mapping if found
		   (SELECT TOP 1 Id FROM DocumentTypes ORDER BY Id))  -- Or default
	as DocumentTypeId,
```

### ✅ AFTER INSERT (Validation):
```
-- Check results:
SELECT 
	COUNT(*) as TotalDocs,
	COUNT(CASE WHEN DocumentTypeId IS NULL THEN 1 END) as NullCount
FROM MMGTDB2A.dbo.ApplicationDocuments

-- Expected:
-- TotalDocs | NullCount
-- ──────────────────────
-- 100000    | 0  ← ZERO NULL DocumentTypeIds!
```

---

## SQL Join Visualization

### The Critical Join (Line 317):
```
Source Table (MMGTDBCOPY.dbo.ApplicationDocuments):
┌──────┬──────────────────────┐
│ Id   │ DocumentType (str)   │
├──────┼──────────────────────┤
│ 1    │ "identity_document"  │ ──┐
│ 2    │ "identity_document"  │ ──┤
│ 3    │ "other_type"         │ ──┤
│ 4    │ "unmapped_type"      │ ──┤
└──────┴──────────────────────┘   │
								  │ JOIN ON
								  │ ad.DocumentType = dtm.SourceType
Mapping Table (#DocTypeMapping):  │
┌──────────────────────┬─────────┐│
│ SourceType (str)     │ TargetTypeId (GUID) ││
├──────────────────────┼─────────┤│
│ "identity_document"  │ A1B2C3D4-... │──┤
│ "other_type"         │ X1Y2Z3A4-... │──┤
│ "unmapped_type"      │ A1B2C3D4-... │ (fallback)
└──────────────────────┴─────────┘│
								  │
					↓ Result of JOIN:

Target Insert (MMGTDB2A.dbo.ApplicationDocuments):
┌──────┬────────────────────┐
│ Id   │ DocumentTypeId     │
├──────┼────────────────────┤
│ 1    │ A1B2C3D4-...     │ ← Mapped from "identity_document"
│ 2    │ A1B2C3D4-...     │ ← Mapped from "identity_document"
│ 3    │ X1Y2Z3A4-...     │ ← Mapped from "other_type"
│ 4    │ A1B2C3D4-...     │ ← Mapped from "unmapped_type" (fallback)
└──────┴────────────────────┘
```

---

## The Answer in One Line

```
YES! ApplicationDocuments.DocumentTypeId will be populated with the GUID 
that corresponds to the source DocumentType string through the mapping 
lookup table, with unmapped types falling back to identity_document GUID.

Result: 100% of documents have valid DocumentTypeId populated (zero NULLs).
```

---

## Verification

After migration completes, you can verify:

```sql
-- Show all documents with their mapped DocumentTypeId:
SELECT TOP 10
	a.ApplicationNumber,
	ad.FileName,
	ad.LegacyDocumentType,        -- Original source string
	ad.DocumentTypeId,            -- Mapped GUID ← THIS IS POPULATED!
	dt.Code as MappedCode,
	dt.Name as MappedName
FROM MMGTDB2A.dbo.ApplicationDocuments ad
LEFT JOIN MMGTDB2A.dbo.Applications a ON a.Id = ad.ApplicationId
LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON dt.Id = ad.DocumentTypeId
ORDER BY ad.CreatedAt DESC

-- Expected result: All rows show DocumentTypeId populated with GUID
```
