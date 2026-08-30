# Phase 2: Legacy Document Handling - Quick Reference

## One-Page Summary

### What is Legacy Document Handling?

Legacy documents are existing application files stored on physical disk before the system migrated to BLOB storage.

**Key Facts**:
- Stored on disk at paths like: `uploads/{applicationId}/{documentId}.pdf`
- Identified by: `IsLegacy = 1` in `ApplicationDocuments` table
- Read-only: Cannot be replaced or deleted after marking as legacy
- Downloads still work: Backend checks BLOB first, then disk
- Coexist with new BLOB documents in same application

---

## Critical Tables & Columns

### ApplicationDocuments Table

| Column | Legacy Value | New Value | Purpose |
|--------|---------------|-----------|---------|
| **IsLegacy** | 1 | 0 | Identifier |
| **FilePath** | NOT NULL (path) | NULL | Disk location |
| **FileContent** | NULL | NOT NULL (bytes) | BLOB storage |
| **DocumentTypeId** | GUID | GUID | Links to DocumentTypes |
| **FileName** | Original name | Original name | Display name |
| **FileSize** | Bytes | Bytes | File size |

### DocumentTypes Table

**24 types seeded** (all active, all PDF, all 300 KB max):
- 21 mandatory (NATIONAL_IDENTITY_CARD, PROOF_OF_INCOME, etc.)
- 3 optional (ADDITIONAL_DOCUMENTS, SUPPORTING_DOCUMENTS, etc.)

**NO separate "Legacy" type** - any type can be legacy

---

## API Endpoints for Legacy Document Access

### Download Legacy Document

```http
GET /api/file/download/{fileId}
Authorization: Bearer {token}
```

**Response**: File bytes (PDF stream)

**Supports**: Both BLOB and disk files

### List Documents for Application

```http
GET /api/file/application/{applicationId}
Authorization: Bearer {token}
```

**Response**: JSON array with `IsLegacy` flag per document

---

## Backend Classes Responsible

| Class | Method | Purpose |
|-------|--------|---------|
| **FileController** | `DownloadFile(fileId)` | API endpoint for retrieval |
| **FileService** | `DownloadFileAsync(fileId)` | Fetch bytes (dual-mode) |
| **FileService** | `DeleteFileAsync(fileId)` | Blocked if legacy |
| **FileService** | `ReplaceFileAsync(fileId)` | Blocked if legacy |
| **ApplicationDocument** | `IsLegacy` property | Identifier flag |

---

## Download Logic (Simplified)

```
Client requests: GET /api/file/download/{fileId}
  ↓
FileService.DownloadFileAsync(fileId)
  ↓
  IF FileContent NOT NULL
	→ Return BLOB bytes
  ELSE IF FilePath NOT NULL
	→ Read from disk: File.ReadAllBytesAsync(FilePath)
  ELSE
	→ Error: No content or path
  ↓
Controller returns: File(bytes, mimeType, fileName)
  ↓
Browser downloads PDF
```

---

## Legacy Document Rules

### What You CAN Do

✅ Download legacy documents  
✅ View legacy document metadata  
✅ List legacy documents in an application  
✅ Review legacy documents (set ReviewStatus)  

### What You CANNOT Do

❌ Replace legacy documents (IsLegacy=1)  
❌ Delete legacy documents (IsLegacy=1)  
❌ Upload to existing legacy document slot  
❌ Modify FilePath after creation  

### Error Behavior

**If you try to replace/delete legacy document**:
```json
{
  "message": "Cannot modify legacy documents"
}
```

---

## Expected State After Phase 2 Migration

### Existing Applications (from MMGTDBCOPY)

| Aspect | Before Migration | After Migration |
|--------|------------------|-----------------|
| Legacy documents | ✅ Present on disk | ✅ Still on disk (copied) |
| IsLegacy flag | ✅ Set to 1 | ✅ Preserved |
| FilePath column | ✅ Populated | ✅ Preserved |
| FileContent | ✅ NULL | ✅ NULL |
| Download works | ✅ Yes | ✅ Yes (same code) |
| Viewable to users | ✅ Yes | ✅ Yes |

### New Applications (post-migration)

| Aspect | Behavior |
|--------|----------|
| Upload | BLOB-only (no disk storage) |
| IsLegacy | 0 |
| FilePath | NULL |
| FileContent | NOT NULL (bytes) |
| Download | Via same endpoint |
| Replaceable | Yes (not legacy) |
| Deletable | Yes (not legacy) |

---

## Pre-Migration Validation Checklist

**Run these SQL queries from**: `Analysis/Phase2_Legacy_Document_Investigation.sql`

- [ ] Query 2: How many legacy documents exist?
- [ ] Query 3: Do all legacy documents have valid paths?
- [ ] Query 4: Are there any inconsistencies?
- [ ] Query 11-12: Foreign keys valid and intact?
- [ ] Manual: Can server reach disk storage path?
- [ ] Manual: Files exist at stored paths?
- [ ] Manual: File sizes ≤ 300 KB?

---

## Key SQL Queries

### Count Legacy Documents

```sql
SELECT COUNT(*) as LegacyDocuments
FROM ApplicationDocuments
WHERE IsLegacy = 1;
```

### Find Applications with Legacy Docs

```sql
SELECT DISTINCT ApplicationId
FROM ApplicationDocuments
WHERE IsLegacy = 1
ORDER BY ApplicationId;
```

### Verify Legacy Document Integrity

```sql
SELECT 
	Id,
	FileName,
	FilePath,
	CASE 
		WHEN FileContent IS NULL AND FilePath IS NOT NULL THEN 'Valid'
		WHEN FileContent IS NULL AND FilePath IS NULL THEN 'INVALID'
		WHEN FileContent IS NOT NULL THEN 'BLOB (not legacy)'
	END as Status
FROM ApplicationDocuments
WHERE IsLegacy = 1;
```

### Check for Orphans

```sql
SELECT COUNT(*) as OrphanCount
FROM ApplicationDocuments ad
WHERE IsLegacy = 1 
  AND (
	ApplicationId NOT IN (SELECT Id FROM Applications)
	OR DocumentTypeId NOT IN (SELECT Id FROM DocumentTypes)
  );
```

---

## Potential Issues & Solutions

### Issue: Legacy document downloaded returns 404/error

**Cause**: FilePath points to non-existent disk location
**Solution**: 
1. Verify file exists at FilePath location
2. Check if server path is accessible from new database server
3. Restore file from backup if deleted

### Issue: New files uploaded as legacy by mistake

**Cause**: IsLegacy not set to 0 during upload
**Solution**: 
1. Never happens - code enforces IsLegacy=0 on upload
2. If found: manual data correction required

### Issue: Legacy document shows in UI but can't download

**Cause**: Disk file missing or FileContent/FilePath both NULL
**Solution**:
1. Run validation query to find inconsistent records
2. Restore file from backup
3. Update FilePath in database if location changed

---

## Configuration Settings

**File Storage** (appsettings.json):
```json
{
  "FileStorage": {
	"StorageType": "Database",  // New uploads → Database
	"MaxFileSizeInMB": 0.3,     // 300 KB limit
	"AllowedExtensions": [".pdf"]  // PDF only
  }
}
```

**Database** (connection):
```json
{
  "ConnectionStrings": {
	"DefaultConnection": "Server=172.16.50.42;Database=MMGTDB2A;..."
  }
}
```

---

## Migration Readiness

**Status**: Investigation ✅ COMPLETE

**Ready for Phase 2 Execution**:
- ✅ Legacy document flow is understood
- ✅ Backend code supports coexistence
- ✅ Database schema supports both storage types
- ✅ Validation queries prepared
- ✅ Risk assessment complete

**Awaiting**: User approval to begin Phase 2 data migration

---

## File References

**Investigation Report**: `Analysis/Phase2_Legacy_Document_Handling_Investigation_Report.md` (detailed)  
**SQL Validation**: `Analysis/Phase2_Legacy_Document_Investigation.sql` (12 queries)  
**Code**: 
- `Services/FileService.cs` (download logic)
- `Controllers/FileController.cs` (API endpoints)
- `Models/ApplicationDocument.cs` (entity with IsLegacy)

**Previous Documentation**:
- `FILE_HANDLING_ARCHITECTURE.md` (full hybrid model)
- `FILE_HANDLING_QUICK_REFERENCE.md` (condensed)
- `FILE_HANDLING_DOCUMENTATION_INDEX.md` (navigation)

---

**Investigation Status**: Investigation-Only (No Code/DB Changes Made)  
**Next Action**: Approve Phase 2 Migration Execution
