# Quick Reference: Phase 2 Migration

## What to Do Next

### ✅ Pre-Execution Checklist
- [ ] Network connection to `172.16.50.42` is working
- [ ] SSMS or sqlcmd is installed
- [ ] You have sa or admin credentials
- [ ] MMGTDB2A backup exists (you already have one)

### ✅ Execute the Migration
Open SQL Server Management Studio or PowerShell and run:

**Option A: SSMS**
1. Open file: `Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql`
2. Connect to: `172.16.50.42`
3. Execute (Ctrl+F5)
4. Wait ~5-10 minutes
5. Review output

**Option B: PowerShell**
```powershell
$sqlPath = "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"
Invoke-Sqlcmd -InputFile $sqlPath -ServerInstance "172.16.50.42" -TrustServerCertificate -QueryTimeout 600
```

### ✅ What the Script Does
1. Creates `identity_document` DocumentType with proper SuperAdmin reference
2. Saves source/target differences to analysis tables
3. Clears target tables (with FK disabled)
4. Migrates all data in correct dependency order
5. Re-enables FK constraints
6. Validates record counts

### ✅ Expected Output Summary
```
MMGTDBCOPY > MMGTDB2A

Record Counts:
  Users: X rows (may be less than source if duplicates skipped)
  PMICompanies: 24 MATCH
  Applications: 13789 MATCH
  ApplicationDocuments: 14047 MATCH (all IsLegacy=1)
  ApplicationReviewHistory: 31135 MATCH

Status: MIGRATION COMPLETE
```

## Issues You Might See

| Issue | Cause | Solution |
|-------|-------|----------|
| `Msg 515: CreatedBy NULL` | Fixed! | Should not appear |
| `Msg 132: Label already declared` | Fixed! | Should not appear |
| `Cannot create DocumentType` | No admin user | Run DbSeeder first |
| `Users: 0-5 rows` | Duplicates skipped | Expected, not an error |
| `ApplicationDocuments: 0 rows` | UploadedBy mapping issue | May need schema inspection |
| `Timeout Expired` | Network issue | Use `-TrustServerCertificate` flag |

## Files Provided

| File | Purpose |
|------|---------|
| `Phase2_COMPLETE_ALL_IN_ONE.sql` | Main migration script (READY TO RUN) |
| `COMPLETE_FIX_SUMMARY.md` | Detailed fix explanation |
| `MIGRATION_READY_TO_EXECUTE.md` | Execution guide |
| `SYNTAX_VALIDATION_TEST.sql` | Optional: verify syntax locally first |

## Success Criteria

✅ Script completes without RAISERROR exceptions  
✅ Section 5.1 shows record count results  
✅ Most tables show "MATCH" status  
✅ Completion timestamp is shown  
✅ identity_document created with valid GUID  

## If Something Goes Wrong

1. **Save the error output** to a text file
2. **Check the specific error message** in Section that failed (look for ERROR: at start of line)
3. **Common fixes**:
   - Missing SUPER_ADMIN → Run DbSeeder
   - Connection timeout → Use `-TrustServerCertificate`
   - FK conflicts → Constraints are auto-disabled, script handles it

4. **You can re-run** the script anytime - it clears tables first

---

**Ready to execute?** Run the script now!

If you need help after running, save the complete output and share what shows in Section 5.
