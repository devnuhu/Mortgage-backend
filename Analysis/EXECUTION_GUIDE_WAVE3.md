# Phase 2 Migration - Execution Guide (Third Wave Fixes Ready)

## Current Status
✓ All 3 waves of schema fixes have been applied to `Phase2_COMPLETE_ALL_IN_ONE.sql`
✓ Script is ready for execution
⏳ Awaiting execution (credential/connection issue preventing PowerShell execution)

---

## Fixes Applied Summary

### Wave 1 (Earlier):
- ✓ identity_document creation with proper CreatedBy
- ✓ Applications: removed invalid DecisionBy/DecisionAt columns
- ✓ ApplicationDocuments: added FileType='Legacy' column
- ✓ ApplicationReviewHistory: removed invalid ReviewNotes/ApprovedAt columns

### Wave 2 (Earlier):
- ✓ Temp table cleanup with DROP IF EXISTS
- ✓ FK constraints include DocumentTypes in NOCHECK/CHECK
- ✓ Users deletion removed to preserve seeded accounts

### Wave 3 (Just Now):
- ✓ **Applications.Priority** = 0 (NOT NULL, no source column)
- ✓ **ApplicationReviewHistory.Status** = 'Completed' (NOT NULL, no source column)
- ✓ Temp table idempotency checks
- ✓ FK constraint handling improved

---

## How to Execute

### Option 1: Using SQL Server Management Studio (SSMS)
```
1. Open SQL Server Management Studio
2. Connect to: 172.16.50.42 (or your server instance)
3. Database: MMGTDB2A
4. File → Open → File
5. Select: C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql
6. Execute: Ctrl+Shift+E or Query → Execute
7. Review output in Results/Messages pane
```

### Option 2: Using sqlcmd (PowerShell)
```powershell
$scriptPath = "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"

# With certificate bypass
sqlcmd -S "172.16.50.42" -d MMGTDB2A -U sa -P "YOUR_PASSWORD" -i "$scriptPath" -h -1

# Or with TrustServerCertificate (newer sqlcmd)
sqlcmd -S "tcp:172.16.50.42,1433" -d MMGTDB2A -U sa -P "YOUR_PASSWORD" -i "$scriptPath" -h -1 -C
```

### Option 3: Using sqlcmd (CMD/Batch)
```cmd
cd C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis

sqlcmd -S 172.16.50.42 -d MMGTDB2A -U sa -P "YOUR_PASSWORD" -i Phase2_COMPLETE_ALL_IN_ONE.sql -h -1
```

### Option 4: PowerShell with SqlClient (Programmatic)
```powershell
$connectionString = "Server=172.16.50.42;Database=MMGTDB2A;User Id=sa;Password=YOUR_PASSWORD;Connection Timeout=30;"
$scriptPath = "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"
$scriptContent = Get-Content -Path $scriptPath -Raw

$conn = New-Object System.Data.SqlClient.SqlConnection $connectionString
try {
	$conn.Open()
	Write-Host "Connected to MMGTDB2A successfully"

	$cmd = $conn.CreateCommand()
	$cmd.CommandTimeout = 300  # 5 minutes
	$cmd.CommandText = $scriptContent
	$cmd.ExecuteNonQuery()
	Write-Host "Migration script executed successfully"
} catch {
	Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
} finally {
	$conn.Close()
}
```

---

## Expected Output

### Successful Execution Should Show:

```
=== SECTION 1: PRE-MIGRATION VALIDATION ===
1.1: Verify MMGTDBCOPY accessible...
OK: MMGTDBCOPY accessible - ApplicationDocuments: 14047 records

1.2: Source record counts:
PMICompanies: 24
Users: 110
Applications: [N]
ApplicationDocuments: 14047
ApplicationReviewHistory: [N]

=== SECTION 2: PREPARE TARGET TABLES ===
2.1: Disabling FK constraints...
OK: FK constraints disabled

2.2: Clearing existing data...
ApplicationReviewHistory: 0 rows deleted
ApplicationDocuments: 0 rows deleted
Applications: 0 rows deleted
Users: Skipping delete to preserve seeded admin accounts
PMICompanies: 24 rows deleted
OK: Target tables cleared

=== SECTION 3: MIGRATE DATA ===

3.1: Migrating PMICompanies...
OK: PMICompanies migrated: 24 rows

3.2: Migrating Users...
OK: Users migrated: [0-N] rows

3.3: Migrating Applications...
OK: Applications migrated: [N] rows  <-- NO NULL Priority ERROR!

3.4: Building DocumentType mapping...
OK: DocumentType mapping ready

3.5: Migrating ApplicationDocuments...
OK: ApplicationDocuments migrated: 14047 rows

3.6: Migrating ApplicationReviewHistory...
OK: ApplicationReviewHistory migrated: [N] rows  <-- NO NULL Status ERROR!

=== SECTION 4: RE-ENABLE CONSTRAINTS ===
Re-enabling FK constraints...
OK: FK constraints re-enabled

=== SECTION 5: POST-MIGRATION VALIDATION ===

5.1: RECORD COUNTS COMPARISON:
Table                    | Source | Target
ApplicationDocuments     | 14047  | 14047
Applications             | [N]    | [N]
ApplicationReviewHistory | [N]    | [N]
Users                    | +[N]   | (seeded preserved)
PMICompanies             | 24     | 24

5.2: DATA QUALITY CHECKS - IsLegacy distribution:
IsLegacy count check...

5.3: FOREIGN KEY INTEGRITY:
FK validation passed

=============================================================================
MIGRATION COMPLETE
Review validation results above
=============================================================================

Completion time: [timestamp]
```

---

## Key Differences from Previous Runs

### What's Different Now:
1. **Applications.Priority** - Will default to 0 instead of causing NULL error
2. **ApplicationReviewHistory.Status** - Will default to 'Completed' instead of causing NULL error
3. **Users deletion skipped** - Preserves seeded admin accounts
4. **Temp table cleanup** - Prevents "already exists" errors on reruns
5. **DocumentTypes in FK constraints** - Properly handled during constraint disable/enable

### Error Prevention:
- ❌ ~~Cannot insert the value NULL into column 'Priority'~~ → ✓ Priority=0 supplied
- ❌ ~~Cannot insert the value NULL into column 'Status'~~ → ✓ Status='Completed' supplied
- ❌ ~~#UniqueDocTypes already exists~~ → ✓ DROP IF EXISTS before creation
- ❌ ~~#DocTypeMapping missing~~ → ✓ Proper order and error handling
- ❌ ~~FK constraint on DocumentTypes.CreatedBy~~ → ✓ DocumentTypes included in constraint management

---

## Connection Troubleshooting

If you encounter connection errors:

### Error: "SSL Provider: certificate chain was issued by an authority that is not trusted"
**Solution:**
```powershell
# Use Encrypt=false in connection string OR
# Add -C flag to sqlcmd (TrustServerCertificate)
sqlcmd -S 172.16.50.42 -d MMGTDB2A -U sa -P "password" -C -i script.sql
```

### Error: "Login failed for user 'sa'"
**Solution:**
1. Verify the SA password is correct
2. Check if SA account is enabled on the server
3. Try with Windows Authentication if SA is disabled
4. Verify SQL Server is accessible from your machine (ping 172.16.50.42)

### Error: "Cannot connect to server"
**Solution:**
1. Verify IP address: 172.16.50.42
2. Check SQL Server port: 1433 (default)
3. Verify firewall allows connection
4. Use SSMS to test connection first

---

## Files Ready for Execution

**Primary Migration Script:**
- `Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql` (426 lines, all fixes applied)

**Support/Reference Scripts:**
- `Analysis/Phase2_CreateDocumentType_IdentityDocument.sql` (standalone identity_document creation)
- `Analysis/QUICK_SCHEMA_CHECK.sql` (schema discovery)

**Documentation:**
- `Analysis/THIRD_WAVE_FIXES_COMPLETE.md` (detailed fix summary)
- `Analysis/ADDITIONAL_SCHEMA_ISSUES_FOUND.md` (issue analysis)
- `Analysis/SCHEMA_CORRECTIONS_APPLIED_v2.md` (wave 1 fixes)
- `Analysis/REEXECUTION_READY_v2.md` (wave 1-2 readiness)

---

## Next Actions After Execution

1. Review the output for any UNexpected errors
2. If successful:
   - Run queries to verify data counts
   - Check FK integrity
   - Verify IsLegacy flags are set correctly
   - Confirm DocumentTypeId mappings

3. If errors occur:
   - Document exact error message
   - Check if it's an application-specific issue
   - May need additional schema discovery

---

**Status:** ✓ READY FOR EXECUTION
**Last Updated:** 2026-07-08 12:45 UTC
**Scripts Modified:** 1 (Phase2_COMPLETE_ALL_IN_ONE.sql)
**Fixes Applied:** 5 (from 3 waves)

