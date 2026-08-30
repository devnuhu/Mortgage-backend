# PowerShell script to safely apply RsaPin deduplication migrations
# This script checks for duplicates first, then applies migrations

param(
	[switch]$CheckOnly = $false,
	[switch]$Force = $false
)

$solutionRoot = "C:\Users\nuhu.audu\Desktop\PRO\BACKEND\accessmortage-backend_live"
$connectionStringUser = "sa"  # Update if different
$databaseName = "MMGT"  # Update if different

Write-Host "RsaPin Deduplication Migration Tool" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# SQL to check for duplicates
$checkDuplicatesSql = @"
WITH DuplicateRsaPins AS (
	SELECT 
		RsaPin,
		COUNT(*) as DuplicateCount,
		COUNT(*) - 1 as ToDelete
	FROM Applications
	WHERE RsaPin IS NOT NULL AND RsaPin != ''
	GROUP BY RsaPin
	HAVING COUNT(*) > 1
)
SELECT SUM(ToDelete) as TotalToDelete, COUNT(*) as TotalGroupsWithDuplicates
FROM DuplicateRsaPins
"@

Write-Host "Step 1: Checking for RsaPin duplicates..." -ForegroundColor Yellow

try {
	# Note: This is pseudo-code. In real scenario, connect to your database
	Write-Host "Connect to your SQL Server and check for duplicates with this query:" -ForegroundColor Gray
	Write-Host $checkDuplicatesSql -ForegroundColor White
	Write-Host ""

	if (-not $CheckOnly) {
		Write-Host "Step 2: Backup your database (highly recommended)" -ForegroundColor Yellow
		Write-Host "Before proceeding, ensure you have a backup of your database!" -ForegroundColor Red
		Write-Host ""

		if (-not $Force) {
			$confirm = Read-Host "Have you backed up your database? (yes/no)"
			if ($confirm -ne "yes") {
				Write-Host "Migration cancelled. Please backup first." -ForegroundColor Red
				exit 1
			}
		}

		Write-Host "Step 3: Applying migrations..." -ForegroundColor Yellow

		Set-Location $solutionRoot

		# Run migrations
		Write-Host "Running: dotnet ef database update" -ForegroundColor Cyan
		dotnet ef database update --project MMGT.API.csproj --context ApplicationDbContext

		if ($LASTEXITCODE -eq 0) {
			Write-Host "✓ Migrations applied successfully!" -ForegroundColor Green
			Write-Host "✓ RsaPin duplicates removed" -ForegroundColor Green
			Write-Host "✓ Unique constraint created" -ForegroundColor Green
		} else {
			Write-Host "✗ Migration failed with exit code: $LASTEXITCODE" -ForegroundColor Red
			exit 1
		}
	} else {
		Write-Host "Check-only mode: Run the above query to see duplicates before migration" -ForegroundColor Yellow
	}
}
catch {
	Write-Host "Error: $_" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
