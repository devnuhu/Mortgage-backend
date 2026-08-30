-- ============================================================================
-- DIAGNOSTIC: Understand why v1 migration only partially succeeded
-- ============================================================================

USE MMGTDB2A
GO

PRINT '=== PHASE 2 MIGRATION DIAGNOSTICS ==='
PRINT 'Analyzing current state after v1 migration attempt'
PRINT ''

-- 1. Current counts
PRINT CHAR(10) + '1. CURRENT RECORD COUNTS (MMGTDB2A):' 
SELECT 
	'Users' as [Table], 
	COUNT(*) as Records,
	'Expected from source: 2' as Expected
FROM MMGTDB2A.dbo.Users

UNION ALL

SELECT 
	'PMICompanies', 
	COUNT(*), 
	'Expected: 2'
FROM MMGTDB2A.dbo.PMICompanies

UNION ALL

SELECT 
	'Applications', 
	COUNT(*), 
	'Expected: 2'
FROM MMGTDB2A.dbo.Applications

UNION ALL

SELECT 
	'ApplicationDocuments', 
	COUNT(*), 
	'Expected: ~132'
FROM MMGTDB2A.dbo.ApplicationDocuments

UNION ALL

SELECT 
	'ApplicationReviewHistory', 
	COUNT(*), 
	'Expected: ~56'
FROM MMGTDB2A.dbo.ApplicationReviewHistory
GO

-- 2. If Users exist, check them
PRINT CHAR(10) + '2. SAMPLE MIGRATED USERS:'
IF (SELECT COUNT(*) FROM MMGTDB2A.dbo.Users) > 0
BEGIN
	SELECT TOP 5 
		Id, 
		Email, 
		FirstName, 
		LastName, 
		Role, 
		PMICompanyId,
		CreatedAt
	FROM MMGTDB2A.dbo.Users
	ORDER BY CreatedAt
END
ELSE
BEGIN
	PRINT '   (No users migrated)'
END
GO

-- 3. If Applications exist, check for orphaned FKs
PRINT CHAR(10) + '3. APPLICATION FK STATUS:'
IF (SELECT COUNT(*) FROM MMGTDB2A.dbo.Applications) > 0
BEGIN
	SELECT 
		'Applications with valid UserId FK' as Status,
		COUNT(*) as Count
	FROM MMGTDB2A.dbo.Applications a
	WHERE EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)

	UNION ALL

	SELECT 
		'Applications with INVALID UserId FK (orphaned)',
		COUNT(*)
	FROM MMGTDB2A.dbo.Applications a
	WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)

	UNION ALL

	SELECT 
		'Applications with valid PmiCompanyId FK',
		COUNT(*)
	FROM MMGTDB2A.dbo.Applications a
	WHERE EXISTS (SELECT 1 FROM MMGTDB2A.dbo.PMICompanies p WHERE p.Id = a.PmiCompanyId)

	UNION ALL

	SELECT 
		'Applications with INVALID PmiCompanyId FK',
		COUNT(*)
	FROM MMGTDB2A.dbo.Applications a
	WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.PMICompanies p WHERE p.Id = a.PmiCompanyId)
END
ELSE
BEGIN
	PRINT '   (No applications migrated)'
END
GO

-- 4. Check ApplicationDocuments
PRINT CHAR(10) + '4. APPLICATIONDOCUMENTS STATUS:'
IF (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments) > 0
BEGIN
	PRINT 'DocumentTypeId distribution:'
	SELECT 
		CASE WHEN DocumentTypeId IS NULL THEN 'NULL (ERROR)' ELSE CAST(DocumentTypeId AS NVARCHAR(36)) END as DocumentTypeId,
		COUNT(*) as Count
	FROM MMGTDB2A.dbo.ApplicationDocuments
	GROUP BY DocumentTypeId
	ORDER BY Count DESC

	PRINT CHAR(10) + 'IsLegacy flag distribution:'
	SELECT 
		IsLegacy,
		COUNT(*) as Count
	FROM MMGTDB2A.dbo.ApplicationDocuments
	GROUP BY IsLegacy

	PRINT CHAR(10) + 'Sample documents:'
	SELECT TOP 10 
		Id,
		FileName,
		FileType,
		DocumentTypeId,
		LegacyDocumentType,
		IsLegacy,
		CreatedAt
	FROM MMGTDB2A.dbo.ApplicationDocuments
	ORDER BY CreatedAt
END
ELSE
BEGIN
	PRINT '   (No documents migrated)'
END
GO

-- 5. Compare with source
PRINT CHAR(10) + '5. SOURCE (MMGTDBCOPY) RECORD COUNTS:'
SELECT 
	'Users' as [Table], 
	COUNT(*) as Records
FROM MMGTDBCOPY.dbo.Users

UNION ALL

SELECT 
	'PMICompanies', 
	COUNT(*)
FROM MMGTDBCOPY.dbo.PMICompanies

UNION ALL

SELECT 
	'Applications', 
	COUNT(*)
FROM MMGTDBCOPY.dbo.Applications

UNION ALL

SELECT 
	'ApplicationDocuments', 
	COUNT(*)
FROM MMGTDBCOPY.dbo.ApplicationDocuments

UNION ALL

SELECT 
	'ApplicationReviewHistory', 
	COUNT(*)
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory
GO

-- 6. Check DocumentTypes in target
PRINT CHAR(10) + '6. AVAILABLE DOCUMENTTYPES IN TARGET:'
SELECT TOP 20
	Id,
	Code,
	Name,
	Description,
	IsMandatory,
	MaxFileSizeKB,
	IsActive
FROM MMGTDB2A.dbo.DocumentTypes
ORDER BY DisplayOrder, Name
GO

-- 7. Check source ApplicationDocuments.DocumentType values
PRINT CHAR(10) + '7. SOURCE APPLICATIONDOCUMENTS.DOCUMENTTYPE VALUES:'
SELECT DISTINCT 
	DocumentType,
	COUNT(*) as Count
FROM MMGTDBCOPY.dbo.ApplicationDocuments
GROUP BY DocumentType
ORDER BY Count DESC
GO

-- 8. Try to map source DocumentTypes to target
PRINT CHAR(10) + '8. DOCUMENTTYPE MAPPING CHECK:'
SELECT DISTINCT
	sad.DocumentType as SourceType,
	sdt.Code as SourceCode,
	tdt.Code as TargetMatchCode,
	tdt.Id as TargetId,
	COUNT(sad.Id) OVER (PARTITION BY sad.DocumentType) as SourceCount
FROM MMGTDBCOPY.dbo.ApplicationDocuments sad
LEFT JOIN MMGTDB2A.dbo.DocumentTypes tdt ON 
	LOWER(sad.DocumentType) = LOWER(tdt.Code) 
	OR LOWER(sad.DocumentType) = LOWER(tdt.Name)
ORDER BY SourceCount DESC, SourceType
GO

-- 9. Summary diagnosis
PRINT CHAR(10) + '=== DIAGNOSIS SUMMARY ==='
DECLARE @TargetUsers INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Users)
DECLARE @TargetPMI INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.PMICompanies)
DECLARE @TargetApps INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.Applications)
DECLARE @TargetDocs INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments)
DECLARE @TargetHistory INT = (SELECT COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory)
DECLARE @SourceUsers INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.Users)
DECLARE @SourcePMI INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.PMICompanies)
DECLARE @SourceApps INT = (SELECT COUNT(*) FROM MMGTDBCOPY.dbo.Applications)

PRINT 'Users:      ' + CAST(@TargetUsers AS NVARCHAR(5)) + '/' + CAST(@SourceUsers AS NVARCHAR(5)) + ' migrated'
PRINT 'PMI:        ' + CAST(@TargetPMI AS NVARCHAR(5)) + '/' + CAST(@SourcePMI AS NVARCHAR(5)) + ' migrated'
PRINT 'Apps:       ' + CAST(@TargetApps AS NVARCHAR(5)) + '/' + CAST(@SourceApps AS NVARCHAR(5)) + ' migrated'
PRINT 'Docs:       ' + CAST(@TargetDocs AS NVARCHAR(5)) + ' migrated'
PRINT 'History:    ' + CAST(@TargetHistory AS NVARCHAR(5)) + ' migrated'
PRINT ''

IF @TargetUsers = 0
	PRINT '⚠ ISSUE: Users not migrated - likely constraint or data quality issue'
IF @TargetDocs = 0
	PRINT '⚠ ISSUE: ApplicationDocuments not migrated - likely DocumentTypeId mapping failed'
IF @TargetHistory = 0
	PRINT '⚠ ISSUE: ReviewHistory not migrated - likely FK constraint issues'
IF @TargetApps > 0 AND @TargetUsers = 0
	PRINT '⚠ ISSUE: Applications migrated but Users not - FKs are orphaned'

PRINT ''
PRINT 'RECOMMENDATION: Run Analysis/Phase2_MigrationExecution_v2.sql to retry with better error handling'
GO
