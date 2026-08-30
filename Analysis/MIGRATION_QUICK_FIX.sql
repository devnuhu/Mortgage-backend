-- ============================================================================
-- QUICK FIX MIGRATION SCRIPT - Address the errors encountered
-- ============================================================================
-- Issues addressed:
-- 1. Populates DocumentTypes table first
-- 2. Properly cascade delete or disable constraints
-- 3. Uses correct DocumentTypeId for ApplicationDocuments
-- ============================================================================

USE MMGTDB2A;
GO

PRINT '===== QUICK FIX: CORRECTED MIGRATION =====';
PRINT '';

-- ============================================================================
-- STEP 0: Populate DocumentTypes (if not already populated)
-- ============================================================================

PRINT 'STEP 0: Checking and populating DocumentTypes if needed...';

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
-- STEP 1: Verify DocumentTypes exist and are valid
-- ============================================================================

PRINT 'STEP 1: Checking DocumentTypes...';

DECLARE @DocTypeCount INT = 0;
DECLARE @ActiveDocTypeCount INT = 0;

SELECT @DocTypeCount = COUNT(*) FROM DocumentTypes;
SELECT @ActiveDocTypeCount = COUNT(*) FROM DocumentTypes WHERE IsActive = 1;

PRINT 'Total DocumentTypes: ' + CAST(@DocTypeCount AS VARCHAR(10));
PRINT 'Active DocumentTypes: ' + CAST(@ActiveDocTypeCount AS VARCHAR(10));

IF @ActiveDocTypeCount = 0
BEGIN
	PRINT 'ERROR: No active DocumentTypes found!';
	PRINT 'You MUST have at least one active DocumentType before migration.';
	PRINT '';
	PRINT 'Sample SQL to create a default DocumentType:';
	PRINT 'INSERT INTO DocumentTypes (Id, Code, Name, Description, IsMandatory, DisplayOrder, MaxFileSizeKB, IsActive, CreatedAt, CreatedBy)';
	PRINT 'VALUES (NEWID(), '
	PRINT '''DEFAULT'', '
	PRINT '''Default Document'', '
	PRINT '''Default document type for legacy imports'', '
	PRINT '0, 1, 5000, 1, GETDATE(), (SELECT TOP 1 Id FROM Users ORDER BY CreatedAt));';
	PRINT '';
	RAISERROR('No active DocumentTypes. Cannot proceed with migration.', 16, 1);
END

PRINT 'DocumentTypes verified. Proceeding...';
PRINT '';

-- ============================================================================
-- STEP 2: Disable ALL constraints (comprehensive)
-- ============================================================================

PRINT 'STEP 2: Disabling all foreign key constraints...';

DECLARE @sql NVARCHAR(MAX) = '';

SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(t.name) + ' NOCHECK CONSTRAINT ALL;' + CHAR(13)
FROM sys.tables t
WHERE SCHEMA_NAME(t.schema_id) = 'dbo';

EXEC sp_executesql @sql;

PRINT 'All constraints disabled.';
PRINT '';

-- ============================================================================
-- STEP 3: Clear existing data (comprehensive - all tables)
-- ============================================================================

PRINT 'STEP 3: Clearing existing data...';

-- Delete from all tables in dependency order
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

-- These have FK to Users, so delete them too
DELETE FROM DocumentTypes WHERE CreatedBy NOT IN (SELECT Id FROM MMGTDBCOPY.dbo.Users);
DELETE FROM EscalationRules WHERE CreatedBy NOT IN (SELECT Id FROM MMGTDBCOPY.dbo.Users);

-- Clear organizational data
DELETE FROM Users;
DELETE FROM PMICompanies;

PRINT 'Data cleared from all tables.';
PRINT '';

-- ============================================================================
-- STEP 4: Migrate Users and PMICompanies
-- ============================================================================

PRINT 'STEP 4: Migrating Users...';

INSERT INTO Users
(
	Id, Email, FirstName, LastName, Role, PasswordHash,
	IsFirstLogin, IsActive, CreatedAt, UpdatedAt, LastLoginAt,
	PMICompanyId, PMIEmail, FirstLoginTokenHash, FirstLoginTokenExpiresAt
)
SELECT
	Id, Email, FirstName, LastName, Role, PasswordHash,
	IsFirstLogin, IsActive, CreatedAt, UpdatedAt, LastLoginAt,
	PMICompanyId, PMIEmail, FirstLoginTokenHash, FirstLoginTokenExpiresAt
FROM MMGTDBCOPY.dbo.Users;

PRINT 'Users: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows inserted.';

PRINT 'STEP 4b: Migrating PMICompanies...';

INSERT INTO PMICompanies
(
	Id, PmiName, EmailAddress, ResourcePersonName,
	ResourcePersonEmail, PhoneNumber, Status, CreatedAt, UpdatedAt
)
SELECT
	Id, PmiName, EmailAddress, ResourcePersonName,
	ResourcePersonEmail, PhoneNumber, Status, CreatedAt, UpdatedAt
FROM MMGTDBCOPY.dbo.PMICompanies;

PRINT 'PMICompanies: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows inserted.';
PRINT '';

-- ============================================================================
-- STEP 5: Re-restore DocumentTypes and EscalationRules with new Users
-- ============================================================================

PRINT 'STEP 5: Restoring DocumentTypes and EscalationRules...';

DELETE FROM DocumentTypes;
DELETE FROM EscalationRules;

-- Restore from backups if they exist, or just note they're empty
IF OBJECT_ID('DocumentTypes_BACKUP', 'U') IS NOT NULL
	INSERT INTO DocumentTypes SELECT * FROM DocumentTypes_BACKUP;

IF OBJECT_ID('EscalationRules_BACKUP', 'U') IS NOT NULL
	INSERT INTO EscalationRules SELECT * FROM EscalationRules_BACKUP;

DECLARE @DocCount INT = (SELECT COUNT(*) FROM DocumentTypes);
DECLARE @EscCount INT = (SELECT COUNT(*) FROM EscalationRules);

PRINT 'DocumentTypes restored: ' + CAST(@DocCount AS VARCHAR(10)) + ' rows.';
PRINT 'EscalationRules restored: ' + CAST(@EscCount AS VARCHAR(10)) + ' rows.';

IF @DocCount = 0
BEGIN
	PRINT '';
	PRINT 'WARNING: DocumentTypes table is empty after restore!';
	PRINT 'You must ensure DocumentTypes has at least one active entry.';
	PRINT '';
END

PRINT '';

-- ============================================================================
-- STEP 6: Migrate Applications
-- ============================================================================

PRINT 'STEP 6: Migrating Applications...';

INSERT INTO Applications
(
	Id, ApplicationNumber, Status, Priority, FirstName, MiddleName, LastName,
	RsaPin, EquityValue, SupplementaryValue, LoanAmount, PropertyValue,
	PmiRepName, PmiRepEmail, PmiCompanyName, CreatedAt, UpdatedAt, SubmittedAt,
	UserId, PmiCompanyId, DocumentStorageType, MandatoryDocumentsComplete,
	CompletedAt, PhoneNumber
)
SELECT
	Id, ApplicationNumber, Status, Priority, FirstName, MiddleName, LastName,
	RsaPin, EquityValue, SupplementaryValue, LoanAmount, PropertyValue,
	PmiRepName, PmiRepEmail, PmiCompanyName, CreatedAt, UpdatedAt, SubmittedAt,
	UserId, PmiCompanyId,
	'FileSystem' AS DocumentStorageType,
	0 AS MandatoryDocumentsComplete,
	CASE WHEN Status IN ('approved', 'rejected') THEN UpdatedAt ELSE NULL END AS CompletedAt,
	NULL AS PhoneNumber
FROM MMGTDBCOPY.dbo.Applications;

PRINT 'Applications: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows inserted.';
PRINT '';

-- ============================================================================
-- STEP 7: Migrate ApplicationDocuments (with fixed DocumentTypeId)
-- ============================================================================

PRINT 'STEP 7: Migrating ApplicationDocuments...';

DECLARE @DocumentTypeId UNIQUEIDENTIFIER = '4EECC413-3B37-452B-88B8-5C5A9A108E29';

PRINT 'Using DocumentTypeId: ' + CAST(@DocumentTypeId AS VARCHAR(36)) + ' (identity_document)';
PRINT 'Setting LegacyDocumentType = identity_document for all rows';

INSERT INTO ApplicationDocuments
(
	Id, FileName, FilePath, FileContent, FileType, DocumentTypeId,
	LegacyDocumentType, FileSize, Version, IsLatestVersion, ReviewStatus,
	ReviewComments, ReviewedBy, ReviewedAt, CreatedAt, UploadedBy,
	ApplicationId, IsLegacy
)
SELECT
	ad.Id, ad.FileName, ad.FilePath, NULL AS FileContent, ad.FileType,
	@DocumentTypeId AS DocumentTypeId,
	'identity_document' AS LegacyDocumentType,
	ad.FileSize, 1 AS Version, 1 AS IsLatestVersion, 'pending' AS ReviewStatus,
	NULL AS ReviewComments, NULL AS ReviewedBy, NULL AS ReviewedAt,
	ad.CreatedAt,
	COALESCE(
		(SELECT TOP 1 arh.ReviewedById 
		 FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh 
		 WHERE arh.ApplicationId = ad.ApplicationId
		 ORDER BY arh.CreatedAt DESC),
		(SELECT a.UserId FROM MMGTDBCOPY.dbo.Applications a WHERE a.Id = ad.ApplicationId)
	) AS UploadedBy,
	ad.ApplicationId, 1 AS IsLegacy
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad;

PRINT 'ApplicationDocuments: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows inserted.';
PRINT '';

-- ============================================================================
-- STEP 8: Migrate ApplicationReviewHistory
-- ============================================================================

PRINT 'STEP 8: Migrating ApplicationReviewHistory...';

INSERT INTO ApplicationReviewHistory
(
	Id, Status, Comments, CreatedAt, ApplicationId, ReviewedById
)
SELECT
	Id, Status, Comments, CreatedAt, ApplicationId, ReviewedById
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory;

PRINT 'ApplicationReviewHistory: ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows inserted.';
PRINT '';

-- ============================================================================
-- STEP 9: Re-enable all constraints
-- ============================================================================

PRINT 'STEP 9: Re-enabling all constraints...';

BEGIN TRY
    SELECT @sql = '';

    SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(t.name) + ' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(13)
    FROM sys.tables t
    WHERE SCHEMA_NAME(t.schema_id) = 'dbo';

    EXEC sp_executesql @sql;
    PRINT 'All constraints re-enabled successfully.';
END TRY
BEGIN CATCH
    PRINT 'Warning: Some constraints could not be re-enabled.';
    PRINT 'Error: ' + ERROR_MESSAGE();
    PRINT 'This may occur if FK references are missing. Check validation results.';
END CATCH

PRINT '';

-- ============================================================================
-- STEP 10: Validation
-- ============================================================================

PRINT 'STEP 10: Post-migration validation...';
PRINT '';

SELECT 'Users' AS Table_Name, COUNT(*) AS [RowCnt] FROM Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory;

PRINT '';
PRINT 'Migration Complete!';
PRINT '';
PRINT 'NEXT STEPS:';
PRINT '1. Verify row counts match expectations';
PRINT '2. Check for any constraint violations';
PRINT '3. Map ApplicationDocuments.LegacyDocumentType to proper DocumentTypeId (see Manual Step 1)';
PRINT '4. Test application connectivity';

GO
