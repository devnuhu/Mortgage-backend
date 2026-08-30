#!/usr/bin/env pwsh
# Database Synchronization Analysis - Simplified

$ServerName = "172.16.50.42"
$UserId = "sa"
$Password = "Sig123456#"

function Get-DbInfo {
	param([string]$DatabaseName)

	$connString = "Server=$ServerName;Database=$DatabaseName;User Id=$UserId;Password=$Password;TrustServerCertificate=True;"

	try {
		$conn = New-Object System.Data.SqlClient.SqlConnection $connString
		$conn.Open()

		# Get tables
		$cmd = $conn.CreateCommand()
		$cmd.CommandText = @"
SELECT 
	OBJECT_NAME(p.object_id) as [TableName],
	SUM(p.rows) as [RecordCount]
FROM sys.partitions p
INNER JOIN sys.indexes i ON p.object_id = i.object_id AND p.index_id = i.index_id
WHERE OBJECTPROPERTY(p.object_id, 'IsUserTable') = 1
	AND p.index_id IN (0, 1)
GROUP BY p.object_id
ORDER BY OBJECT_NAME(p.object_id)
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

Write-Host "Fetching database information..." -ForegroundColor Green

$dev = Get-DbInfo "MMGTDB2A"
$prod = Get-DbInfo "MMGTDB2"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MMGTDB2A (Development Database)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
if ($dev) {
	$dev | Format-Table -AutoSize
	$total = ($dev | Measure-Object -Property RecordCount -Sum).Sum
	Write-Host "Total Rows: $total" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MMGTDB2 (Production Database Copy)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
if ($prod) {
	$prod | Format-Table -AutoSize
	$total = ($prod | Measure-Object -Property RecordCount -Sum).Sum
	Write-Host "Total Rows: $total" -ForegroundColor Green
}
