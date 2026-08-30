-- ============================================================================
-- DIAGNOSTIC SCRIPT: Why did only PMI and Applications migrate?
-- ============================================================================

USE MMGTDB2A
GO

PRINT '=== PHASE 2 MIGRATION DIAGNOSTIC ==='
PRINT CHAR(10) + '1. TARGET DATABASE CURRENT COUNTS:'
SELECT 
	'Users' as [Table], COUNT(*) as Records FROM MMGTDB2A.dbo.Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM MMGTDB2A.dbo.PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM MMGTDB2A.dbo.Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM MMGTDB2A.dbo.ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM MMGTDB2A.dbo.ApplicationReviewHistory
GO

PRINT CHAR(10) + '2. SOURCE DATABASE COUNTS (MMGTDBCOPY):'
SELECT 
	'Users' as [Table], COUNT(*) as Records FROM MMGTDBCOPY.dbo.Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM MMGTDBCOPY.dbo.PMICompanies
UNION ALL
SELECT 'Applications', COUNT(*) FROM MMGTDBCOPY.dbo.Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM MMGTDBCOPY.dbo.ApplicationReviewHistory
GO

PRINT CHAR(10) + '3. SAMPLE MIGRATED USERS:'
SELECT TOP 5 Id, Email, FirstName, LastName, Role, CreatedAt 
FROM MMGTDB2A.dbo.Users
GO

PRINT CHAR(10) + '4. SAMPLE MIGRATED APPLICATIONS:'
SELECT TOP 5 Id, ApplicationNumber, FirstName, LastName, Status, CreatedAt 
FROM MMGTDB2A.dbo.Applications
GO

PRINT CHAR(10) + '5. VERIFY DocumentTypes EXIST:'
SELECT COUNT(*) as DocumentTypeCount FROM MMGTDB2A.dbo.DocumentTypes
SELECT TOP 10 Id, Code, Name, MaxFileSizeKB FROM MMGTDB2A.dbo.DocumentTypes
GO

PRINT CHAR(10) + '6. CHECK FOR ERRORS IN APPLICATION TABLE:'
-- Check if Applications.UserId references exist
SELECT COUNT(*) as AppsWithValidUserFK 
FROM MMGTDB2A.dbo.Applications a
WHERE EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)
GO

SELECT COUNT(*) as AppsWithInvalidUserFK
FROM MMGTDB2A.dbo.Applications a
WHERE NOT EXISTS (SELECT 1 FROM MMGTDB2A.dbo.Users u WHERE u.Id = a.UserId)
GO

PRINT CHAR(10) + '7. SOURCE APPLICATION DOCUMENT TYPES:'
SELECT DISTINCT DocumentType FROM MMGTDBCOPY.dbo.ApplicationDocuments
ORDER BY DocumentType
GO

PRINT CHAR(10) + '8. TARGET AVAILABLE DOCUMENT TYPE CODES:'
SELECT DISTINCT Code FROM MMGTDB2A.dbo.DocumentTypes
ORDER BY Code
GO
