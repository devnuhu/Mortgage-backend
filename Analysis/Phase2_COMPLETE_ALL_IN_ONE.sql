-- ============================================================================
-- PHASE 2: COMPLETE LIVE DATA MIGRATION - ALL IN ONE
-- ============================================================================
-- MMGTDBCOPY → MMGTDB2A (COMBINED SCRIPT)
-- Includes: identity_document creation with SuperAdmin lookup + Full migration
-- ============================================================================

USE MMGTDB2A
GO

PRINT '============================================================================='
PRINT 'PHASE 2: COMPLETE LIVE DATA MIGRATION'
PRINT 'MMGTDBCOPY > MMGTDB2A (COMBINED SCRIPT)'
PRINT '============================================================================='
GO

-- ============================================================================
-- PART 1: ENSURE identity_document DOCUMENTTYPE EXISTS
-- ============================================================================

PRINT ''
PRINT '============================================================================='
PRINT 'PART 1: PREPARE DOCUMENTTYPE'
PRINT '============================================================================='
GO

PRINT ''
PRINT '1.0: Checking for identity_document DocumentType...'

DECLARE @ExistingId UNIQUEIDENTIFIER = (
	SELECT Id FROM MMGTDB2A.dbo.DocumentTypes 
	WHERE LOWER(Code) = 'identity_document' 
	AND LOWER(Name) = 'identity_document'
)

IF @ExistingId IS NOT NULL
BEGIN
	PRINT 'OK: identity_document DocumentType already exists'
END
ELSE
BEGIN
	PRINT 'CREATING: identity_document DocumentType...'

	-- Find SUPER_ADMIN user for CreatedBy (FK requirement)
	DECLARE @SuperAdminId UNIQUEIDENTIFIER = (
		SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
		WHERE LOWER(Role) = 'super_admin' AND LOWER(Email) = 'superadmin@pfa.com' AND IsActive = 1
	)

	IF @SuperAdminId IS NULL
	BEGIN
		SET @SuperAdminId = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
			WHERE LOWER(Role) = 'super_admin' AND IsActive = 1
			ORDER BY CreatedAt DESC
		)
	END

	IF @SuperAdminId IS NULL
	BEGIN
		SET @SuperAdminId = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
			WHERE LOWER(Role) = 'admin' AND IsActive = 1
			ORDER BY CreatedAt DESC
		)
	END

	IF @SuperAdminId IS NULL
	BEGIN
		RAISERROR('Cannot create DocumentType: No SUPER_ADMIN or ADMIN user found. Run DbSeeder first.', 16, 1)
	END

	DECLARE @MaxDisplayOrder INT = (SELECT ISNULL(MAX(DisplayOrder), 0) FROM MMGTDB2A.dbo.DocumentTypes)
	DECLARE @NewId UNIQUEIDENTIFIER = NEWID()

	INSERT INTO MMGTDB2A.dbo.DocumentTypes (
		Id, Code, Name, Description, IsMandatory, DisplayOrder, MaxFileSizeKB, IsActive, CreatedAt, CreatedBy
	)
	VALUES (
		@NewId, 'identity_document', 'identity_document',
		'Legacy on-disk document type for migrated records',
		0, @MaxDisplayOrder + 1, 300, 1, GETDATE(), @SuperAdminId
	)

	PRINT 'CREATED: ' + CAST(@NewId AS NVARCHAR(36))
END

GO

-- Verify it exists
DECLARE @VerifyId UNIQUEIDENTIFIER = (SELECT Id FROM MMGTDB2A.dbo.DocumentTypes WHERE LOWER(Code) = 'identity_document')
IF @VerifyId IS NULL
	RAISERROR('Failed to create/verify identity_document DocumentType', 16, 1)

PRINT 'OK: DocumentType verification passed'
GO

-- ============================================================================
-- PART 2: EXECUTE FULL MIGRATION
-- ============================================================================

PRINT ''
PRINT '============================================================================='
PRINT 'PART 2: EXECUTE MIGRATION'
PRINT '============================================================================='
GO

-- ============================================================================
-- SECTION 1: PRE-MIGRATION VALIDATION
-- ============================================================================
PRINT ''
PRINT '=== SECTION 1: PRE-MIGRATION VALIDATION ==='
GO

PRINT '1.1: Verify MMGTDBCOPY accessible...'
BEGIN TRY
	DECLARE @SourceCount INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments)
	PRINT 'OK: MMGTDBCOPY accessible - ApplicationDocuments: ' + CAST(@SourceCount AS NVARCHAR(15)) + ' records'
END TRY
BEGIN CATCH
	RAISERROR('Cannot access source database', 16, 1)
END CATCH
GO

PRINT ''
PRINT '1.2: Source record counts:'
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

-- ============================================================================
-- SECTION 2: PREPARE TARGET TABLES
-- ============================================================================
PRINT ''
PRINT '=== SECTION 2: PREPARE TARGET TABLES ==='
PRINT '2.1: Disabling FK constraints...'

ALTER TABLE MMGTDB2A.dbo.ApplicationDocuments NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationReviewHistory NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Applications NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Users NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.PMICompanies NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.DocumentTypes NOCHECK CONSTRAINT ALL

PRINT 'OK: FK constraints disabled'
GO

PRINT ''
PRINT '2.2: Clearing existing data...'

DELETE FROM MMGTDB2A.dbo.ApplicationReviewHistory WHERE 1=1
PRINT 'ApplicationReviewHistory: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

DELETE FROM MMGTDB2A.dbo.ApplicationDocuments WHERE 1=1
PRINT 'ApplicationDocuments: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

DELETE FROM MMGTDB2A.dbo.Applications WHERE 1=1
PRINT 'Applications: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

-- Skip Users delete to preserve seeded admin accounts (superadmin@pfa.com, admin@pfa.com)
-- Users migration will use duplicate checking to preserve existing seeded users
PRINT 'Users: Skipping delete to preserve seeded admin accounts'

DELETE FROM MMGTDB2A.dbo.PMICompanies WHERE 1=1
PRINT 'PMICompanies: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows deleted'

PRINT 'OK: Target tables cleared'
GO

-- ============================================================================
-- SECTION 3: MIGRATE DATA
-- ============================================================================
PRINT ''
PRINT '=== SECTION 3: MIGRATE DATA ==='

PRINT ''
PRINT '3.1: Migrating PMICompanies...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.PMICompanies (
		Id, PmiName, EmailAddress, ResourcePersonName, ResourcePersonEmail, 
		PhoneNumber, Status, CreatedAt, UpdatedAt
	)
	SELECT 
		Id, PmiName, EmailAddress, ResourcePersonName, ResourcePersonEmail, 
		PhoneNumber, Status, CreatedAt, UpdatedAt
	FROM MMGTDBCOPY.dbo.PMICompanies

	PRINT 'OK: PMICompanies migrated: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT 'ERROR PMICompanies: ' + ERROR_MESSAGE()
END CATCH
GO

PRINT ''
PRINT '3.2: Migrating Users...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.Users (
		Id, Email, FirstName, LastName, Role, PasswordHash, IsFirstLogin, 
		IsActive, PMICompanyId, CreatedAt, UpdatedAt
	)
	SELECT 
		u.Id, u.Email, u.FirstName, u.LastName, u.Role, u.PasswordHash, u.IsFirstLogin, 
		u.IsActive, u.PMICompanyId, u.CreatedAt, u.UpdatedAt
	FROM MMGTDBCOPY.dbo.Users u
	WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users ux WHERE LOWER(ux.Email) = LOWER(u.Email))

	PRINT 'OK: Users migrated: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT 'ERROR Users: ' + ERROR_MESSAGE()
END CATCH
GO

PRINT ''
PRINT '3.3: Migrating Applications...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.Applications (
		Id, ApplicationNumber, Status, Priority, FirstName, LastName, RsaPin,
		EquityValue, SupplementaryValue, LoanAmount, PropertyValue,
		PmiRepName, PmiRepEmail, PmiCompanyName,
		CreatedAt, UpdatedAt, SubmittedAt, UserId, PmiCompanyId,
		MiddleName,
		DocumentStorageType, MandatoryDocumentsComplete
	)
	SELECT 
		a.Id, a.ApplicationNumber, a.Status, a.Priority, a.FirstName, a.LastName, a.RsaPin,
		a.EquityValue, a.SupplementaryValue, a.LoanAmount, a.PropertyValue,
		a.PmiRepName, a.PmiRepEmail, a.PmiCompanyName,
		a.CreatedAt, a.UpdatedAt, a.SubmittedAt, a.UserId, a.PmiCompanyId,
		a.MiddleName,
		'FileSystem' as DocumentStorageType, 0 as MandatoryDocumentsComplete
	FROM MMGTDBCOPY.dbo.Applications a

	DECLARE @AppCount INT = @@ROWCOUNT
	PRINT 'OK: Applications migrated: ' + CAST(@AppCount AS NVARCHAR(10)) + ' rows'

	IF @AppCount > 0
		PRINT 'WARNING: ' + CAST(@AppCount AS NVARCHAR(10)) + ' Applications migrated'
END TRY
BEGIN CATCH
	PRINT 'ERROR Applications: ' + ERROR_MESSAGE()
END CATCH
GO

PRINT ''
PRINT '3.4: Building DocumentType mapping...'
BEGIN TRY
	IF OBJECT_ID('tempdb..#UniqueDocTypes', 'U') IS NOT NULL 
		DROP TABLE #UniqueDocTypes
	IF OBJECT_ID('tempdb..#DocTypeMapping', 'U') IS NOT NULL 
		DROP TABLE #DocTypeMapping

	SELECT DISTINCT ad.DocumentType as SourceType
	INTO #UniqueDocTypes
	FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
	WHERE ad.DocumentType IS NOT NULL

	SELECT 
		uddt.SourceType,
		dt.Id as TargetTypeId,
		dt.Code,
		dt.Name,
		COUNT(ad.Id) as DocCount
	INTO #DocTypeMapping
	FROM #UniqueDocTypes uddt
	LEFT JOIN MMGTDBCOPY.dbo.ApplicationDocuments ad ON ad.DocumentType = uddt.SourceType
	LEFT JOIN MMGTDB2A.dbo.DocumentTypes dt ON 
		(LOWER(uddt.SourceType) = LOWER(dt.Code) OR LOWER(uddt.SourceType) = LOWER(dt.Name))
	GROUP BY uddt.SourceType, dt.Id, dt.Code, dt.Name

	DECLARE @UnmappedCount INT = (SELECT COUNT(*) FROM #DocTypeMapping WHERE TargetTypeId IS NULL)

	IF @UnmappedCount > 0
	BEGIN
		PRINT 'WARNING: ' + CAST(@UnmappedCount AS NVARCHAR(10)) + ' DocumentTypes could not be mapped'

		DECLARE @DefaultTypeId UNIQUEIDENTIFIER = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes 
			WHERE LOWER(Code) = 'identity_document'
		)

		IF @DefaultTypeId IS NOT NULL
		BEGIN
			PRINT 'Assigning unmapped to default DocumentType: ' + CAST(@DefaultTypeId AS NVARCHAR(36))
			UPDATE #DocTypeMapping SET TargetTypeId = @DefaultTypeId WHERE TargetTypeId IS NULL
		END
	END

	PRINT 'OK: DocumentType mapping ready'
END TRY
BEGIN CATCH
	PRINT 'ERROR DocumentType mapping: ' + ERROR_MESSAGE()
	IF OBJECT_ID('tempdb..#UniqueDocTypes', 'U') IS NOT NULL DROP TABLE #UniqueDocTypes
	IF OBJECT_ID('tempdb..#DocTypeMapping', 'U') IS NOT NULL DROP TABLE #DocTypeMapping
END CATCH
GO

PRINT ''
PRINT '3.5: Migrating ApplicationDocuments...'
BEGIN TRY
	DECLARE @AdminUserId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users WHERE LOWER(Email) LIKE '%admin%' ORDER BY CreatedAt)

	IF @AdminUserId IS NULL
		SET @AdminUserId = (SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users ORDER BY CreatedAt)

	INSERT INTO MMGTDB2A.dbo.ApplicationDocuments (
		Id, ApplicationId, FileName, FilePath, FileContent, FileSize, 
		DocumentTypeId, LegacyDocumentType, IsLegacy, CreatedAt, FileType, Version, IsLatestVersion, ReviewStatus, UploadedBy
	)
	SELECT 
		ad.Id,
		ad.ApplicationId,
		ad.FileName,
		ad.FilePath,
		NULL as FileContent,
		ad.FileSize,
		ISNULL(dtm.TargetTypeId, (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes ORDER BY Id)),
		ad.DocumentType as LegacyDocumentType,
		1 as IsLegacy,
		GETDATE(),
		'Legacy' as FileType,
		1 as Version,
		1 as IsLatestVersion,
		'Pending' as ReviewStatus,
		@AdminUserId as UploadedBy
	FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
	LEFT JOIN #DocTypeMapping dtm ON ad.DocumentType = dtm.SourceType

	PRINT 'OK: ApplicationDocuments migrated: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows'

	IF OBJECT_ID('tempdb..#DocTypeMapping', 'U') IS NOT NULL DROP TABLE #DocTypeMapping
	IF OBJECT_ID('tempdb..#UniqueDocTypes', 'U') IS NOT NULL DROP TABLE #UniqueDocTypes
END TRY
BEGIN CATCH
	PRINT 'ERROR ApplicationDocuments: ' + ERROR_MESSAGE()
END CATCH
GO

PRINT ''
PRINT '3.6: Migrating ApplicationReviewHistory...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
		Id, ApplicationId, ReviewedById, CreatedAt, Status
	)
	SELECT 
		arh.Id, arh.ApplicationId, arh.ReviewedById, arh.CreatedAt, 'Completed' as Status
	FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh

	DECLARE @ReviewCount INT = @@ROWCOUNT
	PRINT 'OK: ApplicationReviewHistory migrated: ' + CAST(@ReviewCount AS NVARCHAR(10)) + ' rows'

	IF @ReviewCount > 0
		PRINT 'WARNING: ' + CAST(@ReviewCount AS NVARCHAR(10)) + ' ReviewHistory records may have invalid ReviewedById FK'
END TRY
BEGIN CATCH
	PRINT 'ERROR ApplicationReviewHistory: ' + ERROR_MESSAGE()
END CATCH
GO

-- ============================================================================
-- SECTION 4: RE-ENABLE CONSTRAINTS
-- ============================================================================
PRINT ''
PRINT '=== SECTION 4: RE-ENABLE CONSTRAINTS ==='
PRINT 'Re-enabling FK constraints...'

ALTER TABLE MMGTDB2A.dbo.PMICompanies CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Users CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Applications CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationDocuments CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationReviewHistory CHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.DocumentTypes CHECK CONSTRAINT ALL

PRINT 'OK: FK constraints re-enabled'
GO

-- ============================================================================
-- SECTION 5: POST-MIGRATION VALIDATION
-- ============================================================================
PRINT ''
PRINT '=== SECTION 5: POST-MIGRATION VALIDATION ==='

PRINT ''
PRINT '5.1: RECORD COUNTS COMPARISON:'

SELECT 
	t.TableName,
	ISNULL(target.cnt, 0) as Migrated,
	source.cnt as Source,
	CASE WHEN ISNULL(target.cnt, 0) = source.cnt THEN 'MATCH' ELSE 'MISMATCH' END as Status
FROM (
	SELECT 'Users' as TableName
	UNION ALL SELECT 'PMICompanies'
	UNION ALL SELECT 'Applications'
	UNION ALL SELECT 'ApplicationDocuments'
	UNION ALL SELECT 'ApplicationReviewHistory'
) t
LEFT JOIN (
	SELECT 'Users' as TableName, COUNT(*) as cnt FROM MMGTDB2A.dbo.Users
	UNION ALL SELECT 'PMICompanies', COUNT(*) FROM MMGTDB2A.dbo.PMICompanies
	UNION ALL SELECT 'Applications', COUNT(*) FROM MMGTDB2A.dbo.Applications
	UNION ALL SELECT 'ApplicationDocuments', COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments
	UNION ALL SELECT 'ApplicationReviewHistory', COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory
) target ON t.TableName = target.TableName
LEFT JOIN (
	SELECT 'Users' as TableName, COUNT(*) as cnt FROM MMGTDBCOPY.dbo.Users
	UNION ALL SELECT 'PMICompanies', COUNT(*) FROM MMGTDBCOPY.dbo.PMICompanies
	UNION ALL SELECT 'Applications', COUNT(*) FROM MMGTDBCOPY.dbo.Applications
	UNION ALL SELECT 'ApplicationDocuments', COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments
	UNION ALL SELECT 'ApplicationReviewHistory', COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationReviewHistory
) source ON t.TableName = source.TableName
GO

PRINT ''
PRINT '5.2: DATA QUALITY CHECKS - IsLegacy distribution:'
SELECT IsLegacy, COUNT(*) as Count FROM MMGTDB2A.dbo.ApplicationDocuments GROUP BY IsLegacy
GO

PRINT ''
PRINT '5.3: FOREIGN KEY INTEGRITY:'
SELECT COUNT(*) as DocumentTypesWithIdentityDocument
FROM MMGTDB2A.dbo.DocumentTypes 
WHERE LOWER(Code) = 'identity_document'
GO

PRINT ''
PRINT '============================================================================='
PRINT 'MIGRATION COMPLETE'
PRINT 'Review validation results above'
PRINT '============================================================================='
GO

PRINT ''
PRINT 'Completion time: ' + CONVERT(NVARCHAR(30), GETDATE(), 121)
GO
