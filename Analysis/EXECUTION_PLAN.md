# PHASE 7: MIGRATION EXECUTION PLAN
## How to Synchronize MMGTDB2A with Latest Development Schema

**⚠️ DO NOT EXECUTE UNTIL PHASE 1-6 ANALYSIS IS APPROVED ⚠️**

---

## PRE-EXECUTION CHECKLIST

### ✓ Must Be Completed Before Starting

- [ ] Read and approve EXECUTIVE_SUMMARY.md
- [ ] Read and approve ANALYSIS_REPORT.md
- [ ] Backup MMGTDB2A database (critical for rollback)
- [ ] Document current row counts (already done: 72,766 total)
- [ ] Stop application if running
- [ ] Have PowerShell access to project directory
- [ ] Have SQL Server access as SA
- [ ] Have Entity Framework tools installed (`dotnet ef` command)

---

## BACKUP PROCEDURE (REQUIRED FIRST STEP)

### Step 1: Backup MMGTDB2A Database

```powershell
# Run in SQL Server Management Studio or via PowerShell

# Option A: Using SSMS
# Right-click MMGTDB2A → Tasks → Back Up...
# Select Full backup, name it: MMGTDB2A_PreMigration_[Date]

# Option B: Using SQL Server command
BACKUP DATABASE MMGTDB2A 
TO DISK = 'C:\Backups\MMGTDB2A_PreMigration_20260708.bak'
WITH INIT, STATS = 10;
```

**Confirm backup completed before proceeding.**

---

## EXECUTION PROCEDURE

### Prerequisites
Ensure you are in the project root directory:
```powershell
cd "C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live"
```

### Migration Groups

The 13 migrations break down into logical groups:

#### **GROUP 1: Add Core Assignment and Configuration Tables** (Migrations 1-3)
Creates the fundamental new tables for the assignment system.

```
20260522114023_AddReviewerAssignmentFeatures
20260601214604_DeduplicateApplicationsByRsaPin
20260601214605_AddRsaPinUniqueConstraint
```

**What it does:**
- Creates 10 new tables
- Applies RSA PIN deduplication
- Adds unique constraint to RsaPin

**Commands:**
```powershell
# Verify current migration state
dotnet ef migrations list --context ApplicationDbContext --project MMGT.API.csproj

# Apply GROUP 1 migrations
dotnet ef database update AddReviewerAssignmentFeatures --context ApplicationDbContext

# Should complete without errors
# Output should show: "Done."
```

**Verify Success:**
```sql
-- Run in SQL Server
USE MMGTDB2A;
SELECT COUNT(*) as MigrationCount FROM __EFMigrationsHistory WHERE MigrationId LIKE '202605%' OR MigrationId LIKE '202606%';
-- Should show: 3
```

#### **GROUP 2: Add Escalation and Workload Features** (Migrations 2-4)
Adds escalation rules and workload management.

```
20260602065404_AddPerformanceAndEscalationFeatures
20260602154023_BackfillMissingApplicationAssignments
20260602160556_AddAdvancedWorkloadFeatures
```

**What it does:**
- Creates escalation management tables
- Backfills ApplicationAssignments with existing data
- Adds workload management features

**⚠️ IMPORTANT:** Migration 3 backfills data. Verify no errors during backfill.

**Commands:**
```powershell
# Apply GROUP 2 migrations
dotnet ef database update AddAdvancedWorkloadFeatures --context ApplicationDbContext

# Should show success and backfill messages
```

**Verify Success - Check Backfill:**
```sql
-- Run in SQL Server
USE MMGTDB2A;
SELECT COUNT(*) as AssignmentCount FROM ApplicationAssignments;
-- Should be > 0 (backfilled from Applications)

-- Check row counts
SELECT 
	'ApplicationAssignments' as [Table], COUNT(*) as Count FROM ApplicationAssignments
UNION ALL
SELECT 'Applications', COUNT(*) FROM Applications
UNION ALL  
SELECT 'ReviewerPerformanceMetrics', COUNT(*) FROM ReviewerPerformanceMetrics;
```

#### **GROUP 3: Configuration and Constraints** (Migrations 5-8)
Normalizes configuration and applies business logic constraints.

```
20260603120000_AddAdvancedWorkloadFeatures
20260603214801_EnsurePhoneNumberConfiguration
20260605_FixMigrationHistory
20260606120000_EnsureApplicationStatusNotNull
```

**What it does:**
- Applies configuration normalization
- Adds phone number constraints
- Fixes any remaining migration issues
- Ensures status values are not null

**Commands:**
```powershell
# Apply GROUP 3 migrations
dotnet ef database update EnsureApplicationStatusNotNull --context ApplicationDbContext

# Should complete without errors
```

**Verify Success:**
```sql
-- Run in SQL Server
USE MMGTDB2A;
-- Check that all applications have a status
SELECT COUNT(*) as AppsWithoutStatus FROM Applications WHERE Status IS NULL;
-- Should be: 0

-- Check phone number format
SELECT COUNT(DISTINCT PhoneNumber) as UniquePhoneNumbers FROM Applications 
WHERE PhoneNumber IS NOT NULL;
```

#### **GROUP 4: New Fields and Audit** (Migrations 9-10)
Adds borrower phone numbers and legacy document support.

```
20260623120000_AddBorrowerPhoneNumber
20260625000000_AddIsLegacyToApplicationDocuments
```

**What it does:**
- Adds phone number to Borrower info
- Flags legacy documents for disk storage
- Enables dual storage support (disk + blob)

**Commands:**
```powershell
# Apply GROUP 4 migrations
dotnet ef database update AddIsLegacyToApplicationDocuments --context ApplicationDbContext

# Should complete without errors
```

**Verify Success:**
```sql
-- Run in SQL Server
USE MMGTDB2A;
-- Check new fields exist
SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'ApplicationDocuments' AND COLUMN_NAME = 'IsLegacy';
-- Should be: 1 (column exists)
```

#### **GROUP 5: Final Fixes** (Migration 11)
Final relationship and metadata fixes.

```
20260707130000_AddUserIdToReviewerPerformanceMetrics
```

**What it does:**
- Adds UserId to ReviewerPerformanceMetrics
- Fixes relationship between metrics and users
- Completes audit trail

**Commands:**
```powershell
# Apply final migration
dotnet ef database update AddUserIdToReviewerPerformanceMetrics --context ApplicationDbContext

# Should complete without errors
```

**Verify Success:**
```sql
-- Run in SQL Server
USE MMGTDB2A;
-- Verify final state - all migrations applied
SELECT COUNT(*) as TotalMigrations FROM __EFMigrationsHistory;
-- Should be: 17 (4 original + 13 new)

-- List all migrations
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC;
```

---

## FINAL VERIFICATION PROCEDURE

### Step 1: Verify All Migrations Applied

```powershell
# List all migrations
dotnet ef migrations list --context ApplicationDbContext
```

Expected output should show all 17 migrations with ✓ applied marker.

### Step 2: Verify All Tables Exist

```sql
-- Run in SQL Server on MMGTDB2A
USE MMGTDB2A;

-- Count all tables
SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo' AND TABLE_TYPE = 'BASE TABLE';
-- Should be: 16 (not counting __EFMigrationsHistory)
```

### Step 3: Verify Data Integrity

```sql
-- Run in SQL Server on MMGTDB2A

-- Check critical row counts
SELECT 'Applications' as TableName, COUNT(*) as RowCount FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory
UNION ALL
SELECT 'ApplicationAssignments', COUNT(*) FROM ApplicationAssignments
UNION ALL
SELECT 'Users', COUNT(*) FROM Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM PMICompanies;

-- All original tables should have maintained their row counts:
-- Applications: ~17,134
-- ApplicationDocuments: ~17,071
-- ApplicationReviewHistory: ~38,423
-- ApplicationAssignments: ~17,000+ (backfilled)
-- Users: ~110
-- PMICompanies: ~24
```

### Step 4: Verify Foreign Key Relationships

```sql
-- Run in SQL Server on MMGTDB2A

-- Check for orphaned Application Records
SELECT COUNT(*) as OrphanedAssignments 
FROM ApplicationAssignments a
LEFT JOIN Applications ap ON a.ApplicationId = ap.Id
WHERE ap.Id IS NULL;
-- Should be: 0

-- Check for broken User References
SELECT COUNT(*) as BrokenUserReferences
FROM ApplicationAssignments a
LEFT JOIN Users u ON a.ReviewerId = u.Id
WHERE u.Id IS NULL;
-- Should be: 0

-- Check Document Type References
SELECT COUNT(*) as BrokenDocTypeReferences
FROM ApplicationDocuments a
LEFT JOIN DocumentTypes dt ON a.DocumentTypeId = dt.Id
WHERE a.DocumentTypeId IS NOT NULL AND dt.Id IS NULL;
-- Should be: 0
```

### Step 5: Verify Specific Features Work

```sql
-- Run in SQL Server on MMGTDB2A

-- 1. Verify RSA PIN deduplication (no duplicates in active applications)
SELECT COUNT(*) as DuplicateRsaPins
FROM (
	SELECT RsaPin, COUNT(*) as cnt
	FROM Applications
	WHERE Status IN ('draft', 'submitted', 'under_review', 'returned', 'resubmitted')
	GROUP BY RsaPin
	HAVING COUNT(*) > 1
) dups;
-- Should be: 0

-- 2. Verify legacy document flagging
SELECT COUNT(*) as LegacyDocumentsMarked
FROM ApplicationDocuments
WHERE IsLegacy = 1;
-- Should be > 0 if legacy storage was used

-- 3. Verify performer metrics linked to users
SELECT COUNT(*) as MetricsWithoutUserLink
FROM ReviewerPerformanceMetrics
WHERE UserId IS NULL;
-- Should be: 0 (all metrics linked to users)
```

---

## ADDRESSING ISSUES

### If Migration Fails Mid-Way

**STOP IMMEDIATELY** - Do not apply next migration.

1. **Check error message** - Note the specific error
2. **Restore from backup:**
   ```sql
   -- Restore MMGTDB2A from backup
   RESTORE DATABASE MMGTDB2A 
   FROM DISK = 'C:\Backups\MMGTDB2A_PreMigration_20260708.bak'
   WITH REPLACE;
   ```
3. **Contact support** - Provide:
   - Migration name that failed
   - Complete error message
   - Migration count from __EFMigrationsHistory
   - Row counts before failure

### If Data Looks Incorrect After All Migrations

1. **Check row counts** - Compare to expected values (see verification step 3)
2. **Check relationships** - Run foreign key verification (step 4)
3. **Rollback if needed:**
   ```powershell
   # Role back to before problematic migration
   dotnet ef database update [PreviousMigrationName] --context ApplicationDbContext
   ```

4. **Restore from backup** if in doubt

---

## POST-EXECUTION

### After Successful Migration

1. **Test Application Start:**
   ```powershell
   # In project directory
   dotnet build
   dotnet run
   ```
   Application should start without EF Core errors

2. **Run Application Tests:**
   ```powershell
   # If tests exist
   dotnet test
   ```

3. **Spot-Check Key Features:**
   - Login functionality
   - Application submission
   - Document upload
   - Reviewer assignment
   - Performance metrics

4. **Update Configuration** if needed for new features

5. **Delete Backup** (once confirmed everything works):
   ```powershell
   Remove-Item "C:\Backups\MMGTDB2A_PreMigration_20260708.bak"
   ```

---

## SUMMARY

This procedure will:
- ✓ Preserve all 72,766 production records
- ✓ Add 10 new tables
- ✓ Apply 13 incremental migrations
- ✓ Enable new features (assignments, escalations, metrics)
- ✓ Support legacy disk storage
- ✓ Support new Blob Storage
- ✓ Maintain all relationships

**Total Time:** 10-30 minutes
**Risk Level:** 🟢 LOW (with backup)
**Rollback Time:** 5 minutes (restore from backup)

---

**Ready to proceed?** 

Confirm all items in PRE-EXECUTION CHECKLIST and begin with BACKUP PROCEDURE.
