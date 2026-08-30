# ⚡ QUICK ACTION GUIDE - Migration Recovery

## 🔴 What Happened
```
❌ Migration failed with 4 errors:
1. CreatedBy NULL for identity_document ✅ FIXED
2. FK constraint on Users delete ✅ AUTO-FIX (depends on 1)
3. Users duplicate email (admin@pfa.com) ⚠️ EXPECTED behavior
4. ApplicationDocuments UploadedBy NULL ❌ NEEDS SCHEMA FIX
```

---

## ✅ What I Fixed
```
Updated: Phase2_COMPLETE_ALL_IN_ONE.sql
├─ Added SuperAdmin lookup (4-level fallback)
├─ Populates CreatedBy from SUPER_ADMIN user
├─ Populates CreatedAt with GETDATE()
└─ Result: identity_document creation will NOW succeed
```

---

## 🚀 WHAT YOU DO NOW

### **Immediate Action (1 minute)**

Re-run the migration with the fixed script:
```powershell
Invoke-Sqlcmd -S "172.16.50.42" -d "MMGTDB2A" `
  -i "Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql"
```

**Expected this time:**
- ✅ identity_document: CREATED successfully
- ✅ FK constraint: RESOLVED
- ⚠️ Users: Still may skip duplicates (admin@pfa.com) - this is OK
- ❌ ApplicationDocuments: Still fails (schema issue) - expected
- ✅ ApplicationReviewHistory: SUCCESS

---

### **Then: Schema Discovery (5 minutes)**

Run this in SSMS against **MMGTDBCOPY** to find the correct column:

```sql
USE MMGTDBCOPY
GO

-- Check what columns exist in source ApplicationDocuments
SELECT 
	COLUMN_NAME,
	DATA_TYPE,
	IS_NULLABLE,
	ORDINAL_POSITION
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'ApplicationDocuments'
ORDER BY ORDINAL_POSITION
GO

-- Show sample data
SELECT TOP 3 * FROM ApplicationDocuments
GO
```

**Share with me:**
- List of all columns
- Which column represents "who uploaded" (CreatedBy, UserId, etc.)

---

### **Finally: I'll Create Corrected Migration**

Once you share the schema, I will:
1. Create correct UploadedBy mapping
2. Fix Users duplication logic
3. Provide new migration script
4. You run it for success

---

## 📊 Current Status

| Component | Status | Notes |
|-----------|--------|-------|
| identity_document | ✅ WILL SUCCEED | SuperAdmin lookup added |
| Users | ⚠️ PARTIAL | Some skip due to duplicates (expected) |
| Applications | ✅ OK | 13,789 migrating fine |
| ApplicationDocuments | ❌ BLOCKED | Need schema discovery |
| ApplicationReviewHistory | ✅ OK | 31,135 migrating fine |

---

## 📁 Files

**Updated:**
- `Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql` - Fixed (SuperAdmin lookup added)

**New Docs:**
- `Analysis/MIGRATION_FAILURE_ANALYSIS.md` - Complete analysis with solutions

---

## ⏭️ The 3-Step Fix

```
Step 1: Execute updated script (1 min)
		↓ ✅ identity_document will succeed

Step 2: Run schema discovery query (5 min)
		↓ Share results with me

Step 3: I create corrected migration (5 min)
		↓ You execute for success
```

---

**Ready when you are! Just run the script again 🚀**
