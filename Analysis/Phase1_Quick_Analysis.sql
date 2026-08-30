-- ==================================================================================
-- FOCUSED: Table and Column Comparison
-- ==================================================================================

USE MMGTDBCOPY;
GO

PRINT '==================================================';
PRINT 'MMGTDBCOPY - TABLE LISTING';
PRINT '==================================================';

SELECT 
	TABLE_NAME,
	(SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = t.TABLE_NAME) AS ColumnCount
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

PRINT '';
PRINT '==================================================';
PRINT 'MMGTDB2A - TABLE LISTING';
PRINT '==================================================';

USE MMGTDB2A;
GO

SELECT 
	TABLE_NAME,
	(SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = t.TABLE_NAME) AS ColumnCount
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

PRINT '';
PRINT '==================================================';
PRINT 'RECORD COUNT COMPARISON';
PRINT '==================================================';

USE MMGTDBCOPY;
GO

SELECT 
	t.name AS TableName,
	s.row_count AS RowCount
FROM sys.tables t
INNER JOIN sys.dm_db_partition_stats s ON t.object_id = s.object_id AND s.index_id < 2
ORDER BY t.name;

PRINT '';
PRINT '==================================================';
PRINT 'MMGTDB2A RECORD COUNTS';
PRINT '==================================================';

USE MMGTDB2A;
GO

SELECT 
	t.name AS TableName,
	s.row_count AS RowCount
FROM sys.tables t
INNER JOIN sys.dm_db_partition_stats s ON t.object_id = s.object_id AND s.index_id < 2
ORDER BY t.name;
