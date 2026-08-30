-- ============================================================================
-- MMGTDBCOPY → MMGTDB2A DATA MIGRATION SCRIPTS
-- ============================================================================
-- Migration Strategy: Copy data from MMGTDBCOPY to MMGTDB2A (5 core tables)
-- New columns in MMGTDB2A will use sensible defaults
-- Execution Order: DocumentTypes → Users → PMICompanies → Applications → ApplicationDocuments → ApplicationReviewHistory
-- ============================================================================

-- ============================================================================
-- STEP 0: POPULATE DOCUMENTTYPES (if needed)
-- ============================================================================

USE MMGTDB2A;
GO

PRINT '===== STEP 0: POPULATE DOCUMENTTYPES =====';
PRINT '';

DECLARE @DocTypeCount INT = (SELECT COUNT(*) FROM DocumentTypes);

IF @DocTypeCount = 0
BEGIN
    PRINT 'DocumentTypes is empty. Populating from MMGTDB2ACOPY...';

    ALTER TABLE ApplicationDocuments NOCHECK CONSTRAINT ALL;
    ALTER TABLE DocumentTypeAuditLogs NOCHECK CONSTRAINT ALL;

    INSERT INTO DocumentTypes
    (
        Id, Code, Name, Description, IsMandatory, DisplayOrder,
        MaxFileSizeKB, IsActive, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
    )
    SELECT
        Id, Code, Name, Description, IsMandatory, DisplayOrder,
        MaxFileSizeKB, IsActive, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy
    FROM MMGTDB2ACOPY.dbo.DocumentTypes;

    PRINT 'DocumentTypes populated: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows inserted.';
END
ELSE
BEGIN
    PRINT 'DocumentTypes already has ' + CAST(@DocTypeCount AS VARCHAR(10)) + ' rows. Proceeding...';
END

PRINT '';

-- ============================================================================
-- STEP 1: PRE-MIGRATION VALIDATION (Run this first on MMGTDBCOPY)
-- ============================================================================

USE MMGTDBCOPY;
GO

PRINT '===== STEP 1: PRE-MIGRATION VALIDATION =====';
PRINT '';

-- Check 1: Verify table row counts
PRINT 'TABLE ROW COUNTS:';
SELECT 'Users' AS TableName, COUNT(*) AS [RowCnt] FROM Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory;

-- Check 2: Orphaned ApplicationDocuments (FK integrity)
PRINT '';
PRINT 'ORPHANED ApplicationDocuments (should be 0):';
SELECT COUNT(*) AS OrphanedCount
FROM ApplicationDocuments ad
WHERE ad.ApplicationId NOT IN (SELECT Id FROM Applications);

-- Check 3: Orphaned ApplicationReviewHistory
PRINT '';
PRINT 'ORPHANED ApplicationReviewHistory (should be 0):';
SELECT COUNT(*) AS OrphanedCount
FROM ApplicationReviewHistory arh
WHERE arh.ApplicationId NOT IN (SELECT Id FROM Applications)
   OR arh.ReviewedById NOT IN (SELECT Id FROM Users);

-- Check 4: Applications with invalid Users
PRINT '';
PRINT 'Applications with invalid User FK (should be 0):';
SELECT COUNT(*) AS InvalidCount
FROM Applications a
WHERE a.UserId NOT IN (SELECT Id FROM Users);

-- Check 5: Applications with invalid PMICompanies
PRINT '';
PRINT 'Applications with invalid PMICompany FK (should be 0):';
SELECT COUNT(*) AS InvalidCount
FROM Applications a
WHERE a.PmiCompanyId NOT IN (SELECT Id FROM PMICompanies);

-- Check 6: Users with invalid PMICompanies
PRINT '';
PRINT 'Users with invalid PMICompany FK (should be 0):';
SELECT COUNT(*) AS InvalidCount
FROM Users u
WHERE u.PMICompanyId IS NOT NULL
  AND u.PMICompanyId NOT IN (SELECT Id FROM PMICompanies);

-- Check 7: Duplicate RsaPin in active statuses
PRINT '';
PRINT 'Duplicate RsaPin in active statuses (should be 0):';
SELECT RsaPin, COUNT(*) AS DupCount
FROM Applications
WHERE Status IN ('draft', 'submitted', 'under_review', 'returned', 'resubmitted')
GROUP BY RsaPin
HAVING COUNT(*) > 1;

-- Check 8: NULL FileSize in ApplicationDocuments
PRINT '';
PRINT 'ApplicationDocuments with NULL FileSize (should be 0):';
SELECT COUNT(*) AS NullFileSizeCount
FROM ApplicationDocuments
WHERE FileSize IS NULL;

-- Check 9: NULL FileName in ApplicationDocuments
PRINT '';
PRINT 'ApplicationDocuments with NULL FileName (should be 0):';
SELECT COUNT(*) AS NullFileNameCount
FROM ApplicationDocuments
WHERE FileName IS NULL OR FileName = '';

PRINT '';
PRINT '===== END PRE-MIGRATION VALIDATION =====';

GO

-- ============================================================================
-- STEP 1.5: VERIFY DOCUMENTTYPES EXIST (Run this before STEP 2)
-- ============================================================================

USE MMGTDB2A;
GO

PRINT '';
PRINT '===== VERIFYING DOCUMENTTYPES EXIST =====';

SELECT 'DocumentTypes count' AS Check_Name, COUNT(*) AS Result 
FROM DocumentTypes;

SELECT 'Active DocumentTypes count', COUNT(*) 
FROM DocumentTypes 
WHERE IsActive = 1;

SELECT 'DocumentType list:' AS Detail;
SELECT Id, Code, Name, IsActive, DisplayOrder 
FROM DocumentTypes 
ORDER BY DisplayOrder;

PRINT '';
PRINT 'IMPORTANT: If no DocumentTypes exist or are active, the migration will FAIL.';
PRINT 'You MUST populate DocumentTypes table before proceeding.';
PRINT '';

GO

USE MMGTDB2A;
GO

PRINT '===== BACKING UP REFERENCE DATA IN MMGTDB2A =====';

-- Create backup tables for reference data to preserve
IF OBJECT_ID('DocumentTypes_BACKUP', 'U') IS NOT NULL
	DROP TABLE DocumentTypes_BACKUP;
SELECT * INTO DocumentTypes_BACKUP FROM DocumentTypes;

IF OBJECT_ID('EscalationRules_BACKUP', 'U') IS NOT NULL
	DROP TABLE EscalationRules_BACKUP;
SELECT * INTO EscalationRules_BACKUP FROM EscalationRules;

IF OBJECT_ID('SystemConfigurations_BACKUP', 'U') IS NOT NULL
	DROP TABLE SystemConfigurations_BACKUP;
SELECT * INTO SystemConfigurations_BACKUP FROM SystemConfigurations;

PRINT 'Backup tables created:';
PRINT '  - DocumentTypes_BACKUP';
PRINT '  - EscalationRules_BACKUP';
PRINT '  - SystemConfigurations_BACKUP';

GO

-- ============================================================================
-- STEP 3: CLEAR EXISTING DATA IN MMGTDB2A (in dependency order)
-- ============================================================================

USE MMGTDB2A;
GO

PRINT '';
PRINT '===== CLEARING EXISTING DATA IN MMGTDB2A =====';

-- Disable ALL constraints on ALL tables
ALTER TABLE ReassignmentHistories NOCHECK CONSTRAINT ALL;
ALTER TABLE AssignmentEscalations NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationAssignments NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationDocumentHistory NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationDocuments NOCHECK CONSTRAINT ALL;
ALTER TABLE ApplicationReviewHistory NOCHECK CONSTRAINT ALL;
ALTER TABLE Applications NOCHECK CONSTRAINT ALL;
ALTER TABLE Users NOCHECK CONSTRAINT ALL;
ALTER TABLE PMICompanies NOCHECK CONSTRAINT ALL;
ALTER TABLE ReviewerAvailabilities NOCHECK CONSTRAINT ALL;
ALTER TABLE ReviewerPerformanceMetrics NOCHECK CONSTRAINT ALL;
ALTER TABLE DocumentTypeAuditLogs NOCHECK CONSTRAINT ALL;
ALTER TABLE DocumentTypes NOCHECK CONSTRAINT ALL;
ALTER TABLE EscalationRules NOCHECK CONSTRAINT ALL;

-- Clear data in reverse dependency order (children first)
-- Start with tables that have no dependents
DELETE FROM ReassignmentHistories;
DELETE FROM AssignmentEscalations;
DELETE FROM ApplicationAssignments;
DELETE FROM ApplicationDocumentHistory;
DELETE FROM ApplicationDocuments;
DELETE FROM ApplicationReviewHistory;
DELETE FROM Applications;
DELETE FROM ReviewerAvailabilities;
DELETE FROM ReviewerPerformanceMetrics;
DELETE FROM DocumentTypeAuditLogs;

-- Tables with FK to Users must be cleared before Users
-- Clear temp rows in DocumentTypes and EscalationRules that reference old Users
DELETE FROM DocumentTypes WHERE CreatedBy NOT IN (SELECT Id FROM MMGTDBCOPY.dbo.Users);
DELETE FROM EscalationRules WHERE CreatedBy NOT IN (SELECT Id FROM MMGTDBCOPY.dbo.Users);

-- Now safe to delete Users and PMICompanies
DELETE FROM Users;
DELETE FROM PMICompanies;

PRINT 'Data cleared from:';
PRINT '  - ReassignmentHistories';
PRINT '  - AssignmentEscalations';
PRINT '  - ApplicationAssignments';
PRINT '  - ApplicationDocumentHistory';
PRINT '  - ApplicationDocuments';
PRINT '  - ApplicationReviewHistory';
PRINT '  - Applications';
PRINT '  - ReviewerAvailabilities';
PRINT '  - ReviewerPerformanceMetrics';
PRINT '  - DocumentTypeAuditLogs';
PRINT '  - DocumentTypes (removed entries with old Users)';
PRINT '  - EscalationRules (removed entries with old Users)';
PRINT '  - Users';
PRINT '  - PMICompanies';
PRINT '';
PRINT 'NOTE: SystemConfigurations reference data preserved';

GO

-- ============================================================================
-- STEP 4: MIGRATE DATA (in dependency order)
-- ============================================================================

USE MMGTDB2A;
GO

PRINT '';
PRINT '===== STARTING DATA MIGRATION =====';
PRINT '';

-- ============================================================================
-- MIGRATION 1: USERS
-- ============================================================================

PRINT 'MIGRATION 1: Copying Users from MMGTDBCOPY...';

INSERT INTO Users
(
	Id,
	Email,
	FirstName,
	LastName,
	Role,
	PasswordHash,
	IsFirstLogin,
	IsActive,
	CreatedAt,
	UpdatedAt,
	LastLoginAt,
	PMICompanyId,
	PMIEmail,
	FirstLoginTokenHash,
	FirstLoginTokenExpiresAt
)
SELECT
	Id,
	Email,
	FirstName,
	LastName,
	Role,
	PasswordHash,
	IsFirstLogin,
	IsActive,
	CreatedAt,
	UpdatedAt,
	LastLoginAt,
	PMICompanyId,
	PMIEmail,
	FirstLoginTokenHash,
	FirstLoginTokenExpiresAt
FROM MMGTDBCOPY.dbo.Users;

PRINT 'Users migration completed.';
PRINT '';

-- ============================================================================
-- MIGRATION 2: PMICOMPANIES
-- ============================================================================

PRINT 'MIGRATION 2: Copying PMICompanies from MMGTDBCOPY...';

INSERT INTO PMICompanies
(
	Id,
	PmiName,
	EmailAddress,
	ResourcePersonName,
	ResourcePersonEmail,
	PhoneNumber,
	Status,
	CreatedAt,
	UpdatedAt
)
SELECT
	Id,
	PmiName,
	EmailAddress,
	ResourcePersonName,
	ResourcePersonEmail,
	PhoneNumber,
	Status,
	CreatedAt,
	UpdatedAt
FROM MMGTDBCOPY.dbo.PMICompanies;

PRINT 'PMICompanies migration completed.';
PRINT '';

-- ============================================================================
-- MIGRATION 3: APPLICATIONS (with new column defaults)
-- ============================================================================

PRINT 'MIGRATION 3: Copying Applications from MMGTDBCOPY...';

INSERT INTO Applications
(
	Id,
	ApplicationNumber,
	Status,
	Priority,
	FirstName,
	MiddleName,
	LastName,
	RsaPin,
	EquityValue,
	SupplementaryValue,
	LoanAmount,
	PropertyValue,
	PmiRepName,
	PmiRepEmail,
	PmiCompanyName,
	CreatedAt,
	UpdatedAt,
	SubmittedAt,
	UserId,
	PmiCompanyId,
	-- New columns with defaults
	DocumentStorageType,
	MandatoryDocumentsComplete,
	CompletedAt,
	PhoneNumber
)
SELECT
	Id,
	ApplicationNumber,
	Status,
	Priority,
	FirstName,
	MiddleName,
	LastName,
	RsaPin,
	EquityValue,
	SupplementaryValue,
	LoanAmount,
	PropertyValue,
	PmiRepName,
	PmiRepEmail,
	PmiCompanyName,
	CreatedAt,
	UpdatedAt,
	SubmittedAt,
	UserId,
	PmiCompanyId,
	-- New columns with defaults
	'FileSystem' AS DocumentStorageType,           -- Legacy storage type
	0 AS MandatoryDocumentsComplete,               -- Will be recalculated post-migration
	CASE 
		WHEN Status IN ('approved', 'rejected') THEN UpdatedAt
		ELSE NULL
	END AS CompletedAt,
	NULL AS PhoneNumber                            -- Not in source
FROM MMGTDBCOPY.dbo.Applications;

PRINT 'Applications migration completed.';
PRINT '';

-- ============================================================================
-- MIGRATION 4: APPLICATIONDOCUMENTS (with fixed DocumentTypeId)
-- ============================================================================

PRINT 'MIGRATION 4: Copying ApplicationDocuments from MMGTDBCOPY...';

-- Using the identity_document DocumentTypeId
DECLARE @DocumentTypeId UNIQUEIDENTIFIER = '4EECC413-3B37-452B-88B8-5C5A9A108E29';

PRINT 'Using DocumentTypeId: ' + CAST(@DocumentTypeId AS VARCHAR(36)) + ' (identity_document)';

INSERT INTO ApplicationDocuments
(
	Id,
	FileName,
	FilePath,
	FileContent,
	FileType,
	DocumentTypeId,
	LegacyDocumentType,
	FileSize,
	Version,
	IsLatestVersion,
	ReviewStatus,
	ReviewComments,
	ReviewedBy,
	ReviewedAt,
	CreatedAt,
	UploadedBy,
	ApplicationId,
	IsLegacy
)
SELECT
	ad.Id,
	ad.FileName,
	ad.FilePath,
	NULL AS FileContent,
	ad.FileType,
	@DocumentTypeId AS DocumentTypeId,
	'identity_document' AS LegacyDocumentType,
	ad.FileSize,
	1 AS Version,
	1 AS IsLatestVersion,
	'pending' AS ReviewStatus,
	NULL AS ReviewComments,
	NULL AS ReviewedBy,
	NULL AS ReviewedAt,
	ad.CreatedAt,
	COALESCE(
		(SELECT TOP 1 arh.ReviewedById 
		 FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh 
		 WHERE arh.ApplicationId = ad.ApplicationId
		 ORDER BY arh.CreatedAt DESC),
		(SELECT a.UserId FROM MMGTDBCOPY.dbo.Applications a WHERE a.Id = ad.ApplicationId)
	) AS UploadedBy,
	ad.ApplicationId,
	1 AS IsLegacy
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad;

PRINT 'ApplicationDocuments migration completed. ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows inserted.';
PRINT '';

-- ============================================================================
-- MIGRATION 5: APPLICATIONREVIEWHISTORY
-- ============================================================================

PRINT 'MIGRATION 5: Copying ApplicationReviewHistory from MMGTDBCOPY...';

INSERT INTO ApplicationReviewHistory
(
	Id,
	Status,
	Comments,
	CreatedAt,
	ApplicationId,
	ReviewedById
)
SELECT
	Id,
	Status,
	Comments,
	CreatedAt,
	ApplicationId,
	ReviewedById
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory;

PRINT 'ApplicationReviewHistory migration completed.';
PRINT '';

-- ============================================================================
-- STEP 5: RE-ENABLE CONSTRAINTS
-- ============================================================================

PRINT 'Re-enabling constraints...';

BEGIN TRY
    ALTER TABLE Users WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE PMICompanies WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE Applications WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE ApplicationDocuments WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE ApplicationReviewHistory WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE DocumentTypes WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE EscalationRules WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE ReviewerAvailabilities WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE ReviewerPerformanceMetrics WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE ApplicationAssignments WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE ApplicationDocumentHistory WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE AssignmentEscalations WITH CHECK CHECK CONSTRAINT ALL;
    ALTER TABLE ReassignmentHistories WITH CHECK CHECK CONSTRAINT ALL;
    PRINT 'All constraints re-enabled successfully.';
END TRY
BEGIN CATCH
    PRINT 'Warning: Some constraints could not be re-enabled. Check FK integrity.';
    PRINT 'Error: ' + ERROR_MESSAGE();
    PRINT 'Ensure DocumentTypes exist and are properly linked.';
END CATCH

PRINT '';
PRINT '===== DATA MIGRATION COMPLETED =====';

GO

-- ============================================================================
-- STEP 6: POST-MIGRATION VALIDATION
-- ============================================================================

USE MMGTDB2A;
GO

PRINT '';
PRINT '===== POST-MIGRATION VALIDATION =====';
PRINT '';

-- Verify row counts
PRINT 'Row count comparison:';
SELECT 'Users' AS TableName, COUNT(*) AS [RowCnt] FROM Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory;
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory;

-- Verify FK integrity
PRINT '';
PRINT 'FK Integrity Check - Orphaned Applications.UserId (should be 0):';
SELECT COUNT(*) AS [Orphaned]
FROM Applications a
WHERE a.UserId NOT IN (SELECT Id FROM Users);

PRINT '';
PRINT 'FK Integrity Check - Orphaned Applications.PmiCompanyId (should be 0):';
SELECT COUNT(*) AS [Orphaned]
FROM Applications a
WHERE a.PmiCompanyId NOT IN (SELECT Id FROM PMICompanies);

PRINT '';
PRINT 'FK Integrity Check - Orphaned ApplicationDocuments.ApplicationId (should be 0):';
SELECT COUNT(*) AS [Orphaned]
FROM ApplicationDocuments ad
WHERE ad.ApplicationId NOT IN (SELECT Id FROM Applications);

PRINT '';
PRINT 'FK Integrity Check - Orphaned ApplicationReviewHistory constraints (should be 0):';
SELECT COUNT(*) AS [Orphaned]
FROM ApplicationReviewHistory arh
WHERE arh.ApplicationId NOT IN (SELECT Id FROM Applications)
   OR arh.ReviewedById NOT IN (SELECT Id FROM Users);

-- Verify NULL values in critical fields
PRINT '';
PRINT 'NULL value check - Applications.DocumentStorageType (should be 0):';
SELECT COUNT(*) FROM Applications WHERE DocumentStorageType IS NULL;

PRINT '';
PRINT 'NULL value check - ApplicationDocuments.Version (should be 0):';
SELECT COUNT(*) FROM ApplicationDocuments WHERE Version IS NULL;

PRINT '';
PRINT 'NULL value check - ApplicationDocuments.IsLatestVersion (should be 0):';
SELECT COUNT(*) FROM ApplicationDocuments WHERE IsLatestVersion IS NULL;

PRINT '';
PRINT 'NULL value check - ApplicationDocuments.ReviewStatus (should be 0):';
SELECT COUNT(*) FROM ApplicationDocuments WHERE ReviewStatus IS NULL;

PRINT '';
PRINT 'NULL value check - ApplicationDocuments.FileSize (should be 0):';
SELECT COUNT(*) FROM ApplicationDocuments WHERE FileSize IS NULL;

-- DocumentTypeId mapping status
PRINT '';
PRINT 'DocumentTypeId Status - Count of NULL values (requires manual mapping):';
SELECT COUNT(*) AS NullDocumentTypeId FROM ApplicationDocuments WHERE DocumentTypeId IS NULL;

PRINT '';
PRINT '===== END POST-MIGRATION VALIDATION =====';

GO

-- ============================================================================
-- POST-MIGRATION MANUAL STEPS (to be executed after inspection)
-- ============================================================================

/*
MANUAL STEP 1: Map ApplicationDocuments.LegacyDocumentType → DocumentTypeId

After migration, you MUST populate DocumentTypeId. Options:

Option A: Manual mapping if you know the mapping
--------
UPDATE ApplicationDocuments
SET DocumentTypeId = dt.Id
FROM ApplicationDocuments ad
JOIN DocumentTypes dt ON ad.LegacyDocumentType = dt.Name
WHERE ad.DocumentTypeId IS NULL;

Option B: If DocumentType string matches DocumentTypes.Code
--------
UPDATE ApplicationDocuments
SET DocumentTypeId = dt.Id
FROM ApplicationDocuments ad
JOIN DocumentTypes dt ON ad.LegacyDocumentType = dt.Code
WHERE ad.DocumentTypeId IS NULL;

Option C: Use a predefined mapping table (create this if needed)
--------
-- First, see what document types exist:
SELECT DISTINCT LegacyDocumentType FROM ApplicationDocuments;
SELECT Id, Code, Name FROM DocumentTypes;
-- Then map manually

MANUAL STEP 2: Recalculate MandatoryDocumentsComplete 

UPDATE Applications
SET MandatoryDocumentsComplete = CASE 
	WHEN EXISTS (
		SELECT 1 
		FROM ApplicationDocuments ad
		WHERE ad.ApplicationId = Applications.Id
		  AND ad.DocumentTypeId IN (SELECT Id FROM DocumentTypes WHERE IsMandatory = 1)
	) THEN 1
	ELSE 0
END;

MANUAL STEP 3: Verify RsaPin uniqueness constraint

The filtered unique index on Applications.RsaPin should automatically validate.
If any violations exist, you'll see constraint errors:

SELECT RsaPin, COUNT(*) as DupCount
FROM Applications
WHERE Status IN ('draft', 'submitted', 'under_review', 'returned', 'resubmitted')
GROUP BY RsaPin
HAVING COUNT(*) > 1;

If duplicates exist, resolve them before proceeding.

MANUAL STEP 4: Test application connectivity

Run your .NET application and verify:
- All Applications load correctly
- All ApplicationDocuments can be retrieved
- All review history is accessible
- Foreign key relationships work
- Filters and searches function properly
*/

PRINT '';
PRINT '===== SEE COMMENTS ABOVE FOR MANUAL STEPS =====';

GO
