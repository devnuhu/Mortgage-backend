-- ============================================================================
-- DIAGNOSTIC: Understand DocumentType mapping in detail
-- ============================================================================

USE MMGTDB2A
GO

PRINT '=== DOCUMENTTYPE MAPPING ANALYSIS ==='
PRINT ''

-- 1. What are the source DocumentType values?
PRINT '1. UNIQUE DOCUMENTTYPES IN SOURCE (MMGTDBCOPY):'
SELECT DISTINCT 
	ad.DocumentType,
	COUNT(*) as DocumentCount,
	MIN(ad.CreatedAt) as FirstCreated,
	MAX(ad.CreatedAt) as LastCreated
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
GROUP BY ad.DocumentType
ORDER BY DocumentCount DESC
GO

-- 2. What DocumentTypes are available in target?
PRINT CHAR(10) + '2. AVAILABLE DOCUMENTTYPES IN TARGET (MMGTDB2A):'
SELECT 
	Id,
	Code,
	Name,
	Description,
	IsMandatory,
	MaxFileSizeKB,
	DisplayOrder,
	IsActive
FROM MMGTDB2A.dbo.DocumentTypes
ORDER BY DisplayOrder, Code
GO

-- 3. Try to match each source type to target
PRINT CHAR(10) + '3. SOURCE-TO-TARGET MAPPING ATTEMPTS:'
SELECT DISTINCT
	ad.DocumentType as SourceType,
	-- Try exact Code match
	(SELECT TOP 1 Code + ' (exact match)' 
	 FROM MMGTDB2A.dbo.DocumentTypes dt 
	 WHERE LOWER(ad.DocumentType) = LOWER(dt.Code)) as ExactCodeMatch,
	-- Try exact Name match
	(SELECT TOP 1 Name + ' (exact match)' 
	 FROM MMGTDB2A.dbo.DocumentTypes dt 
	 WHERE LOWER(ad.DocumentType) = LOWER(dt.Name)) as ExactNameMatch,
	-- Try partial Code match
	(SELECT TOP 1 Code + ' (partial match)' 
	 FROM MMGTDB2A.dbo.DocumentTypes dt 
	 WHERE LOWER(ad.DocumentType) LIKE '%' + LOWER(dt.Code) + '%') as PartialCodeMatch,
	-- Try partial Name match
	(SELECT TOP 1 Name + ' (partial match)' 
	 FROM MMGTDB2A.dbo.DocumentTypes dt 
	 WHERE LOWER(ad.DocumentType) LIKE '%' + LOWER(dt.Name) + '%') as PartialNameMatch,
	COUNT(*) as SourceDocumentCount
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
GROUP BY ad.DocumentType
ORDER BY SourceDocumentCount DESC
GO

-- 4. Manual mapping suggestion
PRINT CHAR(10) + '4. RECOMMENDED MANUAL MAPPING (if needed):'
PRINT 'If script mapping fails, manually map these:'
SELECT DISTINCT
	ad.DocumentType as SourceType,
	COUNT(*) as Count,
	'MUST BE ASSIGNED TO:', 
	dt.Id as SuggestedTargetId,
	dt.Code as SuggestedCode,
	dt.Name as SuggestedName
FROM MMGTDBCOPY.dbo.ApplicationDocuments ad
CROSS JOIN (SELECT TOP 1 * FROM MMGTDB2A.dbo.DocumentTypes ORDER BY DisplayOrder) dt
GROUP BY ad.DocumentType, dt.Id, dt.Code, dt.Name
ORDER BY Count DESC
GO

-- 5. Show what WILL happen in migration if auto-mapping fails
PRINT CHAR(10) + '5. IF AUTO-MAPPING FAILS, THIS WILL BECOME DEFAULT:'
SELECT TOP 1
	Id,
	Code,
	Name,
	'ALL UNMAPPED DOCUMENTS WILL BE ASSIGNED HERE' as Impact
FROM MMGTDB2A.dbo.DocumentTypes
ORDER BY DisplayOrder, Id
GO

PRINT CHAR(10) + '=== END MAPPING ANALYSIS ==='
