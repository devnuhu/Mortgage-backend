-- ============================================================================
-- PHASE 2: FINAL CORRECTED MIGRATION - MMGTDBCOPY → MMGTDB2A (v3)
-- ============================================================================
-- CRITICAL FIXES IN v3:
-- 1. FIX: DocumentType mapping - proper JOIN and NULL handling
-- 2. FIX: Users insertion logic with proper error reporting
-- 3. FACT: ApplicationDocuments migrated from MMGTDBCOPY (source has data)
-- 4. FACT: ReviewHistory has 31K+ records (need to validate FKs post-migration)
-- ============================================================================

USE MMGTDB2A
GO

PRINT '═══════════════════════════════════════════════════════════════════'
PRINT 'PHASE 2: FINAL MIGRATION v3 - MMGTDBCOPY → MMGTDB2A'
PRINT '═══════════════════════════════════════════════════════════════════'
PRINT 'Dataset size: 13,789 applications + 31,135 reviews + ~100K+ documents expected'
GO

-- ============================================================================
-- SECTION 1: PRE-MIGRATION VALIDATION
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 1: PRE-MIGRATION VALIDATION ==='
GO

PRINT '1.1: Verify MMGTDBCOPY accessible...'
BEGIN TRY
	DECLARE @SourceCount INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments)
	PRINT '✓ MMGTDBCOPY accessible - ApplicationDocuments: ' + CAST(@SourceCount AS NVARCHAR(15)) + ' records'
END TRY
BEGIN CATCH
	RAISERROR('Cannot access source database', 16, 1)
END CATCH
GO

PRINT '1.2: Verify target schema ready...'
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
			   WHERE TABLE_NAME='ApplicationDocuments' AND COLUMN_NAME='IsLegacy')
	RAISERROR('IsLegacy column missing', 16, 1)
PRINT '✓ MMGTDB2A schema verified'
GO

PRINT '1.3: Source record counts:'
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
-- SECTION 2: PREPARE TARGET - CLEAR AND REBUILD
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 2: PREPARE TARGET TABLES ==='
PRINT '2.1: Disabling FK constraints...'

ALTER TABLE MMGTDB2A.dbo.ApplicationDocuments NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.ApplicationReviewHistory NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Applications NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.Users NOCHECK CONSTRAINT ALL
ALTER TABLE MMGTDB2A.dbo.PMICompanies NOCHECK CONSTRAINT ALL

PRINT '✓ FK constraints disabled'
GO

PRINT '2.2: Clearing existing data...'

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
-- SECTION 3: MIGRATE DATA IN DEPENDENCY ORDER
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 3: MIGRATE DATA ==='

-- 3.1: PMICompanies (no dependencies)
PRINT CHAR(10) + '3.1: Migrating PMICompanies (dependency-free)...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.PMICompanies (
		Id, PmiName, EmailAddress, ResourcePersonName, ResourcePersonEmail, 
		PhoneNumber, Status, CreatedAt, UpdatedAt
	)
	SELECT 
		Id, PmiName, EmailAddress, ResourcePersonName, ResourcePersonEmail,
		PhoneNumber, Status, CreatedAt, UpdatedAt
	FROM MMGTDBCOPY.dbo.PMICompanies

	PRINT '✓ PMICompanies migrated: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR PMICompanies: ' + ERROR_MESSAGE()
END CATCH
GO

-- 3.2: Users (depends on PMICompanies)
PRINT CHAR(10) + '3.2: Migrating Users...'
BEGIN TRY
	DECLARE @UsersBefore INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Users)

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

	DECLARE @UsersInserted INT = @@ROWCOUNT
	PRINT '✓ Users inserted: ' + CAST(@UsersInserted AS NVARCHAR(10)) + ' rows'

	-- Check for orphaned FK references
	DECLARE @OrphanedFK INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Users 
								WHERE PMICompanyId IS NOT NULL 
								AND NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.PMICompanies p WHERE p.Id = Users.PMICompanyId))
	IF @OrphanedFK > 0
		PRINT '⚠ Warning: ' + CAST(@OrphanedFK AS NVARCHAR(10)) + ' Users have invalid PMICompanyId FK'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR Users: ' + ERROR_MESSAGE()

	-- Diagnostic: Show first failed record
	PRINT 'Attempting to identify problematic record...'
	SELECT TOP 1 * FROM MMGTDBCOPY.dbo.Users ORDER BY CreatedAt
END CATCH
GO

-- 3.3: Applications (depends on Users and PMICompanies)
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

	PRINT '✓ Applications migrated: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' rows'

	-- Check FK integrity
	DECLARE @InvalidUserFk INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Applications 
								   WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = Applications.UserId))
	DECLARE @InvalidPmiFk INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Applications 
								  WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.PMICompanies p WHERE p.Id = Applications.PmiCompanyId))

	IF @InvalidUserFk > 0
		PRINT '⚠ Warning: ' + CAST(@InvalidUserFk AS NVARCHAR(10)) + ' Applications have invalid UserId FK'
	IF @InvalidPmiFk > 0
		PRINT '⚠ Warning: ' + CAST(@InvalidPmiFk AS NVARCHAR(10)) + ' Applications have invalid PmiCompanyId FK'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR Applications: ' + ERROR_MESSAGE()
END CATCH
GO

-- 3.4: BUILD DOCUMENTTYPE MAPPING - CRITICAL FIX
PRINT CHAR(10) + '3.4: Building DocumentType mapping...'
BEGIN TRY
	-- Get unique source DocumentTypes and their counts
	SELECT DISTINCT 
		ad.DocumentType as SourceType
	INTO #UniqueDocTypes
	FROM MMGTDBCOPY.dbo.ApplicationDocuments ad

	PRINT 'Unique DocumentTypes in source:'
	SELECT * FROM #UniqueDocTypes

	-- Create mapping table with both Code and Name matching
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
		(LOWER(uddt.SourceType) = LOWER(dt.Code) 
		 OR LOWER(uddt.SourceType) = LOWER(dt.Name)
		 OR LOWER(uddt.SourceType) LIKE '%' + LOWER(dt.Code) + '%'
		 OR LOWER(uddt.SourceType) LIKE '%' + LOWER(dt.Name) + '%')
	GROUP BY uddt.SourceType, dt.Id, dt.Code, dt.Name

	PRINT CHAR(10) + 'DocumentType mapping results:'
	SELECT * FROM #DocTypeMapping ORDER BY DocCount DESC

	-- Check for unmapped
	DECLARE @UnmappedCount INT = (SELECT COUNT(*) FROM #DocTypeMapping WHERE TargetTypeId IS NULL)
	IF @UnmappedCount > 0
	BEGIN
		PRINT CHAR(10) + '⚠ Warning: ' + CAST(@UnmappedCount AS NVARCHAR(10)) + ' DocumentTypes could not be mapped'

		-- Find default type (most common or 'identity_document')
		DECLARE @DefaultTypeId UNIQUEIDENTIFIER = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes 
			WHERE LOWER(Code) = 'identity_document'
		)
		IF @DefaultTypeId IS NULL
			SET @DefaultTypeId = (SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes ORDER BY Id)

		PRINT 'Assigning unmapped to default DocumentType: ' + CAST(@DefaultTypeId AS NVARCHAR(36))

		UPDATE #DocTypeMapping 
		SET TargetTypeId = @DefaultTypeId
		WHERE TargetTypeId IS NULL
	END

	PRINT '✓ DocumentType mapping ready'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR building mapping: ' + ERROR_MESSAGE()
END CATCH
GO

-- 3.5: MIGRATE APPLICATIONDOCUMENTS - MAIN EVENT
PRINT CHAR(10) + '3.5: Migrating ApplicationDocuments (this may take a moment)...'
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
		NULL as FileContent,                    -- Legacy documents are disk-based
		ad.FileType,
		ISNULL(dtm.TargetTypeId,               -- Use mapping result
			(SELECT TOP 1 Id FROM MMGTDB2A.dbo.DocumentTypes ORDER BY Id)) as DocumentTypeId,
		ad.DocumentType as LegacyDocumentType,  -- Preserve original type string
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
		1 as IsLegacy                           -- CRITICAL: Mark all migrated as legacy
	FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
	LEFT JOIN #DocTypeMapping dtm ON ad.DocumentType = dtm.SourceType

	DECLARE @DocsInserted INT = @@ROWCOUNT
	PRINT '✓ ApplicationDocuments migrated: ' + CAST(@DocsInserted AS NVARCHAR(10)) + ' rows'

	-- Validate DocumentTypeId is NOT NULL
	DECLARE @NullTypeCount INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments WHERE DocumentTypeId IS NULL)
	IF @NullTypeCount > 0
		PRINT '⚠ WARNING: ' + CAST(@NullTypeCount AS NVARCHAR(10)) + ' documents have NULL DocumentTypeId (constraint violation risk)'

	-- Validate IsLegacy flag
	DECLARE @NonLegacyCount INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments WHERE IsLegacy = 0)
	IF @NonLegacyCount > 0
		PRINT '⚠ WARNING: ' + CAST(@NonLegacyCount AS NVARCHAR(10)) + ' documents marked as non-legacy'

END TRY
BEGIN CATCH
	PRINT '✗ ERROR ApplicationDocuments: ' + ERROR_MESSAGE()
END CATCH
GO

-- Clean up temp tables
IF OBJECT_ID('tempdb..#DocTypeMapping') IS NOT NULL
	DROP TABLE #DocTypeMapping
IF OBJECT_ID('tempdb..#UniqueDocTypes') IS NOT NULL
	DROP TABLE #UniqueDocTypes
GO

-- 3.6: Migrate ApplicationReviewHistory (depends on Applications and Users)
PRINT CHAR(10) + '3.6: Migrating ApplicationReviewHistory...'
BEGIN TRY
	INSERT INTO MMGTDB2A.dbo.ApplicationReviewHistory (
		Id, Status, Comments, CreatedAt, ApplicationId, ReviewedById
	)
	SELECT 
		Id, Status, Comments, CreatedAt, ApplicationId, ReviewedById
	FROM MMGTDBCOPY.dbo.ApplicationReviewHistory

	DECLARE @HistoryInserted INT = @@ROWCOUNT
	PRINT '✓ ApplicationReviewHistory migrated: ' + CAST(@HistoryInserted AS NVARCHAR(10)) + ' rows'

	-- Check FK integrity
	DECLARE @InvalidAppFk INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory 
								  WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Applications a WHERE a.Id = ApplicationReviewHistory.ApplicationId))
	DECLARE @InvalidUserFkHistory INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory 
										 WHERE ReviewedById IS NOT NULL 
										 AND NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = ApplicationReviewHistory.ReviewedById))

	IF @InvalidAppFk > 0
		PRINT '⚠ Warning: ' + CAST(@InvalidAppFk AS NVARCHAR(10)) + ' ReviewHistory records have invalid ApplicationId FK'
	IF @InvalidUserFkHistory > 0
		PRINT '⚠ Warning: ' + CAST(@InvalidUserFkHistory AS NVARCHAR(10)) + ' ReviewHistory records have invalid ReviewedById FK'
END TRY
BEGIN CATCH
	PRINT '✗ ERROR ApplicationReviewHistory: ' + ERROR_MESSAGE()
END CATCH
GO

-- ============================================================================
-- SECTION 4: RE-ENABLE FK CONSTRAINTS
-- ============================================================================
PRINT CHAR(10) + '=== SECTION 4: RE-ENABLE CONSTRAINTS ==='
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

PRINT CHAR(10) + '5.1: RECORD COUNTS COMPARISON:'
PRINT 'Table                       | Migrated | Source | Status'
PRINT '─────────────────────────────────────────────────────────'

DECLARE @MigratedUsers INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Users)
DECLARE @SourceUsers INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.Users)
PRINT 'Users                       | ' + RIGHT('    ' + CAST(@MigratedUsers AS VARCHAR(5)), 8) + ' | ' + RIGHT('    ' + CAST(@SourceUsers AS VARCHAR(5)), 6) + ' | ' + 
	  CASE WHEN @MigratedUsers = @SourceUsers THEN '✓ MATCH' ELSE '✗ MISMATCH' END

DECLARE @MigratedPMI INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.PMICompanies)
DECLARE @SourcePMI INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.PMICompanies)
PRINT 'PMICompanies                | ' + RIGHT('    ' + CAST(@MigratedPMI AS VARCHAR(5)), 8) + ' | ' + RIGHT('    ' + CAST(@SourcePMI AS VARCHAR(5)), 6) + ' | ' + 
	  CASE WHEN @MigratedPMI = @SourcePMI THEN '✓ MATCH' ELSE '✗ MISMATCH' END

DECLARE @MigratedApps INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Applications)
DECLARE @SourceApps INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.Applications)
PRINT 'Applications                | ' + RIGHT('    ' + CAST(@MigratedApps AS VARCHAR(6)), 9) + ' | ' + RIGHT('     ' + CAST(@SourceApps AS VARCHAR(6)), 7) + ' | ' + 
	  CASE WHEN @MigratedApps = @SourceApps THEN '✓ MATCH' ELSE '✗ MISMATCH' END

DECLARE @MigratedDocs INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments)
DECLARE @SourceDocs INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments)
PRINT 'ApplicationDocuments        | ' + RIGHT('    ' + CAST(@MigratedDocs AS VARCHAR(6)), 9) + ' | ' + RIGHT('     ' + CAST(@SourceDocs AS VARCHAR(6)), 7) + ' | ' + 
	  CASE WHEN @MigratedDocs = @SourceDocs THEN '✓ MATCH' ELSE '✗ MISMATCH' END

DECLARE @MigratedHistory INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory)
DECLARE @SourceHistory INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationReviewHistory)
PRINT 'ApplicationReviewHistory    | ' + RIGHT('    ' + CAST(@MigratedHistory AS VARCHAR(6)), 9) + ' | ' + RIGHT('     ' + CAST(@SourceHistory AS VARCHAR(6)), 7) + ' | ' + 
	  CASE WHEN @MigratedHistory = @SourceHistory THEN '✓ MATCH' ELSE '✗ MISMATCH' END
GO

PRINT CHAR(10) + '5.2: DATA QUALITY CHECKS:'
PRINT 'ApplicationDocuments - IsLegacy distribution:'
SELECT IsLegacy, COUNT(*) as Count FROM MMGTDB2A.dbo.ApplicationDocuments GROUP BY IsLegacy
GO

PRINT CHAR(10) + 'ApplicationDocuments - DocumentTypeId NULL check:'
SELECT 
	CASE WHEN DocumentTypeId IS NULL THEN 'NULL (ERROR)' ELSE 'Non-NULL' END as Status,
	COUNT(*) as Count
FROM MMGTDB2A.dbo.ApplicationDocuments
GROUP BY CASE WHEN DocumentTypeId IS NULL THEN 'NULL (ERROR)' ELSE 'Non-NULL' END
GO

PRINT CHAR(10) + '5.3: FOREIGN KEY INTEGRITY:'
SELECT 
	'Applications with valid UserId' as Check_Name,
	COUNT(*) as Count
FROM MMGTDB2A.dbo.Applications a
WHERE EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)

UNION ALL

SELECT 
	'Applications with INVALID UserId',
	COUNT(*)
FROM MMGTDB2A.dbo.Applications a
WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)
GO

PRINT CHAR(10) + '=== MIGRATION COMPLETE ==='
PRINT 'Review validation results above. Any ✗ MISMATCH or ERROR requires investigation.'
GO
