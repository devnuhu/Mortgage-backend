-- ============================================================================
-- ADD LEGACY DOCUMENT TYPE: identity_document
-- ============================================================================
-- Purpose: Create a DocumentType entry for legacy on-disk documents
-- This ensures all migrated documents have a valid DocumentTypeId reference
-- CreatedBy must be set to a SUPER_ADMIN user (required FK)
-- IsMandatory: 0 (optional document type)
-- ============================================================================

USE MMGTDB2A
GO

PRINT '=== ADDING LEGACY DOCUMENTTYPE: identity_document ==='
PRINT ''

-- Check if identity_document already exists
DECLARE @ExistingId UNIQUEIDENTIFIER = (
	SELECT Id FROM MMGTDB2A.dbo.DocumentTypes 
	WHERE LOWER(Code) = 'identity_document' 
	AND LOWER(Name) = 'identity_document'
)

IF @ExistingId IS NOT NULL
BEGIN
	PRINT '✓ identity_document DocumentType already exists'
	PRINT '  Id: ' + CAST(@ExistingId AS NVARCHAR(36))
	PRINT '  Skipping insert'
END
ELSE
BEGIN
	PRINT '⭕ identity_document DocumentType does not exist - creating...'
	PRINT ''

	-- ========================================================================
	-- STEP 1: Find SUPER_ADMIN user for CreatedBy (FK requirement)
	-- ========================================================================
	-- The DocumentTypes table requires CreatedBy to reference Users(Id)
	-- We try multiple fallback strategies to find an admin user

	DECLARE @SuperAdminId UNIQUEIDENTIFIER = (
		SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
		WHERE LOWER(Role) = 'super_admin' 
		AND LOWER(Email) = 'superadmin@pfa.com'
		AND IsActive = 1
	)

	PRINT '  Searching for SUPER_ADMIN user...'

	-- Fallback 1: Get any SUPER_ADMIN role user
	IF @SuperAdminId IS NULL
	BEGIN
		PRINT '    Fallback 1: Searching for any SUPER_ADMIN role user...'
		SET @SuperAdminId = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
			WHERE LOWER(Role) = 'super_admin'
			AND IsActive = 1
			ORDER BY CreatedAt DESC
		)
	END

	-- Fallback 2: Get any ADMIN role user
	IF @SuperAdminId IS NULL
	BEGIN
		PRINT '    Fallback 2: Searching for any ADMIN role user...'
		SET @SuperAdminId = (
			SELECT TOP 1 Id FROM MMGTDB2A.dbo.Users 
			WHERE LOWER(Role) = 'admin'
			AND IsActive = 1
			ORDER BY CreatedAt DESC
		)
	END

	-- If still no user found, raise error
	IF @SuperAdminId IS NULL
	BEGIN
		PRINT ''
		PRINT '✗ ERROR: Cannot proceed - No SUPER_ADMIN or ADMIN user found'
		PRINT '  Required: At least one active user with SUPER_ADMIN or ADMIN role'
		PRINT ''
		PRINT 'ACTION REQUIRED:'
		PRINT '  1. Run DbSeeder.SeedAsync() from Program.cs startup'
		PRINT '  2. Or manually insert an admin user into the Users table'
		PRINT '  3. Then retry this script'
		PRINT ''
		RAISERROR('Cannot create DocumentType without admin user', 16, 1)
	END

	PRINT '  ✓ Found CreatedBy user: ' + CAST(@SuperAdminId AS NVARCHAR(36))
	PRINT ''

	-- ========================================================================
	-- STEP 2: Get max DisplayOrder and create new DocumentType
	-- ========================================================================

	DECLARE @MaxDisplayOrder INT = (SELECT ISNULL(MAX(DisplayOrder), 0) FROM MMGTDB2A.dbo.DocumentTypes)
	DECLARE @NewId UNIQUEIDENTIFIER = NEWID()

	INSERT INTO MMGTDB2A.dbo.DocumentTypes (
		Id,
		Code,
		Name,
		Description,
		IsMandatory,
		DisplayOrder,
		MaxFileSizeKB,
		IsActive,
		CreatedAt,
		CreatedBy
	)
	VALUES (
		@NewId,
		'identity_document',
		'identity_document',
		'Legacy on-disk document type for migrated records',
		0,                              -- IsMandatory = 0 (optional)
		@MaxDisplayOrder + 1,           -- DisplayOrder = next available
		300,                            -- MaxFileSizeKB = 300
		1,                              -- IsActive = 1 (enabled)
		GETDATE(),                      -- CreatedAt = now
		@SuperAdminId                   -- CreatedBy = SUPER_ADMIN user
	)

	PRINT '✓ Created identity_document DocumentType'
	PRINT '  Id: ' + CAST(@NewId AS NVARCHAR(36))
	PRINT '  Code: identity_document'
	PRINT '  Name: identity_document'
	PRINT '  IsMandatory: 0'
	PRINT '  DisplayOrder: ' + CAST(@MaxDisplayOrder + 1 AS NVARCHAR(10))
	PRINT '  MaxFileSizeKB: 300'
	PRINT '  IsActive: 1'
	PRINT '  CreatedBy: ' + CAST(@SuperAdminId AS NVARCHAR(36))
END

GO

-- ============================================================================
-- VERIFY DOCUMENTTYPE EXISTS AND IS READY FOR MIGRATION
-- ============================================================================
PRINT CHAR(10) + '=== VERIFICATION ==='
PRINT ''

DECLARE @DocTypeId UNIQUEIDENTIFIER = (
	SELECT Id FROM MMGTDB2A.dbo.DocumentTypes 
	WHERE LOWER(Code) = 'identity_document'
)

IF @DocTypeId IS NOT NULL
BEGIN
	PRINT '✓ DocumentType ready for migration'
	PRINT ''
	PRINT 'Complete DocumentType details:'
	PRINT '========================================'
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
		CreatedBy
	FROM MMGTDB2A.dbo.DocumentTypes
	WHERE LOWER(Code) = 'identity_document'
END
ELSE
BEGIN
	PRINT '✗ ERROR: identity_document DocumentType still not found'
	RAISERROR('Failed to create identity_document DocumentType', 16, 1)
END

GO

-- Show summary
PRINT CHAR(10) + '=== SUMMARY ==='
PRINT 'All DocumentTypes in MMGTDB2A:'
PRINT '========================================'
SELECT 
	COUNT(*) as TotalDocumentTypes,
	SUM(CASE WHEN Code = 'identity_document' THEN 1 ELSE 0 END) as identity_document_Count
FROM MMGTDB2A.dbo.DocumentTypes

GO
