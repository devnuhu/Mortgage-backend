-- BACKUP EXPORT: Records with FileType='Legacy' (before cleanup)
-- This is a SELECT-only export for audit and rollback reference
-- Executed before Phase 2 database update

SELECT 
	COUNT(*) AS TotalRecordsWithLegacy,
	CAST(CONVERT(VARCHAR, GETUTCDATE(), 121) AS VARCHAR) AS BackupTimestamp
FROM ApplicationDocuments
WHERE FileType = 'Legacy';

-- Detailed backup of all affected records
SELECT 
	Id,
	ApplicationId,
	FileName,
	FilePath,
	FileType,
	FileSize,
	IsLegacy,
	FileContent,
	LegacyDocumentType,
	ReviewStatus,
	Version,
	IsLatestVersion,
	CreatedAt,
	UpdatedAt
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
ORDER BY ApplicationId, Id;

-- Summary by unique ApplicationId
SELECT 
	ApplicationId,
	COUNT(*) AS DocumentCountPerApp,
	COUNT(DISTINCT FileName) AS UniqueFileNames
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GROUP BY ApplicationId
ORDER BY COUNT(*) DESC;
