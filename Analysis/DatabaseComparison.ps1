# Database Synchronization Analysis Script
# Compares Production Database Copy with MMGTDB2 (Development)

param(
	[string]$DevDatabase = "MMGTDB2A",
	[string]$ProdDatabase = "MMGTDB2",  # The production copy to analyze
	[string]$ServerName = "172.16.50.42",
	[string]$UserId = "sa",
	[string]$Password = "Sig123456#"
)

# Connection string builder
function Get-ConnectionString {
	param([string]$Database)
	return "Server=$ServerName;Database=$Database;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"
}

# Execute SQL script and return results
function Invoke-SqlQuery {
	param(
		[string]$Database,
		[string]$Query
	)

	try {
		$connectionString = Get-ConnectionString $Database
		$connection = New-Object System.Data.SqlClient.SqlConnection $connectionString
		$connection.Open()

		$command = $connection.CreateCommand()
		$command.CommandText = $Query
		$adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
		$dataSet = New-Object System.Data.DataSet
		$adapter.Fill($dataSet) | Out-Null

		$connection.Close()
		return $dataSet.Tables[0]
	}
	catch {
		Write-Error "Error executing query on database $Database : $_"
		return $null
	}
}

# SQL Queries for Analysis

# 1. Get all tables
$tablesQuery = @"
SELECT 
	TABLE_SCHEMA as SchemaName,
	TABLE_NAME as TableName,
	(SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = t.TABLE_NAME) as ColumnCount
FROM INFORMATION_SCHEMA.TABLES t
WHERE TABLE_TYPE = 'BASE TABLE'
	AND TABLE_SCHEMA NOT IN ('sys', 'information_schema', 'pg_catalog', 'public')
ORDER BY TABLE_NAME
"@

# 2. Get detailed column information
$columnsQuery = @"
SELECT 
	TABLE_NAME as TableName,
	COLUMN_NAME as ColumnName,
	ORDINAL_POSITION as Position,
	IS_NULLABLE as IsNullable,
	DATA_TYPE as DataType,
	CHARACTER_MAXIMUM_LENGTH as MaxLength,
	NUMERIC_PRECISION as Precision,
	NUMERIC_SCALE as Scale,
	COLUMN_DEFAULT as DefaultValue
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA NOT IN ('sys', 'information_schema', 'pg_catalog', 'public')
ORDER BY TABLE_NAME, ORDINAL_POSITION
"@

# 3. Get primary keys
$primaryKeysQuery = @"
SELECT 
	OBJECT_NAME(ic.object_id) as TableName,
	c.name as ColumnName,
	ic.key_ordinal as KeyPosition
FROM sys.index_columns ic
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
WHERE i.is_primary_key = 1
ORDER BY TableName, KeyPosition
"@

# 4. Get foreign keys
$foreignKeysQuery = @"
SELECT 
	OBJECT_NAME(fk.parent_object_id) as TableName,
	COL_NAME(fk.parent_object_id, fkc.parent_column_id) as ColumnName,
	OBJECT_NAME(fk.referenced_object_id) as ReferencedTable,
	COL_NAME(fk.referenced_object_id, fkc.referenced_column_id) as ReferencedColumn,
	fk.name as ForeignKeyName,
	fk.delete_referential_action_desc as DeleteAction,
	fk.update_referential_action_desc as UpdateAction
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
ORDER BY TableName
"@

# 5. Get indexes
$indexesQuery = @"
SELECT 
	OBJECT_NAME(i.object_id) as TableName,
	i.name as IndexName,
	i.type_desc as IndexType,
	i.is_unique as IsUnique,
	i.is_primary_key as IsPrimaryKey,
	STRING_AGG(c.name, ', ') as Columns
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE OBJECT_NAME(i.object_id) NOT LIKE 'sys%'
	AND OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
GROUP BY OBJECT_NAME(i.object_id), i.name, i.type_desc, i.is_unique, i.is_primary_key
ORDER BY TableName, IndexName
"@

# 6. Get row counts
$rowCountsQuery = @"
SELECT 
	OBJECT_NAME(i.object_id) as TableName,
	SUM(p.rows) as RowCount
FROM sys.indexes i
INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
	AND i.index_id = 0
GROUP BY i.object_id
ORDER BY TableName
"@

# 7. Get migration history
$migrationQuery = @"
SELECT 
	MigrationId,
	ProductVersion,
	AppliedOn
FROM __EFMigrationsHistory
ORDER BY MigrationId DESC
"@

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Database Synchronization Analysis" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Analyze Development Database (MMGTDB2)
Write-Host "PHASE 1: ANALYZING DEVELOPMENT DATABASE (MMGTDB2A)" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Yellow

Write-Host "Tables in MMGTDB2A:" -ForegroundColor Cyan
$devTables = Invoke-SqlQuery $DevDatabase $tablesQuery
if ($devTables) {
	$devTables | Format-Table -AutoSize
	Write-Host "Total tables: $($devTables.Rows.Count)"
}

Write-Host "`nColumns in MMGTDB2A:" -ForegroundColor Cyan
$devColumns = Invoke-SqlQuery $DevDatabase $columnsQuery
if ($devColumns) {
	$devColumns | Format-Table -AutoSize | Head -50
	Write-Host "... showing first 50 columns (Total: $($devColumns.Rows.Count))"
}

Write-Host "`nPrimary Keys in MMGTDB2A:" -ForegroundColor Cyan
$devPKs = Invoke-SqlQuery $DevDatabase $primaryKeysQuery
if ($devPKs) {
	$devPKs | Format-Table -AutoSize
}

Write-Host "`nForeign Keys in MMGTDB2A:" -ForegroundColor Cyan
$devFKs = Invoke-SqlQuery $DevDatabase $foreignKeysQuery
if ($devFKs) {
	$devFKs | Format-Table -AutoSize
}

Write-Host "`nIndexes in MMGTDB2A:" -ForegroundColor Cyan
$devIndexes = Invoke-SqlQuery $DevDatabase $indexesQuery
if ($devIndexes) {
	$devIndexes | Format-Table -AutoSize | Head -50
	Write-Host "... showing first 50 indexes (Total: $($devIndexes.Rows.Count))"
}

Write-Host "`nRow Counts in MMGTDB2A:" -ForegroundColor Cyan
$devRowCounts = Invoke-SqlQuery $DevDatabase $rowCountsQuery
if ($devRowCounts) {
	$devRowCounts | Format-Table -AutoSize
}

Write-Host "`nMigration History in MMGTDB2A:" -ForegroundColor Cyan
$devMigrations = Invoke-SqlQuery $DevDatabase $migrationQuery
if ($devMigrations) {
	$devMigrations | Format-Table -AutoSize | Head -50
	Write-Host "... showing first 50 migrations (Total: $($devMigrations.Rows.Count))"
}

Write-Host ""
Write-Host ""

# Analyze Production Database Copy
Write-Host "PHASE 2: ANALYZING PRODUCTION DATABASE COPY (MMGTDB2)" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor Yellow

Write-Host "Tables in MMGTDB2 (Production Copy):" -ForegroundColor Cyan
$prodTables = Invoke-SqlQuery $ProdDatabase $tablesQuery
if ($prodTables) {
	$prodTables | Format-Table -AutoSize
	Write-Host "Total tables: $($prodTables.Rows.Count)"
}

Write-Host "`nColumns in MMGTDB2 (Production Copy):" -ForegroundColor Cyan
$prodColumns = Invoke-SqlQuery $ProdDatabase $columnsQuery
if ($prodColumns) {
	$prodColumns | Format-Table -AutoSize | Head -50
	Write-Host "... showing first 50 columns (Total: $($prodColumns.Rows.Count))"
}

Write-Host "`nPrimary Keys in MMGTDB2 (Production Copy):" -ForegroundColor Cyan
$prodPKs = Invoke-SqlQuery $ProdDatabase $primaryKeysQuery
if ($prodPKs) {
	$prodPKs | Format-Table -AutoSize
}

Write-Host "`nForeign Keys in MMGTDB2 (Production Copy):" -ForegroundColor Cyan
$prodFKs = Invoke-SqlQuery $ProdDatabase $foreignKeysQuery
if ($prodFKs) {
	$prodFKs | Format-Table -AutoSize
}

Write-Host "`nIndexes in MMGTDB2 (Production Copy):" -ForegroundColor Cyan
$prodIndexes = Invoke-SqlQuery $ProdDatabase $indexesQuery
if ($prodIndexes) {
	$prodIndexes | Format-Table -AutoSize | Head -50
	Write-Host "... showing first 50 indexes (Total: $($prodIndexes.Rows.Count))"
}

Write-Host "`nRow Counts in MMGTDB2 (Production Copy):" -ForegroundColor Cyan
$prodRowCounts = Invoke-SqlQuery $ProdDatabase $rowCountsQuery
if ($prodRowCounts) {
	$prodRowCounts | Format-Table -AutoSize
}

Write-Host "`nMigration History in MMGTDB2 (Production Copy):" -ForegroundColor Cyan
$prodMigrations = Invoke-SqlQuery $ProdDatabase $migrationQuery
if ($prodMigrations) {
	$prodMigrations | Format-Table -AutoSize | Head -50
	Write-Host "... showing first 50 migrations (Total: $($prodMigrations.Rows.Count))"
}

Write-Host ""
Write-Host ""

# Export data to CSV for detailed review
Write-Host "Exporting detailed analysis to CSV files..." -ForegroundColor Green

$exportPath = "Analysis"
if (-not (Test-Path $exportPath)) {
	New-Item -ItemType Directory -Path $exportPath | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

$devTables | Export-Csv "$exportPath/DevTables_$timestamp.csv" -NoTypeInformation
$devColumns | Export-Csv "$exportPath/DevColumns_$timestamp.csv" -NoTypeInformation
$devPKs | Export-Csv "$exportPath/DevPrimaryKeys_$timestamp.csv" -NoTypeInformation
$devFKs | Export-Csv "$exportPath/DevForeignKeys_$timestamp.csv" -NoTypeInformation
$devIndexes | Export-Csv "$exportPath/DevIndexes_$timestamp.csv" -NoTypeInformation
$devRowCounts | Export-Csv "$exportPath/DevRowCounts_$timestamp.csv" -NoTypeInformation
$devMigrations | Export-Csv "$exportPath/DevMigrations_$timestamp.csv" -NoTypeInformation

$prodTables | Export-Csv "$exportPath/ProdTables_$timestamp.csv" -NoTypeInformation
$prodColumns | Export-Csv "$exportPath/ProdColumns_$timestamp.csv" -NoTypeInformation
$prodPKs | Export-Csv "$exportPath/ProdPrimaryKeys_$timestamp.csv" -NoTypeInformation
$prodFKs | Export-Csv "$exportPath/ProdForeignKeys_$timestamp.csv" -NoTypeInformation
$prodIndexes | Export-Csv "$exportPath/ProdIndexes_$timestamp.csv" -NoTypeInformation
$prodRowCounts | Export-Csv "$exportPath/ProdRowCounts_$timestamp.csv" -NoTypeInformation
$prodMigrations | Export-Csv "$exportPath/ProdMigrations_$timestamp.csv" -NoTypeInformation

Write-Host "Export completed to: $exportPath" -ForegroundColor Green
Writing-Host "Files created with suffix: $timestamp" -ForegroundColor Green

Write-Host ""
Write-Host "ANALYSIS SUMMARY" -ForegroundColor Cyan
Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "DevDatabase Tables: $($devTables.Rows.Count)"
Write-Host "DevDatabase Columns: $($devColumns.Rows.Count)"
Write-Host "DevDatabase Migrations: $($devMigrations.Rows.Count)"
Write-Host ""
Write-Host "ProdDatabase Tables: $($prodTables.Rows.Count)"
Write-Host "ProdDatabase Columns: $($prodColumns.Rows.Count)"
Write-Host "ProdDatabase Migrations: $($prodMigrations.Rows.Count)"

Write-Host ""
Write-Host "Analysis complete!" -ForegroundColor Yellow
