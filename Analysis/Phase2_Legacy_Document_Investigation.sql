-- ============================================================================
-- Phase 2: Legacy Document Handling Investigation
-- ============================================================================
-- This script investigates how legacy (disk-based) documents are stored,
-- identified, and accessed in the current database schema.
-- 
-- CONSTRAINT: Investigation only - NO data modifications
-- ============================================================================

-- 1. List ALL DocumentTypes (with counts)
-- PURPOSE: Verify all document categories, identify if there's a legacy category
-- =============================================================================
SELECT 
	dt.Id,
	dt.Code,
	dt.Name,
	dt.Description,
	dt.IsMandatory,
	dt.DisplayOrder,
	dt.MaxFileSizeKB,
	dt.IsActive,
	COUNT(ad.Id) as DocumentCount
FROM DocumentTypes dt
LEFT JOIN ApplicationDocuments ad ON dt.Id = ad.DocumentTypeId
GROUP BY dt.Id, dt.Code, dt.Name, dt.Description, dt.IsMandatory, dt.DisplayOrder, dt.MaxFileSizeKB, dt.IsActive
ORDER BY dt.DisplayOrder;

-- 2. Statistics on Legacy vs. New Documents
-- PURPOSE: Identify how many applications have legacy documents on disk
-- =============================================================================
SELECT 
	'Total Documents' as Metric,
	COUNT(*) as Count
FROM ApplicationDocuments
UNION ALL
SELECT 
	'Legacy Documents (IsLegacy=1)',
	COUNT(*) 
FROM ApplicationDocuments 
WHERE IsLegacy = 1
UNION ALL
SELECT 
	'BLOB Documents (FileContent NOT NULL)',
	COUNT(*) 
FROM ApplicationDocuments 
WHERE FileContent IS NOT NULL
UNION ALL
SELECT 
	'Disk Documents (FilePath NOT NULL)',
	COUNT(*) 
FROM ApplicationDocuments 
WHERE FilePath IS NOT NULL
UNION ALL
SELECT 
	'Documents with IsLegacy=1 AND FilePath IS NOT NULL',
	COUNT(*) 
FROM ApplicationDocuments 
WHERE IsLegacy = 1 AND FilePath IS NOT NULL
UNION ALL
SELECT 
	'Applications with Legacy Documents',
	COUNT(DISTINCT ApplicationId) 
FROM ApplicationDocuments 
WHERE IsLegacy = 1;

-- 3. Detailed Legacy Document Analysis
-- PURPOSE: Verify each legacy document has valid references and paths
-- =============================================================================
SELECT TOP 50
	ad.Id as DocumentId,
	ad.ApplicationId,
	ad.FileName,
	ad.FilePath,
	ad.FileSize,
	ad.IsLegacy,
	ad.LegacyDocumentType,
	ad.DocumentTypeId,
	dt.Code as DocumentTypeCode,
	dt.Name as DocumentTypeName,
	ad.FileType,
	ad.ReviewStatus,
	ad.CreatedAt,
	CASE 
		WHEN ad.IsLegacy = 1 AND ad.FilePath IS NOT NULL AND ad.FileContent IS NULL THEN 'Valid Legacy (Disk)'
		WHEN ad.IsLegacy = 1 AND ad.FilePath IS NOT NULL AND ad.FileContent IS NOT NULL THEN 'Hybrid (Disk + Blob)'
		WHEN ad.IsLegacy = 1 AND ad.FilePath IS NULL AND ad.FileContent IS NOT NULL THEN 'New BLOB (IsLegacy mismatch)'
		WHEN ad.IsLegacy = 0 AND ad.FilePath IS NULL AND ad.FileContent IS NOT NULL THEN 'Valid BLOB'
		WHEN ad.IsLegacy = 0 AND ad.FilePath IS NOT NULL THEN 'Potential Orphan (Disk ref without legacy flag)'
		ELSE 'Unknown State'
	END as StorageStatus
FROM ApplicationDocuments ad
LEFT JOIN DocumentTypes dt ON ad.DocumentTypeId = dt.Id
WHERE ad.IsLegacy = 1
ORDER BY ad.CreatedAt DESC;

-- 4. Check for Inconsistencies
-- PURPOSE: Identify documents that may have storage/legacy mismatches
-- =============================================================================
SELECT 
	COUNT(*) as InconsistencyCount,
	'Legacy flag set but no FilePath' as Issue
FROM ApplicationDocuments 
WHERE IsLegacy = 1 AND FilePath IS NULL
UNION ALL
SELECT 
	COUNT(*),
	'FilePath exists but IsLegacy=0' 
FROM ApplicationDocuments 
WHERE IsLegacy = 0 AND FilePath IS NOT NULL
UNION ALL
SELECT 
	COUNT(*),
	'DocumentTypeId NULL on legacy documents' 
FROM ApplicationDocuments 
WHERE IsLegacy = 1 AND DocumentTypeId IS NULL
UNION ALL
SELECT 
	COUNT(*),
	'ApplicationId NULL (orphan records)' 
FROM ApplicationDocuments 
WHERE ApplicationId IS NULL;

-- 5. Applications with Mixed Storage (Legacy + New)
-- PURPOSE: Verify applications can have both legacy and new documents
-- =============================================================================
SELECT 
	a.Id as ApplicationId,
	a.ReferenceNumber,
	COUNT(CASE WHEN ad.IsLegacy = 1 THEN 1 END) as LegacyDocumentCount,
	COUNT(CASE WHEN ad.IsLegacy = 0 AND ad.FileContent IS NOT NULL THEN 1 END) as BlobDocumentCount,
	COUNT(ad.Id) as TotalDocuments,
	a.Status,
	a.CreatedAt
FROM Applications a
LEFT JOIN ApplicationDocuments ad ON a.Id = ad.ApplicationId
WHERE COUNT(ad.Id) > 0
GROUP BY a.Id, a.ReferenceNumber, a.Status, a.CreatedAt
HAVING COUNT(CASE WHEN ad.IsLegacy = 1 THEN 1 END) > 0 AND COUNT(CASE WHEN ad.IsLegacy = 0 AND ad.FileContent IS NOT NULL THEN 1 END) > 0
ORDER BY a.CreatedAt DESC;

-- 6. document Storage Type Distribution
-- PURPOSE: Show the breakdown of document storage methods
-- =============================================================================
SELECT 
	CASE 
		WHEN IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NULL THEN 'Disk (Legacy)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NULL THEN 'BLOB (New)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NOT NULL THEN 'BLOB + Disk Path'
		WHEN IsLegacy = 1 AND FileContent IS NOT NULL THEN 'Legacy Marked w/ BLOB Content'
		ELSE 'Other'
	END as StorageType,
	COUNT(*) as DocumentCount,
	CAST(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM ApplicationDocuments) AS DECIMAL(5,2)) as Percentage
FROM ApplicationDocuments
GROUP BY 
	CASE 
		WHEN IsLegacy = 1 AND FilePath IS NOT NULL AND FileContent IS NULL THEN 'Disk (Legacy)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NULL THEN 'BLOB (New)'
		WHEN IsLegacy = 0 AND FileContent IS NOT NULL AND FilePath IS NOT NULL THEN 'BLOB + Disk Path'
		WHEN IsLegacy = 1 AND FileContent IS NOT NULL THEN 'Legacy Marked w/ BLOB Content'
		ELSE 'Other'
	END
ORDER BY DocumentCount DESC;

-- 7. LegacyDocumentType Field Analysis
-- PURPOSE: Check if LegacyDocumentType is populated or if DocumentTypeId is being used
-- =============================================================================
SELECT 
	'LegacyDocumentType NOT NULL' as Metric,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE LegacyDocumentType IS NOT NULL
UNION ALL
SELECT 
	'LegacyDocumentType IS NULL',
	COUNT(*) 
FROM ApplicationDocuments
WHERE LegacyDocumentType IS NULL
UNION ALL
SELECT 
	'LegacyDocumentType populated but DocumentTypeId also set',
	COUNT(*) 
FROM ApplicationDocuments
WHERE LegacyDocumentType IS NOT NULL AND DocumentTypeId IS NOT NULL;

-- 8. File Extension Distribution
-- PURPOSE: Verify file type consistency, confirm PDF enforcement
-- =============================================================================
SELECT 
	FileType as MimeType,
	COUNT(*) as DocumentCount
FROM ApplicationDocuments
GROUP BY FileType
ORDER BY DocumentCount DESC;

-- 9. Review Status of Legacy Documents
-- PURPOSE: Check if legacy documents can be reviewed and their workflow status
-- =============================================================================
SELECT 
	ReviewStatus,
	COUNT(*) as LegacyDocumentCount,
	COUNT(ReviewedBy) as ReviewedCount,
	COUNT(ReviewComments) as CommentsCount
FROM ApplicationDocuments
WHERE IsLegacy = 1
GROUP BY ReviewStatus
ORDER BY LegacyDocumentCount DESC;

-- 10. File Size Distribution for Legacy Documents
-- PURPOSE: Verify file sizes are reasonable and enforce 300KB constraint
-- =============================================================================
SELECT 
	CASE 
		WHEN FileSize = 0 THEN 'Empty (0 bytes)'
		WHEN FileSize < 1024 THEN '< 1 KB'
		WHEN FileSize < 102400 THEN '1-100 KB'
		WHEN FileSize < 307200 THEN '100-300 KB'
		WHEN FileSize >= 307200 THEN '> 300 KB'
	END as FileSizeRange,
	COUNT(*) as LegacyDocumentCount
FROM ApplicationDocuments
WHERE IsLegacy = 1
GROUP BY 
	CASE 
		WHEN FileSize = 0 THEN 'Empty (0 bytes)'
		WHEN FileSize < 1024 THEN '< 1 KB'
		WHEN FileSize < 102400 THEN '1-100 KB'
		WHEN FileSize < 307200 THEN '100-300 KB'
		WHEN FileSize >= 307200 THEN '> 300 KB'
	END
ORDER BY 
	CASE 
		WHEN FileSize = 0 THEN 0
		WHEN FileSize < 1024 THEN 1
		WHEN FileSize < 102400 THEN 2
		WHEN FileSize < 307200 THEN 3
		WHEN FileSize >= 307200 THEN 4
	END;

-- 11. Check ApplicationDocument-Application Referential Integrity
-- PURPOSE: Verify all documents reference valid applications
-- =============================================================================
SELECT 
	COUNT(*) as OrphanDocumentCount,
	'Documents with invalid ApplicationId (not in Applications table)' as Issue
FROM ApplicationDocuments ad
WHERE NOT EXISTS (SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId);

-- 12. Check ApplicationDocument-DocumentType Referential Integrity
-- PURPOSE: Verify all documents reference valid document types
-- =============================================================================
SELECT 
	COUNT(*) as InvalidDocumentCount,
	'Documents with invalid DocumentTypeId (not in DocumentTypes table)' as Issue
FROM ApplicationDocuments ad
WHERE ad.DocumentTypeId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM DocumentTypes dt WHERE dt.Id = ad.DocumentTypeId);

-- ============================================================================
-- SUMMARY STATISTICS
-- ============================================================================
SELECT 
	'Total Applications' as Metric,
	COUNT(DISTINCT Id) as Value
FROM Applications
UNION ALL
SELECT 
	'Total Documents',
	COUNT(*) 
FROM ApplicationDocuments
UNION ALL
SELECT 
	'Legacy Documents (Disk-based)',
	COUNT(*) 
FROM ApplicationDocuments WHERE IsLegacy = 1
UNION ALL
SELECT 
	'New Documents (BLOB-based)',
	COUNT(*) 
FROM ApplicationDocuments WHERE IsLegacy = 0 AND FileContent IS NOT NULL
UNION ALL
SELECT 
	'Document Types Defined',
	COUNT(*) 
FROM DocumentTypes
UNION ALL
SELECT 
	'Active Document Types',
	COUNT(*) 
FROM DocumentTypes WHERE IsActive = 1
ORDER BY Value DESC;
