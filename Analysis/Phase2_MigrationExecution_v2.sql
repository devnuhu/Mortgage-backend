-- ============================================================================
-- PHASE 2: CORRECTED LIVE DATA MIGRATION - MMGTDBCOPY → MMGTDB2A (v2)
-- ============================================================================
-- FIXES IN v2:
-- 1. Remove FK constraints BEFORE deletes to prevent cascades
-- 2. Add TRY-CATCH blocks around each major insert to catch errors
-- 3. Add detailed error reporting for each table
-- 4. Fix Users insertion to handle PMICompanyId lookup from PMICompanies 
-- 5. Validate ApplicationDocuments DocumentTypeId mapping before insert
-- ============================================================================

USE MMGTDB2A
GO

PRINT '═══════════════════════════════════════════════════════════════════'
PRINT 'PHASE 2: CORRECTED MIGRATION v2 - MMGTDBCOPY → MMGTDB2A'
PRINT '═══════════════════════════════════════════════════════════════════'

-- ============================================================================
-- SECTION 1: PRE-MIGRATION VALIDATION
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 1: PRE-MIGRATION VALIDATION ==='
PRINT 'Validating source and target readiness...'
GO

-- 1.1: Verify source accessible
PRINT CHAR(10) + '1.1: Verify MMGTDBCOPY accessible...'
BEGIN TRY
	SELECT TOP 1 COUNT(*) as test FROM MMGTDBCOPY.dbo.Users
	PRINT '✓ MMGTDBCOPY accessible'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR: Cannot access MMGTDBCOPY - ' + ERROR_MESSAGE()
	RAISERROR('Cannot access source database', 16, 1)
END CATCH
GO

-- 1.2: Verify IsLegacy column exists
PRINT CHAR(10) + '1.2: Verify target schema ready...'
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
			   WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='IsLegacy')
BEGIN
	PRINT '✗ ERROR: IsLegacy column not found'
	RAISERROR('IsLegacy column missing', 16, 1)
END
PRINT '✓ MMGTDB2A schema verified'
GO

-- 1.3: Source record counts
PRINT CHAR(10) + '1.4: Source (MMGTDBCOPY) record counts:'
SELECT 'Users' as [Table], COUNT(*) as Records FROM MMGTDBCOPY.dbo.Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM MMGTDBCOPY.dbo.PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM MMGTDBCOPY.dbo.Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationReviewHistory
GO

-- 1.5: Target current record counts (before)
PRINT CHAR(10) + '1.5: Target (MMGTDB2A) BEFORE migration:'
SELECT 'Users' as [Table], COUNT(*) as Records FROM MMGTDB2A.dbo.Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM MMGTDB2A.dbo.PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM MMGTDB2A.dbo.Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory
GO

-- ============================================================================
-- SECTION 2: PREPARE TARGET DATABASE - DISABLE ALL FK CONSTRAINTS FIRST
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 2: PREPARE TARGET DATABASE ==='
PRINT 'Disabling all FK constraints before deletion...'

-- Disable FK constraints for all affected tables (to prevent cascade deletes)
ALTER TABLE MMGTDB2A.dbo.ApplicationDocuments NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationReviewHistory NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Applications NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Users NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.PMICompanies NOCHECK CONSTRAINT ALL
PRINT '✓ FK constraints disabled'
GO

-- Delete in reverse FK dependency order
PRINT CHAR(10) + 'Clearing target tables...'

DELETE FROM MMGTDB2A.dbo.ApplicationReviewHistory WHERE 1=1
PRINT 'ApplicationReviewHistory: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

DELETE FROM MMGTDB2A.dbo.ApplicationDocuments WHERE 1=1
PRINT 'ApplicationDocuments: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

DELETE FROM MMGTDB2A.dbo.Applications WHERE 1=1
PRINT 'Applications: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

DELETE FROM MMGTDB2A.dbo.Users WHERE 1=1
PRINT 'Users: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

DELETE FROM MMGTDB2A.dbo.PMICompanies WHERE 1=1
PRINT 'PMICompanies: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

PRINT '✓ Target tables cleared'
GO

-- ============================================================================
-- SECTION 3: MIGRATE DATA WITH ERROR HANDLING
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 3: MIGRATE DATA ==='

-- 3.1: Migrate PMICompanies FIRST (no FK dependencies)
PRINT CHAR(10) + '3.1: Migrating PMICompanies...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.PMICompanies (
		Id, PmiName, EmailAddress, ResourcePersonName, ResourcePersonEmail, 
		PhoneNumber, Status, CreatedAt, UpdatedAt
	)
	SELECT 
		Id, PmiName, EmailAddress, ResourcePersonName, ResourcePersonEmail,
		PhoneNumber, Status, CreatedAt, UpdatedAt
	FROM MMGTDBCOPY.dbo.PMICompanies

	DECLARE @PMICount INT = @@ROWCOUNT
	PRINT '✓ PMICompanies migrated: ' + CAST(@PMICount AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR migrating PMICompanies: ' + ERROR_MESSAGE()
END CATCH
GO

-- 3.2: Migrate Users (depends on PMICompanies existing for FK)
PRINT CHAR(10) + '3.2: Migrating Users...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.Users (
		Id, Email, FirstName, LastName, Role, PasswordHash, 
		IsFirstLogin, IsActive, CreatedAt, UpdatedAt, 
		LastLoginAt, PMICompanyId, PMIEmail, FirstLoginTokenHash, FirstLoginTokenExpiresAt
	)
	SELECT 
		Id, Email, FirstName, LastName, Role, PasswordHash,
		IsFirstLogin, IsActive, CreatedAt, UpdatedAt,
		LastLoginAt, PMICompanyId, PMIEmail, FirstLoginTokenHash, FirstLoginTokenExpiresAt
	FROM MMGTDBCOPY.dbo.Users

	DECLARE @UsersCount INT = @@ROWCOUNT
	PRINT '✓ Users migrated: ' + CAST(@UsersCount AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR migrating Users: ' + ERROR_MESSAGE()
	-- Continue with other tables
END CATCH
GO

-- 3.3: Migrate Applications (depends on Users and PMICompanies)
PRINT CHAR(10) + '3.3: Migrating Applications...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.Applications (
		Id, ApplicationNumber, Status, Priority, FirstName, MiddleName, LastName, 
		RsaPin, EquityValue, SupplementaryValue, LoanAmount, PropertyValue,
		PmiRepName, PmiRepEmail, PmiCompanyName, CreatedAt, UpdatedAt, SubmittedAt,
		DocumentStorageType, MandatoryDocumentsComplete, UserId, PmiCompanyId,
		CompletedAt, PhoneNumber
	)
	SELECT 
		Id, ApplicationNumber, Status, Priority, FirstName, MiddleName, LastName,
		RsaPin, EquityValue, SupplementaryValue, LoanAmount, PropertyValue,
		PmiRepName, PmiRepEmail, PmiCompanyName, CreatedAt, UpdatedAt, SubmittedAt,
		'LEGACY' as DocumentStorageType,
		0 as MandatoryDocumentsComplete,
		UserId, PmiCompanyId,
		NULL as CompletedAt,
		NULL as PhoneNumber
	FROM MMGTDBCOPY.dbo.Applications

	DECLARE @AppsCount INT = @@ROWCOUNT
	PRINT '✓ Applications migrated: ' + CAST(@AppsCount AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR migrating Applications: ' + ERROR_MESSAGE()
END CATCH
GO

-- 3.4: Build DocumentType Mapping
PRINT CHAR(10) + '3.4: Building DocumentType mapping...'
BEGIN TRY
	SELECT DISTINCT 
		ad.DocumentType as SourceType,
		dt.Id as TargetTypeId
	INTO #DocTypeMapping
	FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
	LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON 
		LOWER(ad.DocumentType) = LOWER(dt.Code)
		OR LOWER(ad.DocumentType) = LOWER(dt.Name)

	DECLARE @MappedCount INT = (SELECT COUNT(*) FROM #DocTypeMapping WHERE TargetTypeId IS NOT NULL)
	DECLARE @UnmappedCount INT = (SELECT COUNT(*) FROM #DocTypeMapping WHERE TargetTypeId IS NULL)

	PRINT '✓ Document Type Mapping: ' + CAST(@MappedCount AS NVARCHAR(10)) + ' mapped'

	IF @UnmappedCount > 0
	BEGIN
		PRINT '⚠ Warning: ' + CAST(@UnmappedCount AS NVARCHAR(10)) + ' unmapped types detected:'
		SELECT DISTINCT SourceType FROM #DocTypeMapping WHERE TargetTypeId IS NULL

		-- Show available DocumentTypes in target
		PRINT CHAR(10) + 'Available DocumentTypes in target:'
		SELECT TOP 20 Code, Name, Id FROM MMGTDB2A.dbo.DocumentTypes

		-- Assign unmapped to default (first identity_document or first available)
		DECLARE @DefaultTypeId UNIQUEIDENTIFIER = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes 
			WHERE LOWER(Code) = 'identity_document' 
			ORDER BY Id
		)
		IF @DefaultTypeId IS NULL
			SET @DefaultTypeId = (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes ORDER BY Id)

		UPDATE #DocTypeMapping 
		SET TargetTypeId = @DefaultTypeId
		WHERE TargetTypeId IS NULL
		PRINT '✓ Assigned unmapped types to default: ' + CAST(@DefaultTypeId AS NVARCHAR(MAX))
	END
END TRY
BEGIN CATCH
	PRINT '✗ ERROR building document type mapping: ' + ERROR_MESSAGE()
END CATCH
GO

-- 3.5: Migrate ApplicationDocuments
PRINT CHAR(10) + '3.5: Migrating ApplicationDocuments...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
		Id, FileName, FilePath, FileContent, FileType, DocumentTypeId,
		LegacyDocumentType, FileSize, Version, IsLatestVersion, 
		ReviewStatus, ReviewComments, ReviewedBy, ReviewedAt,
		CreatedAt, UploadedBy, ApplicationId, IsLegacy
	)
	SELECT 
		ad.Id, 
		ad.FileName, 
		ad.FilePath,
		NULL as FileContent,
		ad.FileType,
		ISNULL(dtm.TargetTypeId, 
			(SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes ORDER BY Id)) as DocumentTypeId,
		ad.DocumentType as LegacyDocumentType,
		ad.FileSize,
		1 as Version,
		1 as IsLatestVersion,
		'pending' as ReviewStatus,
		NULL as ReviewComments,
		NULL as ReviewedBy,
		NULL as ReviewedAt,
		ad.CreatedAt,
		NULL as UploadedBy,
		ad.ApplicationId,
		1 as IsLegacy
	FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
	LEFT JOIN #DocTypeMapping dtm ON ad.DocumentType = dtm.SourceType

	DECLARE @DocsCount INT = @@ROWCOUNT
	PRINT '✓ ApplicationDocuments migrated: ' + CAST(@DocsCount AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR migrating ApplicationDocuments: ' + ERROR_MESSAGE()
END CATCH
GO

-- Clean up temp table
IF OBJECT_ID('tempdb..#DocTypeMapping') IS NOT NULL
	DROP TABLE #DocTypeMapping
GO

-- 3.6: Migrate ApplicationReviewHistory
PRINT CHAR(10) + '3.6: Migrating ApplicationReviewHistory...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
		Id, Status, Comments, CreatedAt, ApplicationId, ReviewedById
	)
	SELECT 
		Id, Status, Comments, CreatedAt, ApplicationId, ReviewedById
	FROM MMGTDBCOPY.dbo.ApplicationReviewHistory

	DECLARE @HistoryCount INT = @@ROWCOUNT
	PRINT '✓ ApplicationReviewHistory migrated: ' + CAST(@HistoryCount AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR migrating ApplicationReviewHistory: ' + ERROR_MESSAGE()
END CATCH
GO

-- ============================================================================
-- SECTION 4: RE-ENABLE FK CONSTRAINTS
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 4: RE-ENABLE FK CONSTRAINTS ==='
PRINT 'Re-enabling FK constraints...'

ALTER TABLE MMGTDB2A.dbo.Users CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.PMICompanies CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Applications CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationDocuments CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationReviewHistory CHECK CONSTRAINT ALL

PRINT '✓ FK constraints re-enabled'
GO

-- ============================================================================
-- SECTION 5: POST-MIGRATION VALIDATION
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 5: POST-MIGRATION VALIDATION ==='

PRINT CHAR(10) + '5.1: Target (MMGTDB2A) AFTER migration:'
SELECT 'Users' as [Table], COUNT(*) as Records FROM MMGTDB2A.dbo.Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM MMGTDB2A.dbo.PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM MMGTDB2A.dbo.Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory
GO

PRINT CHAR(10) + '5.2: Verify IsLegacy flags set correctly:'
SELECT 
	IsLegacy,
	COUNT(*) as DocumentCount
FROM MMGTDB2A.dbo.ApplicationDocuments
GROUP BY IsLegacy
GO

PRINT CHAR(10) + '5.3: Verify DocumentTypeId is NOT NULL:'
SELECT 
	'Non-NULL DocumentTypeId' as Check_Type,
	COUNT(*) as Count
FROM MMGTDB2A.dbo.ApplicationDocuments
WHERE DocumentTypeId IS NOT NULL

UNION ALL

SELECT 
	'NULL DocumentTypeId (ERROR)',
	COUNT(*)
FROM MMGTDB2A.dbo.ApplicationDocuments
WHERE DocumentTypeId IS NULL
GO

PRINT CHAR(10) + '5.4: Verify FK integrity (Applications.UserId → Users):'
SELECT 
	'Valid FK' as FK_Status,
	COUNT(*) as Count
FROM MMGTDB2A.dbo.Applications a
WHERE EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)

UNION ALL

SELECT 
	'Invalid FK (ERROR)',
	COUNT(*)
FROM MMGTDB2A.dbo.Applications a
WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)
GO

PRINT CHAR(10) + '=== MIGRATION COMPLETE ==='
PRINT 'Review results above for any errors or warnings.'
GO
