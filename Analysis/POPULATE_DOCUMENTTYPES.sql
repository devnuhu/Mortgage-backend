-- ============================================================================
-- POPULATE DOCUMENTTYPES IN MMGTDB2A
-- ============================================================================
-- This script populates DocumentTypes table in MMGTDB2A from MMGTDB2ACOPY
-- Constraints are disabled during insert, then re-enabled

USE MMGTDB2A;
GO

PRINT '===== POPULATING DOCUMENTTYPES =====';
PRINT '';

-- ============================================================================
-- STEP 1: Disable constraints on tables that reference DocumentTypes
-- ============================================================================

PRINT 'STEP 1: Disabling constraints...';

ALTER TABLE ApplicationDocuments NOCHECK CONSTRAINT ALL;
ALTER TABLE DocumentTypeAuditLogs NOCHECK CONSTRAINT ALL;
ALTER TABLE DocumentTypes NOCHECK CONSTRAINT ALL;

PRINT 'Constraints disabled.';
PRINT '';

-- ============================================================================
-- STEP 2: Clear existing DocumentTypes (optional - comment out if you want to keep existing)
-- ============================================================================

PRINT 'STEP 2: Clearing existing DocumentTypes...';

DELETE FROM DocumentTypes;

PRINT 'DocumentTypes cleared.';
PRINT '';

-- ============================================================================
-- STEP 3: Insert DocumentTypes from MMGTDB2ACOPY
-- ============================================================================

PRINT 'STEP 3: Inserting DocumentTypes from MMGTDB2ACOPY...';

INSERT INTO DocumentTypes
(
	Id,
	Code,
	Name,
	Description,
	IsMandatory,
	DisplayOrder,
	MaxFileSizeKB,
	IsActive,
	CreatedAt,
	CreatedBy,
	ModifiedAt,
	ModifiedBy
)
SELECT
	Id,
	Code,
	Name,
	Description,
	IsMandatory,
	DisplayOrder,
	MaxFileSizeKB,
	IsActive,
	CreatedAt,
	CreatedBy,
	ModifiedAt,
	ModifiedBy
FROM MMGTDB2ACOPY.dbo.DocumentTypes;

PRINT 'DocumentTypes inserted: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows.';
PRINT '';

-- ============================================================================
-- STEP 4: Verify insert
-- ============================================================================

PRINT 'STEP 4: Verifying DocumentTypes...';

SELECT 
	COUNT(*) AS TotalCount,
	SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) AS ActiveCount
FROM DocumentTypes;

SELECT TOP 10 
	Id, Code, Name, IsActive, DisplayOrder 
FROM DocumentTypes 
ORDER BY DisplayOrder;

PRINT '';

-- ============================================================================
-- STEP 5: Re-enable constraints
-- ============================================================================

PRINT 'STEP 5: Re-enabling constraints...';

BEGIN TRY
	ALTER TABLE DocumentTypes WITH CHECK CHECK CONSTRAINT ALL;
	ALTER TABLE ApplicationDocuments WITH CHECK CHECK CONSTRAINT ALL;
	ALTER TABLE DocumentTypeAuditLogs WITH CHECK CHECK CONSTRAINT ALL;

	PRINT 'All constraints re-enabled successfully.';
END TRY
BEGIN CATCH
	PRINT 'Warning: Some constraints could not be re-enabled.';
	PRINT 'Error: ' + ERROR_MESSAGE();
END CATCH

PRINT '';
PRINT '===== DOCUMENTTYPES POPULATION COMPLETE =====';

GO
