# Phase 2: Legacy Document Handling - Technical Deep Dive

## Overview for Developers

This document provides technical details for developers implementing or troubleshooting legacy document handling in the AccessMortgage system.

---

## Storage Model: Hybrid BLOB + Disk

### Entity Design

```csharp
public class ApplicationDocument
{
	// Identifier
	public Guid Id { get; set; }  // Primary key
	public Guid ApplicationId { get; set; }  // Parent app
	public Guid DocumentTypeId { get; set; }  // Type reference

	// Storage: Two Paths
	public string? FilePath { get; set; }  // Legacy disk path
	public byte[]? FileContent { get; set; }  // New BLOB content

	// Critical Flag
	public bool IsLegacy { get; set; }  // 0=New (BLOB), 1=Disk

	// Metadata
	public string FileName { get; set; }
	public string FileType { get; set; }
	public long FileSize { get; set; }
	public int Version { get; set; }

	// Workflow
	public string ReviewStatus { get; set; }  // Pending, Approved, Rejected
	public Guid? ReviewedBy { get; set; }
	public DateTime? ReviewedAt { get; set; }
}
```

### Four Storage States

| State | IsLegacy | FilePath | FileContent | Meaning | Behavior |
|-------|----------|----------|-------------|---------|----------|
| **New BLOB** | 0 | NULL | bytes | Modern storage | ✅ Replaceable, deletable |
| **Legacy Disk** | 1 | path | NULL | Old storage | ❌ Read-only |
| **Hybrid** | 0/1 | path | bytes | Transitional | ⚠️ Unusual (shouldn't exist) |
| **Invalid** | 1 | NULL | NULL | Error state | ❌ No content source |

---

## FileService Implementation Details

### Core Download Logic

**Location**: `Services/FileService.cs` → `DownloadFileAsync()`

**Decision Tree**:
```csharp
public async Task<byte[]> DownloadFileAsync(Guid fileId)
{
	var document = await _context.ApplicationDocuments
		.AsNoTracking()  // Read-only query
		.FirstOrDefaultAsync(d => d.Id == fileId);

	if (document == null)
		throw new FileNotFoundException($"Document {fileId} not found");

	// Path 1: Check for BLOB (preferred for performance)
	if (document.FileContent != null && document.FileContent.Length > 0)
	{
		// Return database BLOB (fastest)
		return document.FileContent;
	}

	// Path 2: Fall back to disk (legacy)
	if (document.FilePath != null)
	{
		// Validate file exists first
		if (!File.Exists(document.FilePath))
		{
			throw new FileNotFoundException(
				$"Legacy file not found at path: {document.FilePath}. " +
				$"Document ID: {fileId}. " +
				$"This may indicate the physical file was deleted or moved.");
		}

		// Read from disk
		try
		{
			return await File.ReadAllBytesAsync(document.FilePath);
		}
		catch (Exception ex)
		{
			throw new IOException(
				$"Error reading legacy file from {document.FilePath}: {ex.Message}");
		}
	}

	// Neither BLOB nor disk path available - error
	throw new FileNotFoundException(
		$"Document {fileId} has no FileContent (BLOB) or FilePath (disk). " +
		$"Data appears corrupted.");
}
```

### Protection: Delete Operation

**Location**: `Services/FileService.cs` → `DeleteFileAsync()`

```csharp
public async Task<bool> DeleteFileAsync(Guid fileId, Guid performedById)
{
	var document = await _context.ApplicationDocuments.FindAsync(fileId);
	if (document == null)
		return false;

	// Block deletion if marked as legacy
	if (document.IsLegacy)
	{
		throw new InvalidOperationException(
			$"Cannot delete legacy document {fileId}. " +
			$"Legacy documents are read-only and must be preserved for historical reference.");
	}

	// Block deletion if disk file reference exists (orphan state)
	if (document.FilePath != null && document.FileContent == null)
	{
		throw new InvalidOperationException(
			$"Cannot delete document with orphaned FilePath. " +
			$"This document may be marked legacy incorrectly.");
	}

	// Safe to delete: remove from context and save
	_context.ApplicationDocuments.Remove(document);
	await _context.SaveChangesAsync();

	return true;
}
```

### Protection: Replace Operation

**Location**: `Services/FileService.cs` → `ReplaceFileAsync()`

```csharp
public async Task<FileUploadResponse> ReplaceFileAsync(
	Guid existingFileId,
	IFormFile newFile,
	Guid performedById)
{
	var existingDoc = await _context.ApplicationDocuments
		.FirstOrDefaultAsync(d => d.Id == existingFileId);

	if (existingDoc == null)
		throw new FileNotFoundException($"Document {existingFileId} not found");

	// Block replacement if legacy
	if (existingDoc.IsLegacy)
	{
		throw new InvalidOperationException(
			$"Cannot replace legacy document {existingFileId}. " +
			$"Legacy documents must remain unchanged for compliance reasons.");
	}

	// Validate new file
	ValidateFile(newFile);  // Checks PDF, 300 KB max

	// Create new version in the same slot
	var fileBytes = new byte[newFile.Length];
	await newFile.OpenReadStream().ReadAsync(fileBytes);

	existingDoc.FileContent = fileBytes;
	existingDoc.FilePath = null;  // Clear disk reference
	existingDoc.FileSize = fileBytes.Length;
	existingDoc.FileType = newFile.ContentType;
	existingDoc.Version++;
	existingDoc.ReviewStatus = "Pending";  // Reset review
	existingDoc.ReviewedBy = null;
	existingDoc.ReviewedAt = null;

	await _context.SaveChangesAsync();

	return new FileUploadResponse
	{
		FileId = existingDoc.Id,
		FileName = existingDoc.FileName,
		FileSize = existingDoc.FileSize,
		IsBlobStored = true,
		IsLegacy = false
	};
}
```

### Metadata Retrieval

**Location**: `Services/FileService.cs` → `GetFileAsync()`

```csharp
public async Task<FileUploadResponse> GetFileAsync(Guid fileId)
{
	var document = await _context.ApplicationDocuments
		.AsNoTracking()
		.Where(d => d.Id == fileId)
		.Include(d => d.DocumentType)
		.FirstOrDefaultAsync();

	if (document == null)
		return null;

	// Determine storage type
	bool isBlobStored = document.FileContent != null && document.FileContent.Length > 0;

	return new FileUploadResponse
	{
		FileId = document.Id,
		ApplicationId = document.ApplicationId,
		FileName = document.FileName,
		FileSize = document.FileSize,
		FileType = document.FileType,
		DocumentTypeCode = document.DocumentType?.Code,
		DocumentTypeName = document.DocumentType?.Name,
		ReviewStatus = document.ReviewStatus,
		ReviewComments = document.ReviewComments,
		ReviewedBy = document.ReviewedBy,
		ReviewedAt = document.ReviewedAt,
		CreatedAt = document.CreatedAt,
		UploadedBy = document.UploadedBy,
		Version = document.Version,

		// Critical flags
		IsBlobStored = isBlobStored,  // Derived from FileContent length
		IsLegacy = document.IsLegacy  // Direct from DB
	};
}
```

### List by Application

**Location**: `Services/FileService.cs` → `GetFilesByApplicationAsync()`

```csharp
public async Task<List<FileUploadResponse>> GetFilesByApplicationAsync(Guid applicationId)
{
	var documents = await _context.ApplicationDocuments
		.AsNoTracking()
		.Where(d => d.ApplicationId == applicationId)
		.Include(d => d.DocumentType)
		.OrderByDescending(d => d.CreatedAt)
		.Select(d => new FileUploadResponse
		{
			FileId = d.Id,
			FileName = d.FileName,
			FileSize = d.FileSize,
			ReviewStatus = d.ReviewStatus,
			CreatedAt = d.CreatedAt,
			UploadedBy = d.UploadedBy,
			DocumentTypeCode = d.DocumentType.Code,
			DocumentTypeName = d.DocumentType.Name,

			// Include storage info for each doc
			IsBlobStored = d.FileContent != null && d.FileContent.Length > 0,
			IsLegacy = d.IsLegacy
		})
		.ToListAsync();

	return documents;
}
```

---

## API Endpoints

### File Download

```http
GET /api/file/download/{fileId}
Authorization: Bearer {token}
```

**Handler**:
```csharp
[HttpGet("download/{fileId}")]
public async Task<IActionResult> DownloadFile(Guid fileId)
{
	try
	{
		var document = await _fileService.GetFileAsync(fileId);
		var fileBytes = await _fileService.DownloadFileAsync(fileId);

		return File(
			fileBytes,
			document.FileType,  // application/pdf
			document.FileName   // passport_scan.pdf
		);
	}
	catch (FileNotFoundException ex)
	{
		return NotFound(new { error = ex.Message });
	}
}
```

**Response: 200 OK**
```
Content-Type: application/pdf
Content-Disposition: attachment; filename="passport_scan.pdf"
Content-Length: 245781

[Binary PDF data...]
```

### List Application Documents

```http
GET /api/file/application/{applicationId}
Authorization: Bearer {token}
```

**Response: 200 OK**
```json
[
  {
	"fileId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
	"fileName": "passport_scan.pdf",
	"fileSize": 245781,
	"reviewStatus": "Pending",
	"createdAt": "2025-01-10T14:30:00Z",
	"documentTypeCode": "NATIONAL_IDENTITY_CARD",
	"isBlobStored": false,
	"isLegacy": true
  },
  {
	"fileId": "550e8400-e29b-41d4-a716-446655440000",
	"fileName": "proof_of_income.pdf",
	"fileSize": 102400,
	"reviewStatus": "Approved",
	"createdAt": "2025-01-15T10:00:00Z",
	"documentTypeCode": "PROOF_OF_INCOME",
	"isBlobStored": true,
	"isLegacy": false
  }
]
```

---

## Migration: What Happens

### Before Phase 2

```
MMGTDBCOPY database:
├── Users: 110
├── PMICompanies: 24
├── Applications: 13,789
├── ApplicationDocuments: 14,047
│   ├── Legacy (IsLegacy=1): ~12,000
│   │   ├── FilePath: "uploads/app-uuid/doc-uuid.pdf"
│   │   ├── FileContent: NULL
│   │   └── Status: Read-only, on-disk
│   │
│   └── New (IsLegacy=0): ~2,000
│       ├── FilePath: NULL
│       ├── FileContent: bytes (BLOB)
│       └── Status: Replaceable, deletable
│
└── ApplicationReviewHistory: 31,135
```

### After Phase 2 → MMGTDB2A

```
MMGTDB2A database (all data copied):
├── Users: 110 (copied)
├── PMICompanies: 24 (copied)
├── Applications: 13,789 (copied + new columns)
├── ApplicationDocuments: 14,047 (copied as-is)
│   ├── Legacy (IsLegacy=1): ~12,000
│   │   ├── FilePath: "uploads/app-uuid/doc-uuid.pdf" (PRESERVED)
│   │   ├── FileContent: NULL (PRESERVED)
│   │   └── Status: Still read-only
│   │
│   └── New (IsLegacy=0): ~2,000+
│       ├── FilePath: NULL
│       ├── FileContent: bytes (BLOB in new DB)
│       └── Status: Replaceable, deletable
│
└── ApplicationReviewHistory: 31,135 (copied)
```

### Critical Invariants

1. **IsLegacy flag value MUST be preserved** (0 → 0, 1 → 1)
2. **FilePath strings MUST NOT be modified** (original path must work)
3. **FileContent MUST remain NULL for legacy docs** (don't try to read disk to BLOB)
4. **Physical files MUST still exist** at FilePath locations
5. **GUID values MUST be preserved** (maintain all FK relationships)

---

## Troubleshooting

### Problem: Legacy document returns 404 on download

**Symptoms**:
- User requests GET /api/file/download/{legacyDocId}
- Returns: 404 NotFound
- Error: "File not found at path..."

**Root Causes**:
1. Physical file no longer exists at FilePath
2. Server cannot access FilePath location (network/permissions)
3. FilePath is corrupted or NULL
4. IsLegacy flag not set (tries to find in BLOB, fails)

**Diagnostic Steps**:

```sql
-- Find the document
SELECT Id, IsLegacy, FilePath, FileContent
FROM ApplicationDocuments
WHERE Id = '{legacyDocId}';

-- Expected:
-- IsLegacy: 1
-- FilePath: 'uploads/...'
-- FileContent: NULL
```

```powershell
# Verify file physically exists
Test-Path "\\fileserver\uploads\550e8400-e29b-41d4-a716\f47ac10b-58cc.pdf"

# If false, restore from backup
# If true, check permissions
Get-Acl "\\fileserver\uploads\550e8400-e29b-41d4-a716\f47ac10b-58cc.pdf"
```

**Solutions**:
- Restore file from backup
- Fix FilePath if corrupted (UPDATE ApplicationDocuments SET FilePath = '...')
- Ensure service account has read permissions
- Test network connectivity to file server

### Problem: Cannot modify (replace/delete) non-legacy document

**Symptoms**:
- User tries to replace non-legacy document
- Returns: 400 Bad Request
- Error: "Cannot modify legacy documents"

**Root Cause**:
- IsLegacy flag incorrectly set to 1

**Diagnostic**:
```sql
SELECT Id, IsLegacy, FilePath, FileContent
FROM ApplicationDocuments
WHERE Id = '{docId}' AND IsLegacy = 1;
```

**Solution**:
```sql
-- If this is actually a new BLOB document wrongly marked as legacy:
UPDATE ApplicationDocuments
SET IsLegacy = 0
WHERE Id = '{docId}'
  AND FileContent IS NOT NULL
  AND FilePath IS NULL;
```

### Problem: User creates new document but it appears as legacy

**Symptoms**:
- New upload shows IsLegacy=true in API response
- Document appears read-only
- Cannot modify/delete

**Root Cause**:
- FileService.UploadFileAsync() not setting IsLegacy=0
- Or legacy data migration incorrectly marked new docs

**Check Code**:
```csharp
// In FileService.UploadFileAsync(), should be:
var newDocument = new ApplicationDocument
{
	IsLegacy = false,  // NOT true
	FileContent = fileBytes,  // NOT NULL
	FilePath = null,  // NOT populated
	...
};
```

**Solution**:
1. Verify code has IsLegacy = false
2. If accidentally set in database, manually correct:
```sql
UPDATE ApplicationDocuments
SET IsLegacy = 0
WHERE FileContent IS NOT NULL
  AND FilePath IS NULL
  AND CreatedAt > '2025-01-15';  -- After phase 2
```

---

## SQL Inspection Queries

### Check Storage Type Distribution

```sql
SELECT 
	CASE 
		WHEN IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NULL 
			THEN 'Disk (Legacy)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NULL 
			THEN 'BLOB (New)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NOT NULL 
			THEN 'BLOB + Disk Reference'
		WHEN IsLegacy = 1 AND FileContent IS NOT NULL 
			THEN 'LEGACY MARKED but HAS BLOB'
		ELSE 'Unknown State'
	END as StorageType,
	COUNT(*) as DocumentCount
FROM ApplicationDocuments
GROUP BY 
	CASE 
		WHEN IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NULL 
			THEN 'Disk (Legacy)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NULL 
			THEN 'BLOB (New)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NOT NULL 
			THEN 'BLOB + Disk Reference'
		WHEN IsLegacy = 1 AND FileContent IS NOT NULL 
			THEN 'LEGACY MARKED but HAS BLOB'
		ELSE 'Unknown State'
	END
ORDER BY DocumentCount DESC;
```

### Find Anomalies

```sql
-- Legacy documents without disk path
SELECT COUNT(*) as AnomalyCount, 'Legacy but no FilePath' as Issue
FROM ApplicationDocuments
WHERE IsLegacy = 1 AND FilePath IS NULL
UNION ALL
-- New documents that are disk-referenced
SELECT COUNT(*), 'New but has FilePath'
FROM ApplicationDocuments
WHERE IsLegacy = 0 AND FilePath IS NOT NULL
UNION ALL
-- Documents with both storages
SELECT COUNT(*), 'Has both BLOB and FilePath'
FROM ApplicationDocuments
WHERE FileContent IS NOT NULL AND FilePath IS NOT NULL
UNION ALL
-- Orphan documents (no storage)
SELECT COUNT(*), 'No BLOB or FilePath'
FROM ApplicationDocuments
WHERE FileContent IS NULL AND FilePath IS NULL;
```

---

## Performance Considerations

### Download Performance

| Storage | First-time | Cached | Notes |
|---------|-----------|--------|-------|
| **BLOB** | ~50-100ms | instant | Database query + memory read |
| **Disk** | ~100-200ms | instant | File read from disk |

**Optimization**: 
- Preferred order: Check BLOB first (faster in-memory)
- Falls back to disk only if BLOB missing
- Use `AsNoTracking()` for read-only queries

### Disk File Path Considerations

**Issue**: Long FilePath strings stored in database

```
Max length: 500 characters
Pattern: uploads/{guid}/{guid}.pdf
Actual: 50 characters average
```

**Not a constraint**, but suggests files are organized efficiently.

### Database BLOB Storage

**Current Size** (estimated):
- Average PDF: 100-200 KB
- New documents: ~2,000
- Total BLOB size: ~200-400 MB

**Acceptable for**: SQL Server VARBINARY(MAX)

---

## Configuration (appsettings.json)

```json
{
  "FileStorage": {
	"StorageType": "Database",
	"MaxFileSizeInMB": 0.3,
	"AllowedExtensions": [".pdf"]
  },
  "ConnectionStrings": {
	"DefaultConnection": "Server={host};Database={database};Integrated Security=true;"
  }
}
```

**Impact**:
- New uploads → Always Database (BLOB)
- File validation → 300 KB + PDF only
- Existing files → Not re-validated

---

## Migration Validation SQL

Execute after Phase 2 data copy:

```sql
-- Step 1: Verify counts match
SELECT 'Applications' as TableName, COUNT(*) FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL
SELECT 'Users', COUNT(*) FROM Users;

-- Step 2: Verify legacy preservation
SELECT 
	COUNT(*) as LegacyPreserved
FROM ApplicationDocuments
WHERE IsLegacy = 1 
  AND FilePath IS NOT NULL 
  AND FileContent IS NULL;

-- Step 3: Verify no new BLOBs in legacy
SELECT COUNT(*) as IssueCount
FROM ApplicationDocuments
WHERE IsLegacy = 1 AND FileContent IS NOT NULL;
-- Should return 0

-- Step 4: Check FK integrity
SELECT COUNT(*) as OrphanCount
FROM ApplicationDocuments ad
WHERE ApplicationId NOT IN (SELECT Id FROM Applications);
-- Should return 0

-- Step 5: Data type validation
SELECT 
	COUNT(*) as ValidDocuments
FROM ApplicationDocuments
WHERE 
	ISNULL(FileContent, 0) != 0  -- If BLOB present
	OR FilePath IS NOT NULL;  -- Or disk path present
-- Should equal total document count
```

---

## Reference Architecture

```
┌─────────────────────────────────────────────┐
│         Frontend UI (React/Angular)         │
│  [Download] [Upload] [Replace] [Delete]     │
└────────────────────┬────────────────────────┘
					 │
					 ↓ HTTP
		┌────────────────────────┐
		│  FileController        │
		│  /api/file/download    │
		│  /api/file/upload      │
		│  /api/file/replace     │
		│  /api/file/{id}        │
		└───────────┬────────────┘
					│
					↓ DI
	   ┌─────────────────────────┐
	   │   FileService (IFS)     │
	   │  Download: BLOB→Disk    │
	   │  Upload: →BLOB          │
	   │  Replace: Block Legacy  │
	   │  Delete: Block Legacy   │
	   └────────┬────────┬───────┘
				│        │
		┌───────↓───┐    └──────────────┐
		│ EF Core   │                   │ File I/O
		│ DbContext │                   │ System
		└───────┬───┘                   │
				│                       │
		Database ↓                      ↓ Disk
	 ┌──────────────────┐    ┌────────────────────┐
	 │ ApplicationDocs  │    │ Physical Storage   │
	 │ IsLegacy=0→BLOB  │    │ uploads/{id}/{id}  │
	 │ IsLegacy=1→Path  │    │ (legacy files)     │
	 └──────────────────┘    └────────────────────┘
```

---

**Prepared for**: Phase 2 Data Migration  
**Status**: Investigation Complete  
**Audience**: Developers & DBAs  
**Last Updated**: 2025-01-15
