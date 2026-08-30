-- ==================================================================================
-- PHASE 1: LIVE DATA MIGRATION ANALYSIS
-- Source: MMGTDBCOPY (Production Live Data)
-- Target: MMGTDB2A (Latest Development Schema)
-- ==================================================================================
-- This script analyzes both databases to prepare for safe data migration
-- ==================================================================================

-- ==================================================================================
-- SECTION 1: DATABASE OVERVIEW
-- ==================================================================================

USE master;
GO

/* Check database status and sizes */
SELECT 
	DB_NAME(database_id) AS DatabaseName,
	CAST(SUM(size) * 8.0 / 1024 / 1024 AS DECIMAL(10,2)) AS SizeMB,
	CAST(SUM(CASE WHEN status & 64 = 0 THEN size ELSE 0 END) * 8.0 / 1024 / 1024 AS DECIMAL(10,2)) AS DataMB,
	CAST(SUM(CASE WHEN status & 64 <> 0 THEN size ELSE 0 END) * 8.0 / 1024 / 1024 AS DECIMAL(10,2)) AS LogMB
FROM sys.master_files
WHERE DB_NAME(database_id) IN ('MMGTDBCOPY', 'MMGTDB2A')
GROUP BY DB_NAME(database_id)
ORDER BY DB_NAME(database_id);
GO

-- ==================================================================================
-- SECTION 2: TABLE INVENTORY - SOURCE DATABASE (MMGTDBCOPY)
-- ==================================================================================

PRINT '=================================================='
PRINT 'SOURCE DATABASE: MMGTDBCOPY'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

SELECT 
	TABLE_SCHEMA,
	TABLE_NAME,
	(SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = t.TABLE_NAME AND TABLE_SCHEMA = t.TABLE_SCHEMA) AS ColumnCount
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME;
GO

-- ==================================================================================
-- SECTION 3: TABLE INVENTORY - TARGET DATABASE (MMGTDB2A)
-- ==================================================================================

PRINT '=================================================='
PRINT 'TARGET DATABASE: MMGTDB2A'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

SELECT 
	TABLE_SCHEMA,
	TABLE_NAME,
	(SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = t.TABLE_NAME AND TABLE_SCHEMA = t.TABLE_SCHEMA) AS ColumnCount
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME;
GO

-- ==================================================================================
-- SECTION 4: DETAILED COLUMN COMPARISON FOR EACH TABLE
-- ==================================================================================

PRINT '=================================================='
PRINT 'DETAILED TABLE STRUCTURE COMPARISON'
PRINT '=================================================='
GO

-- Detailed columns in source
USE MMGTDBCOPY;
GO

SELECT 
	'SOURCE' AS [Database],
	TABLE_NAME,
	COLUMN_NAME,
	ORDINAL_POSITION,
	DATA_TYPE,
	CHARACTER_MAXIMUM_LENGTH,
	NUMERIC_PRECISION,
	NUMERIC_SCALE,
	IS_NULLABLE,
	COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- Detailed columns in target
USE MMGTDB2A;
GO

SELECT 
	'TARGET' AS [Database],
	TABLE_NAME,
	COLUMN_NAME,
	ORDINAL_POSITION,
	DATA_TYPE,
	CHARACTER_MAXIMUM_LENGTH,
	NUMERIC_PRECISION,
	NUMERIC_SCALE,
	IS_NULLABLE,
	COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION;
GO

-- ==================================================================================
-- SECTION 5: PRIMARY KEY ANALYSIS
-- ==================================================================================

PRINT '=================================================='
PRINT 'PRIMARY KEY ANALYSIS - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

SELECT 
	t.TABLE_NAME,
	c.COLUMN_NAME,
	pk.CONSTRAINT_NAME,
	COLUMNPROPERTY(OBJECT_ID(t.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity,
	IDENT_SEED(t.TABLE_NAME, c.COLUMN_NAME) AS IdentitySeed,
	IDENT_INCR(t.TABLE_NAME, c.COLUMN_NAME) AS IdentityIncrement
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS pk
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE c ON pk.CONSTRAINT_NAME = c.CONSTRAINT_NAME
JOIN INFORMATION_SCHEMA.TABLES t ON c.TABLE_NAME = t.TABLE_NAME
WHERE pk.CONSTRAINT_TYPE = 'PRIMARY KEY'
	AND pk.TABLE_SCHEMA = 'dbo'
ORDER BY t.TABLE_NAME;
GO

PRINT '=================================================='
PRINT 'PRIMARY KEY ANALYSIS - TARGET (MMGTDB2A)'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

SELECT 
	t.TABLE_NAME,
	c.COLUMN_NAME,
	pk.CONSTRAINT_NAME,
	COLUMNPROPERTY(OBJECT_ID(t.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity,
	IDENT_SEED(t.TABLE_NAME, c.COLUMN_NAME) AS IdentitySeed,
	IDENT_INCR(t.TABLE_NAME, c.COLUMN_NAME) AS IdentityIncrement
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS pk
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE c ON pk.CONSTRAINT_NAME = c.CONSTRAINT_NAME
JOIN INFORMATION_SCHEMA.TABLES t ON c.TABLE_NAME = t.TABLE_NAME
WHERE pk.CONSTRAINT_TYPE = 'PRIMARY KEY'
	AND pk.TABLE_SCHEMA = 'dbo'
ORDER BY t.TABLE_NAME;
GO

-- ==================================================================================
-- SECTION 6: FOREIGN KEY RELATIONSHIPS
-- ==================================================================================

PRINT '=================================================='
PRINT 'FOREIGN KEY ANALYSIS - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

SELECT 
	KCU1.CONSTRAINT_NAME AS ForeignKeyName,
	KCU1.TABLE_NAME AS ForeignKeyTable,
	KCU1.COLUMN_NAME AS ForeignKeyColumn,
	KCU2.TABLE_NAME AS ReferencedTable,
	KCU2.COLUMN_NAME AS ReferencedColumn,
	RC.UPDATE_RULE,
	RC.DELETE_RULE
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU1 ON RC.CONSTRAINT_NAME = KCU1.CONSTRAINT_NAME
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU2 ON RC.UNIQUE_CONSTRAINT_NAME = KCU2.CONSTRAINT_NAME
WHERE KCU1.TABLE_SCHEMA = 'dbo'
ORDER BY KCU1.TABLE_NAME, KCU1.COLUMN_NAME;
GO

PRINT '=================================================='
PRINT 'FOREIGN KEY ANALYSIS - TARGET (MMGTDB2A)'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

SELECT 
	KCU1.CONSTRAINT_NAME AS ForeignKeyName,
	KCU1.TABLE_NAME AS ForeignKeyTable,
	KCU1.COLUMN_NAME AS ForeignKeyColumn,
	KCU2.TABLE_NAME AS ReferencedTable,
	KCU2.COLUMN_NAME AS ReferencedColumn,
	RC.UPDATE_RULE,
	RC.DELETE_RULE
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS RC
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU1 ON RC.CONSTRAINT_NAME = KCU1.CONSTRAINT_NAME
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU2 ON RC.UNIQUE_CONSTRAINT_NAME = KCU2.CONSTRAINT_NAME
WHERE KCU1.TABLE_SCHEMA = 'dbo'
ORDER BY KCU1.TABLE_NAME, KCU1.COLUMN_NAME;
GO

-- ==================================================================================
-- SECTION 7: RECORD COUNTS IN SOURCE
-- ==================================================================================

PRINT '=================================================='
PRINT 'RECORD COUNTS - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

EXEC sp_MSForEachTable 'SELECT ''?'' AS TableName, COUNT(*) AS RecordCount FROM ? ORDER BY ? DESC'
GO

-- ==================================================================================
-- SECTION 8: RECORD COUNTS IN TARGET
-- ==================================================================================

PRINT '=================================================='
PRINT 'RECORD COUNTS - TARGET (MMGTDB2A)'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

EXEC sp_MSForEachTable 'SELECT ''?'' AS TableName, COUNT(*) AS RecordCount FROM ? ORDER BY ? DESC'
GO

-- ==================================================================================
-- SECTION 9: UNIQUE CONSTRAINTS
-- ==================================================================================

PRINT '=================================================='
PRINT 'UNIQUE CONSTRAINTS - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

SELECT 
	KCU.TABLE_NAME,
	KCU.COLUMN_NAME,
	TC.CONSTRAINT_NAME,
	TC.CONSTRAINT_TYPE
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU
JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC ON KCU.CONSTRAINT_NAME = TC.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE IN ('UNIQUE', 'PRIMARY KEY')
	AND KCU.TABLE_SCHEMA = 'dbo'
ORDER BY KCU.TABLE_NAME, KCU.COLUMN_NAME;
GO

PRINT '=================================================='
PRINT 'UNIQUE CONSTRAINTS - TARGET (MMGTDB2A)'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

SELECT 
	KCU.TABLE_NAME,
	KCU.COLUMN_NAME,
	TC.CONSTRAINT_NAME,
	TC.CONSTRAINT_TYPE
FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU
JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC ON KCU.CONSTRAINT_NAME = TC.CONSTRAINT_NAME
WHERE TC.CONSTRAINT_TYPE IN ('UNIQUE', 'PRIMARY KEY')
	AND KCU.TABLE_SCHEMA = 'dbo'
ORDER BY KCU.TABLE_NAME, KCU.COLUMN_NAME;
GO

-- ==================================================================================
-- SECTION 10: INDEXES (excluding primary key)
-- ==================================================================================

PRINT '=================================================='
PRINT 'INDEXES - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

SELECT 
	t.name AS TableName,
	i.name AS IndexName,
	c.name AS ColumnName,
	i.type_desc AS IndexType,
	ic.is_descending_key
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.type > 0 
	AND schema_name(t.schema_id) = 'dbo'
ORDER BY t.name, i.name, ic.key_ordinal;
GO

PRINT '=================================================='
PRINT 'INDEXES - TARGET (MMGTDB2A)'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

SELECT 
	t.name AS TableName,
	i.name AS IndexName,
	c.name AS ColumnName,
	i.type_desc AS IndexType,
	ic.is_descending_key
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.type > 0 
	AND schema_name(t.schema_id) = 'dbo'
ORDER BY t.name, i.name, ic.key_ordinal;
GO

-- ==================================================================================
-- SECTION 11: DEFAULT VALUES
-- ==================================================================================

PRINT '=================================================='
PRINT 'DEFAULT VALUES - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

SELECT 
	t.name AS TableName,
	c.name AS ColumnName,
	d.name AS ConstraintName,
	d.definition AS DefaultDefinition
FROM sys.default_constraints d
JOIN sys.tables t ON d.parent_object_id = t.object_id
JOIN sys.columns c ON d.parent_column_id = c.column_id
WHERE schema_name(t.schema_id) = 'dbo'
ORDER BY t.name, c.name;
GO

PRINT '=================================================='
PRINT 'DEFAULT VALUES - TARGET (MMGTDB2A)'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

SELECT 
	t.name AS TableName,
	c.name AS ColumnName,
	d.name AS ConstraintName,
	d.definition AS DefaultDefinition
FROM sys.default_constraints d
JOIN sys.tables t ON d.parent_object_id = t.object_id
JOIN sys.columns c ON d.parent_column_id = c.column_id
WHERE schema_name(t.schema_id) = 'dbo'
ORDER BY t.name, c.name;
GO

-- ==================================================================================
-- SECTION 12: SAMPLE DATA FROM CRITICAL TABLES (SOURCE)
-- ==================================================================================

PRINT '=================================================='
PRINT 'SAMPLE DATA - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

-- Check if key tables exist and show sample data
IF OBJECT_ID('Users', 'U') IS NOT NULL
BEGIN
	PRINT 'Users Table (First 5 records):'
	SELECT TOP 5 * FROM Users;
END
GO

IF OBJECT_ID('Roles', 'U') IS NOT NULL
BEGIN
	PRINT 'Roles Table (First 5 records):'
	SELECT TOP 5 * FROM Roles;
END
GO

IF OBJECT_ID('Applications', 'U') IS NOT NULL
BEGIN
	PRINT 'Applications Table (First 5 records):'
	SELECT TOP 5 * FROM Applications;
END
GO

IF OBJECT_ID('ApplicationDocuments', 'U') IS NOT NULL
BEGIN
	PRINT 'ApplicationDocuments Table (First 5 records):'
	SELECT TOP 5 * FROM ApplicationDocuments;
END
GO

IF OBJECT_ID('DocumentTypes', 'U') IS NOT NULL
BEGIN
	PRINT 'DocumentTypes Table (All records):'
	SELECT * FROM DocumentTypes;
END
GO

-- ==================================================================================
-- SECTION 13: SAMPLE DATA FROM CRITICAL TABLES (TARGET)
-- ==================================================================================

PRINT '=================================================='
PRINT 'SAMPLE DATA - TARGET (MMGTDB2A)'
PRINT '=================================================='
GO

USE MMGTDB2A;
GO

-- Check if key tables exist and show sample data
IF OBJECT_ID('Users', 'U') IS NOT NULL
BEGIN
	PRINT 'Users Table (First 5 records):'
	SELECT TOP 5 * FROM Users;
END
GO

IF OBJECT_ID('Roles', 'U') IS NOT NULL
BEGIN
	PRINT 'Roles Table (First 5 records):'
	SELECT TOP 5 * FROM Roles;
END
GO

IF OBJECT_ID('Applications', 'U') IS NOT NULL
BEGIN
	PRINT 'Applications Table (First 5 records):'
	SELECT TOP 5 * FROM Applications;
END
GO

IF OBJECT_ID('ApplicationDocuments', 'U') IS NOT NULL
BEGIN
	PRINT 'ApplicationDocuments Table (First 5 records):'
	SELECT TOP 5 * FROM ApplicationDocuments;
END
GO

IF OBJECT_ID('DocumentTypes', 'U') IS NOT NULL
BEGIN
	PRINT 'DocumentTypes Table (All records):'
	SELECT * FROM DocumentTypes;
END
GO

-- ==================================================================================
-- SECTION 14: CHECK FOR DATA ISSUES IN SOURCE
-- ==================================================================================

PRINT '=================================================='
PRINT 'DATA INTEGRITY CHECKS - SOURCE (MMGTDBCOPY)'
PRINT '=================================================='
GO

USE MMGTDBCOPY;
GO

-- Check for orphaned records (if tables exist)
IF OBJECT_ID('ApplicationDocuments', 'U') IS NOT NULL AND OBJECT_ID('Applications', 'U') IS NOT NULL
BEGIN
	PRINT 'Orphaned ApplicationDocuments (no matching Application):'
	SELECT COUNT(*) AS OrphanedCount
	FROM ApplicationDocuments ad
	WHERE NOT EXISTS (SELECT 1 FROM Applications a WHERE a.Id = ad.ApplicationId)
END
GO

IF OBJECT_ID('Applications', 'U') IS NOT NULL AND OBJECT_ID('Users', 'U') IS NOT NULL
BEGIN
	PRINT 'Applications with missing Users:'
	SELECT COUNT(*) AS MissingUserCount
	FROM Applications a
	WHERE a.ApplicantId IS NOT NULL
		AND NOT EXISTS (SELECT 1 FROM Users u WHERE u.Id = a.ApplicantId)
END
GO

-- ==================================================================================
-- END OF ANALYSIS
-- ==================================================================================

PRINT '=================================================='
PRINT 'ANALYSIS COMPLETE'
PRINT '=================================================='
GO
