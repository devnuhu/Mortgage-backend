-- ========================================
-- Database Structure Comparison Script
-- Compares MMGTDB2A (Dev) vs MMGTDB2 (Prod Copy)
-- ========================================

-- Run this in MMGTDB2A first
PRINT '========== MMGTDB2A (Development Database) =========='
PRINT ''
PRINT '--- Tables and Row Counts ---'
SELECT 
	OBJECT_NAME(p.object_id) as [Table],
	SUM(p.rows) as [Row Count],
	OBJECT_NAME(i.object_id) as [ObjectName]
FROM sys.partitions p
INNER JOIN sys.indexes i ON p.object_id = i.object_id AND p.index_id = i.index_id
WHERE OBJECTPROPERTY(p.object_id, 'IsUserTable') = 1
	AND p.index_id IN (0, 1)
GROUP BY p.object_id, i.object_id
ORDER BY OBJECT_NAME(p.object_id)
GO

PRINT ''
PRINT '--- Columns by Table ---'
SELECT 
	TABLE_NAME,
	COLUMN_NAME,
	DATA_TYPE,
	IS_NULLABLE,
	COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION
GO

PRINT ''
PRINT '--- Primary Keys ---'
SELECT 
	OBJECT_NAME(ic.object_id) as [Table],
	c.name as [Column]
FROM sys.index_columns ic
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
WHERE i.is_primary_key = 1
ORDER BY [Table]
GO

PRINT ''
PRINT '--- Foreign Keys ---'
SELECT 
	OBJECT_NAME(fk.parent_object_id) as [Parent Table],
	COL_NAME(fk.parent_object_id, fkc.parent_column_id) as [Parent Column],
	OBJECT_NAME(fk.referenced_object_id) as [Referenced Table],
	COL_NAME(fk.referenced_object_id, fkc.referenced_column_id) as [Referenced Column],
	fk.name as [FK Name]
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
ORDER BY [Parent Table]
GO

PRINT ''
PRINT '--- Indexes ---'
SELECT 
	OBJECT_NAME(i.object_id) as [Table],
	i.name as [Index Name],
	i.type_desc as [Type],
	i.is_unique as [Unique],
	i.is_primary_key as [PK]
FROM sys.indexes i
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
ORDER BY OBJECT_NAME(i.object_id), i.name
GO

PRINT ''
PRINT '--- Migration History ---'
SELECT MigrationId, ProductVersion
FROM __EFMigrationsHistory
ORDER BY MigrationId
GO

-- Now switch to MMGTDB2 (Production Copy)
USE MMGTDB2
GO

PRINT ''
PRINT ''
PRINT '========== MMGTDB2 (Production Database Copy) =========='
PRINT ''
PRINT '--- Tables and Row Counts ---'
SELECT 
	OBJECT_NAME(p.object_id) as [Table],
	SUM(p.rows) as [Row Count],
	OBJECT_NAME(i.object_id) as [ObjectName]
FROM sys.partitions p
INNER JOIN sys.indexes i ON p.object_id = i.object_id AND p.index_id = i.index_id
WHERE OBJECTPROPERTY(p.object_id, 'IsUserTable') = 1
	AND p.index_id IN (0, 1)
GROUP BY p.object_id, i.object_id
ORDER BY OBJECT_NAME(p.object_id)
GO

PRINT ''
PRINT '--- Columns by Table ---'
SELECT 
	TABLE_NAME,
	COLUMN_NAME,
	DATA_TYPE,
	IS_NULLABLE,
	COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION
GO

PRINT ''
PRINT '--- Primary Keys ---'
SELECT 
	OBJECT_NAME(ic.object_id) as [Table],
	c.name as [Column]
FROM sys.index_columns ic
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
WHERE i.is_primary_key = 1
ORDER BY [Table]
GO

PRINT ''
PRINT '--- Foreign Keys ---'
SELECT 
	OBJECT_NAME(fk.parent_object_id) as [Parent Table],
	COL_NAME(fk.parent_object_id, fkc.parent_column_id) as [Parent Column],
	OBJECT_NAME(fk.referenced_object_id) as [Referenced Table],
	COL_NAME(fk.referenced_object_id, fkc.referenced_column_id) as [Referenced Column],
	fk.name as [FK Name]
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
ORDER BY [Parent Table]
GO

PRINT ''
PRINT '--- Indexes ---'
SELECT 
	OBJECT_NAME(i.object_id) as [Table],
	i.name as [Index Name],
	i.type_desc as [Type],
	i.is_unique as [Unique],
	i.is_primary_key as [PK]
FROM sys.indexes i
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
ORDER BY OBJECT_NAME(i.object_id), i.name
GO

PRINT ''
PRINT '--- Migration History ---'
SELECT MigrationId, ProductVersion
FROM __EFMigrationsHistory
ORDER BY MigrationId
GO
