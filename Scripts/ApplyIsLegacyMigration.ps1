# PowerShell script to apply the IsLegacy migration manually
[System.Data.SqlClient.SqlConnection] | Out-Null

$connStr = "Server=172.16.50.42;Database=MMGTDBF;User Id=sa;Password=Ajay@123456;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
	$conn.Open()
	Write-Host "Connected to database successfully"

	$sql = Get-Content "Scripts\ManuallyApplyIsLegacyMigration.sql" -Raw
	$cmd = $conn.CreateCommand()
	$cmd.CommandText = $sql
	$result = $cmd.ExecuteNonQuery()

	Write-Host "Migration applied successfully (rows affected: $result)"
} catch {
	Write-Host "Error: $($_.Exception.Message)"
} finally {
	if ($conn.State -eq 'Open') {
		$conn.Close()
	}
}
