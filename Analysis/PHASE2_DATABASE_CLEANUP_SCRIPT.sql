-- ============================================================================
-- PHASE 2: DATABASE CLEANUP - Legacy FilePath Root Migration
-- ============================================================================
-- Purpose: Correct legacy document FilePath values from old server root
--          to new server root for improved resilience.
--
-- Scope: ONLY legacy on-disk documents (IsLegacy=1, FileContent IS NULL)
-- Impact: 14,178 legacy documents (verified from earlier queries)
-- Database: MMGTDB2A
-- ============================================================================

USE MMGTDB2A;
GO

-- ============================================================================
-- STEP 1: PRE-EXECUTION VERIFICATION
-- Count and display affected records BEFORE any changes
-- ============================================================================
PRINT '========================================';
PRINT 'STEP 1: PRE-EXECUTION VERIFICATION';
PRINT '========================================';

SELECT 
	COUNT(*) AS AffectedRecordCount,
	'Legacy documents to be updated' AS Description
FROM ApplicationDocuments
WHERE 
	IsLegacy = 1 
	AND FileContent IS NULL
	AND FilePath IS NOT NULL
	AND FilePath LIKE 'C:\inetpub\wwwroot\backend\AccessMortgage\wwwroot%';

GO

-- Show sample records (first 10) that will be updated
PRINT '';
PRINT 'Sample Records (First 10) to be Updated:';
PRINT '';

SELECT TOP 10
	Id,
	ApplicationId,
	FileName,
	FilePath AS OldPath,
	REPLACE(FilePath, 'C:\inetpub\wwwroot\backend\AccessMortgage\wwwroot', 'C:\inetpub\wwwroot\Mortgage\wwwroot') AS NewPath,
	IsLegacy,
	FileSize,
	CreatedAt
FROM ApplicationDocuments
WHERE 
	IsLegacy = 1 
	AND FileContent IS NULL
	AND FilePath IS NOT NULL
	AND FilePath LIKE 'C:\inetpub\wwwroot\backend\AccessMortgage\wwwroot%'
ORDER BY CreatedAt DESC;

GO

-- ============================================================================
-- STEP 2: EXECUTE CLEANUP
-- Replace old server root with new server root in FilePath
-- ============================================================================
PRINT '';
PRINT '========================================';
PRINT 'STEP 2: EXECUTING CLEANUP UPDATE';
PRINT '========================================';
PRINT 'Updating FilePath values...';
PRINT '';

UPDATE ApplicationDocuments
SET FilePath = REPLACE(
	FilePath,
	'C:\inetpub\wwwroot\backend\AccessMortgage\wwwroot',
	'C:\inetpub\wwwroot\Mortgage\wwwroot'
)
WHERE 
	IsLegacy = 1 
	AND FileContent IS NULL
	AND FilePath IS NOT NULL
	AND FilePath LIKE 'C:\inetpub\wwwroot\backend\AccessMortgage\wwwroot%';

DECLARE @RowsAffected INT = @@ROWCOUNT;
PRINT 'Update completed successfully.';
PRINT 'Rows affected: ' + CAST(@RowsAffected AS VARCHAR(10));
PRINT '';

GO

-- ============================================================================
-- STEP 3: POST-EXECUTION VERIFICATION
-- Verify the cleanup completed successfully
-- ============================================================================
PRINT '';
PRINT '========================================';
PRINT 'STEP 3: POST-EXECUTION VERIFICATION';
PRINT '========================================';

-- Verify no old-server references remain in legacy documents
SELECT 
	COUNT(*) AS RemainingOldPathCount,
	'Should be 0 if cleanup successful' AS Description
FROM ApplicationDocuments
WHERE 
	IsLegacy = 1 
	AND FileContent IS NULL
	AND FilePath LIKE 'C:\inetpub\wwwroot\backend\AccessMortgage\wwwroot%';

GO

-- Show sample records (first 10) AFTER update
PRINT '';
PRINT 'Sample Records (First 10) After Cleanup:';
PRINT '';

SELECT TOP 10
	Id,
	ApplicationId,
	FileName,
	FilePath AS UpdatedPath,
	IsLegacy,
	FileContent,
	FileSize,
	CreatedAt
FROM ApplicationDocuments
WHERE 
	IsLegacy = 1 
	AND FileContent IS NULL
	AND FilePath LIKE 'C:\inetpub\wwwroot\Mortgage\wwwroot%'
ORDER BY CreatedAt DESC;

GO

-- Overall summary
PRINT '';
PRINT '========================================';
PRINT 'CLEANUP SUMMARY';
PRINT '========================================';

SELECT 
	COUNT(*) AS TotalLegacyDocuments,
	SUM(CASE WHEN FilePath LIKE 'C:\inetpub\wwwroot\Mortgage\wwwroot%' THEN 1 ELSE 0 END) AS UpdatedToNewRoot,
	SUM(CASE WHEN FilePath LIKE 'C:\inetpub\wwwroot\backend\AccessMortgage\wwwroot%' THEN 1 ELSE 0 END) AS StillOnOldRoot
FROM ApplicationDocuments
WHERE IsLegacy = 1 AND FileContent IS NULL AND FilePath IS NOT NULL;

GO

PRINT '';
PRINT '========================================';
PRINT 'Cleanup verification complete.';
PRINT '========================================';
