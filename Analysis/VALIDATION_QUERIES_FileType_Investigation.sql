-- =====================================================
-- VALIDATION QUERIES FOR FileType = 'Legacy' INVESTIGATION
-- =====================================================
-- These queries must be executed against MMGTDB2A to complete the investigation
-- Do NOT modify any data - these are SELECT queries only

USE MMGTDB2A
GO

-- =====================================================
-- 1. DATA SCOPE ANALYSIS
-- =====================================================

-- Query 1.1: Total count of records with FileType = 'Legacy'
SELECT 
	'Total FileType=Legacy Records' as Metric,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GO

-- Query 1.2: Count of unique applications affected
SELECT 
	'Unique Applications with FileType=Legacy' as Metric,
	COUNT(DISTINCT ApplicationId) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GO

-- Query 1.3: Verify all FileType='Legacy' records have IsLegacy=1
SELECT 
	'FileType=Legacy WITH IsLegacy=1' as Category,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy' AND IsLegacy = 1
UNION ALL
SELECT 
	'FileType=Legacy WITH IsLegacy=0' as Category,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy' AND IsLegacy = 0
GO

-- Query 1.4: Verify all FileType='Legacy' records are disk-based
SELECT 
	'FileType=Legacy WITH FileContent=NULL' as Category,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy' AND FileContent IS NULL
UNION ALL
SELECT 
	'FileType=Legacy WITH FileContent NOT NULL' as Category,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy' AND FileContent IS NOT NULL
GO

-- Query 1.5: Date range of affected records
SELECT 
	'Earliest FileType=Legacy Record' as Metric,
	MIN(CreatedAt) as Value
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
UNION ALL
SELECT 
	'Latest FileType=Legacy Record' as Metric,
	MAX(CreatedAt) as Value
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
UNION ALL
SELECT 
	'Total FileType=Legacy Records' as Metric,
	COUNT(*) as Value
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GO

-- =====================================================
-- 2. MIME TYPE VALIDATION
-- =====================================================

-- Query 2.1: Breakdown of file extensions for FileType='Legacy' records
SELECT 
	CASE 
		WHEN FileName LIKE '%.pdf' THEN '.pdf'
		WHEN FileName LIKE '%.txt' THEN '.txt'
		WHEN FileName LIKE '%.doc' OR FileName LIKE '%.docx' THEN '.doc/.docx'
		WHEN FileName LIKE '%.xls' OR FileName LIKE '%.xlsx' THEN '.xls/.xlsx'
		WHEN FileName LIKE '%.jpg' OR FileName LIKE '%.jpeg' THEN '.jpg/.jpeg'
		WHEN FileName LIKE '%.png' THEN '.png'
		WHEN FileName LIKE '%.gif' THEN '.gif'
		ELSE 'OTHER'
	END as FileExtension,
	COUNT(*) as Count,
	COUNT(*) * 100.0 / SUM(COUNT(*)) OVER () as Percentage
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GROUP BY 
	CASE 
		WHEN FileName LIKE '%.pdf' THEN '.pdf'
		WHEN FileName LIKE '%.txt' THEN '.txt'
		WHEN FileName LIKE '%.doc' OR FileName LIKE '%.docx' THEN '.doc/.docx'
		WHEN FileName LIKE '%.xls' OR FileName LIKE '%.xlsx' THEN '.xls/.xlsx'
		WHEN FileName LIKE '%.jpg' OR FileName LIKE '%.jpeg' THEN '.jpg/.jpeg'
		WHEN FileName LIKE '%.png' THEN '.png'
		WHEN FileName LIKE '%.gif' THEN '.gif'
		ELSE 'OTHER'
	END
ORDER BY Count DESC
GO

-- Query 2.2: Breakdown of FilePath extensions for FileType='Legacy' records
SELECT 
	CASE 
		WHEN FilePath LIKE '%.pdf' THEN '.pdf'
		WHEN FilePath LIKE '%.txt' THEN '.txt'
		WHEN FilePath LIKE '%.doc' OR FilePath LIKE '%.docx' THEN '.doc/.docx'
		WHEN FilePath LIKE '%.xls' OR FilePath LIKE '%.xlsx' THEN '.xls/.xlsx'
		WHEN FilePath LIKE '%.jpg' OR FilePath LIKE '%.jpeg' THEN '.jpg/.jpeg'
		WHEN FilePath LIKE '%.png' THEN '.png'
		WHEN FilePath LIKE '%.gif' THEN '.gif'
		ELSE 'NO EXTENSION IN PATH'
	END as PathExtension,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GROUP BY 
	CASE 
		WHEN FilePath LIKE '%.pdf' THEN '.pdf'
		WHEN FilePath LIKE '%.txt' THEN '.txt'
		WHEN FilePath LIKE '%.doc' OR FilePath LIKE '%.docx' THEN '.doc/.docx'
		WHEN FilePath LIKE '%.xls' OR FilePath LIKE '%.xlsx' THEN '.xls/.xlsx'
		WHEN FilePath LIKE '%.jpg' OR FilePath LIKE '%.jpeg' THEN '.jpg/.jpeg'
		WHEN FilePath LIKE '%.png' THEN '.png'
		WHEN FilePath LIKE '%.gif' THEN '.gif'
		ELSE 'NO EXTENSION IN PATH'
	END
ORDER BY Count DESC
GO

-- Query 2.3: Sample records to verify content
SELECT TOP 10
	Id,
	FileName,
	FilePath,
	FileSize,
	FileType,
	IsLegacy,
	CreatedAt
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
ORDER BY CreatedAt DESC
GO

-- =====================================================
-- 3. INVALID FileType SCAN
-- =====================================================

-- Query 3.1: Count of NULL, empty, or invalid MIME type FileType values
SELECT 
	CASE 
		WHEN FileType IS NULL THEN 'NULL'
		WHEN FileType = '' THEN 'EMPTY STRING'
		WHEN FileType NOT LIKE '%/%' THEN 'INVALID (no slash)'
		ELSE 'VALID MIME TYPE'
	END as FileTypeCategory,
	COUNT(*) as Count
FROM ApplicationDocuments
GROUP BY 
	CASE 
		WHEN FileType IS NULL THEN 'NULL'
		WHEN FileType = '' THEN 'EMPTY STRING'
		WHEN FileType NOT LIKE '%/%' THEN 'INVALID (no slash)'
		ELSE 'VALID MIME TYPE'
	END
GO

-- Query 3.2: List of all distinct invalid FileType values
SELECT DISTINCT
	FileType as InvalidFileType,
	COUNT(*) as Occurrences
FROM ApplicationDocuments
WHERE 
	FileType IS NULL 
	OR FileType = '' 
	OR FileType NOT LIKE '%/%'
GROUP BY FileType
ORDER BY Occurrences DESC
GO

-- Query 3.3: Detailed list of all distinct FileType values currently in database
SELECT DISTINCT
	FileType,
	COUNT(*) as RecordCount,
	CASE 
		WHEN FileType LIKE '%/%' THEN 'VALID MIME TYPE'
		ELSE 'INVALID'
	END as Validation
FROM ApplicationDocuments
GROUP BY FileType
ORDER BY RecordCount DESC
GO

-- =====================================================
-- 4. MIGRATION SOURCE INVESTIGATION
-- =====================================================

-- Query 4.1: Check if IsLegacy pattern correlates with FilePath presence
SELECT 
	CASE 
		WHEN FileType = 'Legacy' AND IsLegacy = 1 AND FileContent IS NULL AND FilePath IS NOT NULL
			THEN 'Full Legacy Pattern (FileType=Legacy + IsLegacy=1 + FileContent=NULL + FilePath!=NULL)'
		WHEN FileType = 'Legacy' AND IsLegacy = 1 AND FileContent IS NULL AND FilePath IS NULL
			THEN 'Partial Legacy (FileType=Legacy + IsLegacy=1 + FileContent=NULL + FilePath=NULL)'
		ELSE 'Other'
	END as Pattern,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
GROUP BY 
	CASE 
		WHEN FileType = 'Legacy' AND IsLegacy = 1 AND FileContent IS NULL AND FilePath IS NOT NULL
			THEN 'Full Legacy Pattern (FileType=Legacy + IsLegacy=1 + FileContent=NULL + FilePath!=NULL)'
		WHEN FileType = 'Legacy' AND IsLegacy = 1 AND FileContent IS NULL AND FilePath IS NULL
			THEN 'Partial Legacy (FileType=Legacy + IsLegacy=1 + FileContent=NULL + FilePath=NULL)'
		ELSE 'Other'
	END
GO

-- Query 4.2: Check migration history for clues
SELECT 
	MigrationId,
	ProductVersion
FROM __EFMigrationsHistory
WHERE MigrationId LIKE '%Legacy%' OR MigrationId LIKE '%Document%' OR MigrationId LIKE '%File%'
ORDER BY MigrationId
GO

-- Query 4.3: Check for any records where FileType='Legacy' but stored differently
SELECT 
	DISTINCT
	FileType,
	IsLegacy,
	CASE WHEN FileContent IS NULL THEN 'NULL' ELSE 'HAS CONTENT' END as BlobStatus,
	CASE WHEN FilePath IS NULL THEN 'NULL' ELSE 'HAS PATH' END as PathStatus,
	COUNT(*) as Count
FROM ApplicationDocuments
WHERE FileType = 'Legacy' OR FileType LIKE '%legacy%' OR FileType LIKE '%Legacy%'
GROUP BY FileType, IsLegacy, FileContent, FilePath
GO

-- =====================================================
-- 5. COMPREHENSIVE SUMMARY
-- =====================================================

-- Query 5.1: Complete summary report
SELECT 
	'Total Records in ApplicationDocuments' as Metric,
	COUNT(*) as Value
FROM ApplicationDocuments
UNION ALL
SELECT 
	'Records with FileType=Legacy',
	COUNT(*)
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
UNION ALL
SELECT 
	'Records with FileType=Legacy AND IsLegacy=1',
	COUNT(*)
FROM ApplicationDocuments
WHERE FileType = 'Legacy' AND IsLegacy = 1
UNION ALL
SELECT 
	'Records with FileType=Legacy AND FileContent IS NULL',
	COUNT(*)
FROM ApplicationDocuments
WHERE FileType = 'Legacy' AND FileContent IS NULL
UNION ALL
SELECT 
	'Unique Applications with FileType=Legacy',
	COUNT(DISTINCT ApplicationId)
FROM ApplicationDocuments
WHERE FileType = 'Legacy'
UNION ALL
SELECT 
	'Records with NULL FileType',
	COUNT(*)
FROM ApplicationDocuments
WHERE FileType IS NULL
UNION ALL
SELECT 
	'Records with EMPTY FileType',
	COUNT(*)
FROM ApplicationDocuments
WHERE FileType = ''
UNION ALL
SELECT 
	'Records with FileType not containing slash (invalid)',
	COUNT(*)
FROM ApplicationDocuments
WHERE FileType NOT LIKE '%/%' AND FileType IS NOT NULL AND FileType != ''
UNION ALL
SELECT 
	'Records with valid MIME type (contains slash)',
	COUNT(*)
FROM ApplicationDocuments
WHERE FileType LIKE '%/%'
GO

-- Query 5.2: File type distribution
SELECT 
	'Distribution of FileType values:' as Report,
	'' as Blank
UNION ALL
SELECT 
	'FileType: ' + ISNULL(FileType, 'NULL'),
	CAST(COUNT(*) as VARCHAR) + ' records'
FROM ApplicationDocuments
GROUP BY FileType
ORDER BY COUNT(*) DESC
GO
