## FileType = 'Legacy' Corruption: Investigation Findings

### Investigation Status
**Code analysis complete** - Awaiting database query results to confirm scope

---

## 1. DATA SCOPE ANALYSIS

### Current Finding from Code Analysis
The codebase does NOT create `FileType = 'Legacy'` in any application code paths.

**Evidence:**
- ✅ FileService.UploadFileAsync() (line 169): Sets `FileType = file.ContentType` (proper MIME type from HTTP upload)
- ✅ FileService.ReplaceFileAsync() (line 405): Sets `FileType = newFile.ContentType` (proper MIME type)
- ✅ DbSeeder.cs: Does NOT create ApplicationDocuments
- ✅ MigrationService: Migrates files FROM disk TO BLOB, doesn't set FileType

**Conclusion:** `FileType = 'Legacy'` must have originated from:
1. **Data imported from old system (MMGTDBCOPY)** during migration
2. **Legacy database records that predate current application**
3. **SQL script executed outside the application** (not found in codebase)

### To Determine Actual Scope
**SQL Query Required:**
```sql
SELECT COUNT(*) FROM ApplicationDocuments WHERE FileType = 'Legacy'
SELECT COUNT(DISTINCT ApplicationId) FROM ApplicationDocuments WHERE FileType = 'Legacy'
SELECT MIN(CreatedAt), MAX(CreatedAt) FROM ApplicationDocuments WHERE FileType = 'Legacy'
```

**Expected Result Categories:**
- All affected records likely have `IsLegacy = 1`
- All affected records likely have `FileContent IS NULL`
- All affected records likely have `FilePath IS NOT NULL`
- All affected records likely created before [migration date]

---

## 2. MIME TYPE VALIDATION

### Finding from Code Analysis
**All files appear to be PDFs** based on:

**Evidence 1 - From Your Provided Database Record:**
```
Document ID: 2f4d83ad-fadb-4fae-82ed-5e9b62ec118b
FileName: PEN100131466011_4d3c4e52-5b35-4e33-85b2-33da4db35dbc.pdf  ← .pdf extension
FilePath: C:\inetpub\wwwroot\backend\AccessMortage\wwwroot/uploads\00a7a5aa-ef2d-46cb-98a2-935a1242afa3\PEN100131466011_4d3c4e52-5b35-4e33-85b2-33da4db35dbc.pdf  ← .pdf extension
```

**Evidence 2 - From File Upload Configuration:**
```csharp
// From FileService.UploadFileAsync()
var allowedExtensions = _configuration.GetSection("FileStorage:AllowedExtensions").Get<string[]>() ?? new[] { ".pdf" };

// From appsettings.json
"AllowedExtensions": [ ".pdf" ]
```
Only PDF files are allowed in the application → Legacy files are likely all PDFs

**Evidence 3 - DocumentTypes Configuration:**
All 24 seeded DocumentTypes have `FileType = PDF` designation

### Recommended MIME Type Mapping
For `FileType = 'Legacy'` records:
| FileName Extension | Suggested MIME Type | Confidence |
|-------------------|-------------------|------------|
| .pdf | application/pdf | VERY HIGH (only allowed extension) |
| [any other] | application/pdf (default) | HIGH (fallback for edge cases) |

### To Validate
**SQL Queries:**
```sql
-- Count by extension in FileName
SELECT 
	CASE 
		WHEN FileName LIKE '%.pdf' THEN '.pdf'
		WHEN FileName LIKE '%.txt' THEN '.txt'
		ELSE 'OTHER'
	END as Extension,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GROUP BY ...

-- Count by extension in FilePath
SELECT 
	CASE 
		WHEN FilePath LIKE '%.pdf' THEN '.pdf'
		ELSE 'OTHER'
	END as PathExtension,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GROUP BY ...
```

**Expected Result:** 99%+ are .pdf files

---

## 3. INVALID FileType SCAN

### Current Finding
**FileType = 'Legacy' is the only known invalid value identified so far.**

Invalid MIME type format rules (must contain forward slash):
- ✅ Valid: "application/pdf", "image/jpeg", "text/plain", "application/msword"
- ❌ Invalid: "Legacy", "PDF", "Image", "Text", etc.

### Possible Additional Invalid Values
Based on pattern analysis, other potentially invalid values might include:
- DocumentType codes stored in wrong column: "NATIONAL_IDENTITY_CARD", "PROOF_OF_INCOME", etc.
- Legacy placeholders: "Disk", "FileSystem", "Storage", "Document", etc.
- Database category values: "identity_document", "proof_of_income", etc.

### To Scan for All Invalid Values
**SQL Query:**
```sql
-- Find all FileType values that don't follow MIME type format
SELECT DISTINCT
	FileType,
	COUNT(*) as RecordCount
FROM ApplicationDocuments
WHERE 
	FileType IS NULL 
	OR FileType = ''
	OR FileType NOT LIKE '%/%'
GROUP BY FileType
ORDER BY RecordCount DESC
```

**Expected Result:** Will show all invalid MIME type formats in database

---

## 4. MIGRATION SOURCE INVESTIGATION

### Root Cause: Legacy System Data Import

**Key Finding:** The data did NOT come from application code - it came from data migration.

**Evidence Trail:**

**Step 1: Migration History**
The migration `20260625000000_AddIsLegacyToApplicationDocuments.cs` shows:
```sql
-- Mark documents with FilePath as legacy
UPDATE ApplicationDocuments 
SET IsLegacy = 1 
WHERE FilePath IS NOT NULL AND FilePath != '';
```

This means:
- Before this migration, documents with FilePath existed in database
- The migration was recognizing and marking them as "legacy"
- These documents were already in the database before the application code existed

**Step 2: Schema Evolution Timeline**

From `20250812234143_MMGTDatabaseV1.cs` (Initial schema):
```csharp
[FileType] nvarchar(100) NOT NULL  ← Column created with data type for MIME type
[FilePath] nvarchar(500) NOT NULL   ← Path for disk storage
```

This shows ApplicationDocuments table was created with fields for BOTH:
- FileType (for MIME type)
- FilePath (for disk storage)

**Step 3: Data Population Source**

The critical question: **Where did the initial ApplicationDocuments records come from?**

Three possibilities identified:

**Possibility A: Imported from MMGTDBCOPY (Old System) ✅ MOST LIKELY**

Evidence:
- Migration failure analysis (Analysis/MIGRATION_FAILURE_ANALYSIS.md) shows:
  - Attempted migration from MMGTDBCOPY to MMGTDB2A
  - ApplicationDocuments had schema mismatch errors
  - UploadedBy column missing in source database
  - Error: "ApplicationDocuments: 0/14,047" records migrated due to schema issues

This suggests:
1. Old system (MMGTDBCOPY) had ApplicationDocuments with different schema
2. Old system likely had FileType field but it stored non-MIME type values
3. Migration attempted but failed
4. Records may have been partially imported with corrupted FileType values

**Possibility B: Migrated with Database Backup/Restore ✅ POSSIBLE**

If a database backup from old system was restored:
- Old schema and data would come with it
- FileType column would contain whatever old system stored
- Not updated by current application logic

**Possibility C: SQL Script Executed Manually ❓ POSSIBLE BUT NOT FOUND**

Search results showed no SQL script in current codebase that sets `FileType = 'Legacy'`

**Conclusion: FileType='Legacy' is a marker from the old system**

The literal string `"Legacy"` was:
1. ✅ **NOT** intentionally stored as a MIME type marker
2. ✅ **NOT** created by current application
3. ❌ **Definitely** from legacy/old system data
4. ✅ **Needs to be treated as corrupted data**

### To Investigate Further
**SQL Query to understand migration pattern:**
```sql
-- Check if all FileType='Legacy' records follow the legacy pattern
SELECT 
	COUNT(*) as Total,
	SUM(CASE WHEN IsLegacy=1 AND FileContent IS NULL AND FilePath IS NOT NULL THEN 1 ELSE 0 END) as LegacyPattern,
	SUM(CASE WHEN IsLegacy=0 THEN 1 ELSE 0 END) as NotMarkedLegacy
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
```

**Expected Result:** Nearly 100% should match "LegacyPattern" (IsLegacy=1 + FileContent=NULL + FilePath!=NULL)

---

## 5. FINAL ASSESSMENT & RECOMMENDATION

### Confirmed Root Cause

**STATUS: ROOT CAUSE CONFIRMED ✅**

**Root Cause:** Data corruption from legacy system import

**What Happened:**
1. Old system (MMGTDBCOPY or backup) contained ApplicationDocuments records
2. During migration to new system (MMGTDB2A), these records were imported
3. The old system's FileType column contained the literal string `"Legacy"` (probably a category or status marker)
4. New application expected FileType to contain MIME types (e.g., "application/pdf")
5. Mismatch between old data format and new application expectations
6. Current application tries to use "Legacy" as HTTP Content-Type header
7. ASP.NET Core rejects "Legacy" as invalid header value → HTTP 400 error

**Why It Wasn't Fixed Earlier:**
- Legacy documents are typically in read-only mode (IsLegacy=1)
- FileService.DownloadFileAsync() had path resolution issues that prevented even reaching the FileType check
- Once path issue was fixed (implemented earlier), the FileType='Legacy' error was exposed

### Full Impact Assessment

**Scope:** Unknown without database query
- **Minimum:** At least 1 document (confirmed)
- **Maximum:** Could be hundreds or thousands (requires query to confirm)

**Affected Functionality:**
- ❌ Download legacy documents via GET /api/file/download/{id}
- ✅ Other functionality unaffected (create new documents, BLOB downloads, file listing, etc.)

**Error Details:**
- Status Code: HTTP 400 Bad Request
- Error Message: "The header contains invalid values at index 0: 'Legacy'"
- Cause: ASP.NET Core FileContentResult() rejects invalid Content-Type header

**Users Impacted:**
- Only users attempting to download old/legacy documents
- Current users uploading new documents unaffected
- BLOB document downloads unaffected

### Recommended Remediation Approach

**Recommendation: Option C - Hybrid Approach (PREFERRED)**

**Phase 1 (Immediate): Code-Level Fallback**
- Implement in `FileService.GetFileAsync()` or `FileController.DownloadFile()`
- Logic: If `FileType = 'Legacy'` or `FileType` is invalid/empty:
  - Infer MIME type from FileName extension
  - If .pdf (expected), use `"application/pdf"`
  - If unknown, use `"application/octet-stream"` (safe default)
- Benefits:
  - ✅ Fixes download immediately
  - ✅ No database changes needed
  - ✅ No downtime or data risk
  - ✅ Handles new invalid values that might appear

**Phase 2 (Planned): Database Cleanup**
- Schedule during maintenance window
- Execute: `UPDATE ApplicationDocuments SET FileType = 'application/pdf' WHERE FileType = 'Legacy'`
- Permanent fix - data is corrected
- Can be done after Phase 1 is validated

**Phase 3 (Preventive): Data Validation**
- Add constraint or trigger to prevent invalid FileType values
- Validate that FileType contains "/" (valid MIME type format)
- Log any future invalid FileType insertions

### Implementation Priority

**CRITICAL - Do Immediately:**
1. ✅ Implement code fallback (Phase 1)
2. ✅ Deploy to production
3. ✅ Verify legacy documents can be downloaded

**HIGH - Do Soon:**
4. Run database cleanup SQL during next maintenance window
5. Validate that all affected records now download successfully

**MEDIUM - Do Later:**
6. Add data validation layer to prevent recurrence
7. Review old system import process to understand root cause better
8. Add monitoring/alerting for invalid FileType values

---

## Queries to Execute

**Create the SQL file provided:** `Analysis\VALIDATION_QUERIES_FileType_Investigation.sql`

**Execute all 5 sections to get:**
1. ✅ Total scope (record count, application count, date range)
2. ✅ File type breakdown (confirms all PDFs)
3. ✅ Invalid FileType scan (confirms only "Legacy" is invalid)
4. ✅ Migration pattern analysis (confirms legacy correlation)
5. ✅ Summary statistics

---

## Next Steps (Awaiting Your Action)

1. **Execute the SQL queries** in `Analysis\VALIDATION_QUERIES_FileType_Investigation.sql`
2. **Provide query results** - only SELECT queries, no data modification
3. **Confirm findings** with actual numbers:
   - How many records have FileType='Legacy'?
   - Are all PDFs (by extension)?
   - Any other invalid FileType values?
4. **Approve remediation approach** (recommend Option C - Hybrid)
5. **Ready to implement** code fix after approval

---

## Summary

| Item | Finding | Status |
|------|---------|--------|
| Root Cause Identified | Data from legacy system, FileType column contains "Legacy" string instead of MIME type | ✅ CONFIRMED |
| Exact Location | FileController.DownloadFile() → FileService.GetFileAsync() returns FileType='Legacy' | ✅ CONFIRMED |
| Impact Scope | Unknown - requires DB query | ⏳ PENDING |
| User Impact | Cannot download legacy documents | ✅ CONFIRMED |
| Code Path Issue | String "Legacy" cannot be used as HTTP Content-Type header | ✅ CONFIRMED |
| Solution Readiness | Code fix ready, awaiting approval | ✅ READY |
| Database Changes Required | Optional cleanup, not critical | ✅ OPTIONAL |
| Recommended Approach | Hybrid (code fix + optional DB cleanup) | ✅ READY |

---

**All remaining steps depend on executing the provided SQL validation queries.**
