#!/usr/bin/env pwsh
# Detailed Database Analysis Script

$ServerName = "172.16.50.42"
$UserId = "sa"
$Password = "Sig123456#"

function Get-MigrationHistory {
	param([string]$DatabaseName)

	$connString = "Server=$ServerName;Database=$DatabaseName;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"

	try {
		$conn = New-Object System.Data.SqlClient.SqlConnection $connString
		$conn.Open()

		$cmd = $conn.CreateCommand()
		$cmd.CommandText = "SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId"
		$adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
		$ds = New-Object System.Data.DataSet
		[void]$adapter.Fill($ds)

		$conn.Close()
		return $ds.Tables[0]
	}
	catch {
		Write-Error "Failed to query $DatabaseName : $_"
		return $null
	}
}

function Get-TableStructure {
	param([string]$DatabaseName)

	$connString = "Server=$ServerName;Database=$DatabaseName;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"

	try {
		$conn = New-Object System.Data.SqlClient.SqlConnection $connString
		$conn.Open()

		# Get columns
		$cmd = $conn.CreateCommand()
		$cmd.CommandText = @"
SELECT 
	TABLE_NAME,
	COLUMN_NAME,
	ORDINAL_POSITION,
	DATA_TYPE,
	CHARACTER_MAXIMUM_LENGTH as MaxLength,
	IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION
"@
		$adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
		$ds = New-Object System.Data.DataSet
		[void]$adapter.Fill($ds)

		$conn.Close()
		return $ds.Tables[0]
	}
	catch {
		Write-Error "Failed to query $DatabaseName : $_"
		return $null
	}
}

Write-Host "Analyzing Migration History..." -ForegroundColor Green
Write-Host ""

$devMigrations = Get-MigrationHistory "MMGTDB2A"
$prodMigrations = Get-MigrationHistory "MMGTDB2"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MMGTDB2A (Development) - Migrations" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
if ($devMigrations) {
	$devMigrations | Format-Table -AutoSize
	Write-Host "Total Migrations: $($devMigrations.Rows.Count)" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MMGTDB2 (Production Copy) - Migrations" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
if ($prodMigrations) {
	$prodMigrations | Format-Table -AutoSize
	Write-Host "Total Migrations: $($prodMigrations.Rows.Count)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Analyzing Table Structures..." -ForegroundColor Green

$devStructure = Get-TableStructure "MMGTDB2A"
$prodStructure = Get-TableStructure "MMGTDB2"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MMGTDB2A (Development) - Table Columns" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
if ($devStructure) {
	$devStructure | Format-Table -AutoSize | Head -100
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MMGTDB2 (Production Copy) - Table Columns" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
if ($prodStructure) {
	$prodStructure | Format-Table -AutoSize | Head -100
}

# Export to CSV
Write-Host ""
Write-Host "Exporting to CSV files..." -ForegroundColor Green
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

if ($devMigrations) {
	$devMigrations | Export-Csv "Analysis/DevMigrations_$timestamp.csv" -NoTypeInformation
	Write-Host "Exported: DevMigrations_$timestamp.csv"
}

if ($prodMigrations) {
	$prodMigrations | Export-Csv "Analysis/ProdMigrations_$timestamp.csv" -NoTypeInformation
	Write-Host "Exported: ProdMigrations_$timestamp.csv"
}

if ($devStructure) {
	$devStructure | Export-Csv "Analysis/DevStructure_$timestamp.csv" -NoTypeInformation
	Write-Host "Exported: DevStructure_$timestamp.csv"
}

if ($prodStructure) {
	$prodStructure | Export-Csv "Analysis/ProdStructure_$timestamp.csv" -NoTypeInformation
	Write-Host "Exported: ProdStructure_$timestamp.csv"
}

Write-Host ""
Write-Host "Analysis complete!" -ForegroundColor Yellow
