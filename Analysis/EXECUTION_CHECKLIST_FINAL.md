# PHASE 2: FINAL EXECUTION CHECKLIST ✅

## 📋 PRE-EXECUTION VERIFICATION

- [x] All 4 waves of fixes applied
- [x] Script syntax validated
- [x] FirstName='' added to Applications (line 227, 231)
- [x] Version=1 added to ApplicationDocuments (line 303, 317)
- [x] Priority=0 present in Applications (line 227, 231)
- [x] Status='Completed' present in ApplicationReviewHistory (line 334, 337)
- [x] Temp table cleanup present (lines 245-250)
- [x] FK constraints include DocumentTypes (lines 150, 366)
- [x] Users delete removed (lines 167-168)
- [x] All documentation complete
- [x] Model defaults verified (FirstName from Application.cs:24, Version from ApplicationDocument.cs:71)

---

## 🚀 EXECUTION INSTRUCTIONS

### STEP 1: Open the Script
**File:** `C:\Users\nuhu.audu\Desktop\PRO\BACKUP\BACKEND\main bk\29-06-26B\accessmortage-backend_live\Analysis\Phase2_COMPLETE_ALL_IN_ONE.sql`

**Options:**
- [ ] SSMS (File → Open → Select file → Open)
- [ ] Visual Studio (File → Open File)
- [ ] Any SQL editor that supports .sql files

### STEP 2: Connect to Database
**Server:** 172.16.50.42
**Database:** MMGTDB2A
**Credentials:** SA account (your credentials)

**Note:** If SSL error, use `-C` flag in sqlcmd or use SSMS trusted connection

### STEP 3: Execute Script
**SSMS Method:**
- Ctrl+Shift+E OR
- Query Menu → Execute OR
- Click Execute button (▶)

**sqlcmd Method:**
```powershell
sqlcmd -S 172.16.50.42 -d MMGTDB2A -U sa -P "password" -i "Phase2_COMPLETE_ALL_IN_ONE.sql" -C
```

### STEP 4: Monitor Execution
- Watch Messages pane
- Look for error indicators (RED text)
- Expected duration: 2-5 minutes

### STEP 5: Verify Completion
- Look for: **"MIGRATION COMPLETE"**
- All sections should show SUCCESS
- Error count should be 0

---

## ✅ SUCCESS CRITERIA

### Must See:
```
OK: PMICompanies migrated: 24 rows
OK: Users migrated: [N] rows
OK: Applications migrated: [N] rows ← NO NULL FirstName ERROR
OK: DocumentType mapping ready
OK: ApplicationDocuments migrated: 14047+ rows ← NO NULL Version ERROR
OK: ApplicationReviewHistory migrated: 31135 rows
OK: FK constraints re-enabled
MIGRATION COMPLETE
```

### Must NOT See:
- ❌ Cannot insert the value NULL into column 'FirstName'
- ❌ Cannot insert the value NULL into column 'Version'
- ❌ Cannot insert the value NULL into column 'Priority'
- ❌ Cannot insert the value NULL into column 'Status'
- ❌ #UniqueDocTypes already exists
- ❌ Invalid object name '#DocTypeMapping'
- ❌ FK constraint conflict

---

## 🔍 TROUBLESHOOTING

### If FirstName error appears:
- Line 227 must have: `FirstName`
- Line 231 must have: `'' as FirstName`
- Update script and rerun

### If Version error appears:
- Line 303 must have: `FileType, Version`
- Line 316-317 must have: `'Legacy' as FileType, 1 as Version`
- Update script and rerun

### If any other error:
- Record exact error message
- Report with line number
- May indicate new schema discovery needed

### If connection fails:
- Verify 172.16.50.42 is accessible
- Verify SA credentials correct
- Try SSMS connection first (simpler)
- Add `-C` flag to sqlcmd if SSL error

---

## 📊 POST-EXECUTION VALIDATION

After successful execution, run these queries:

```sql
USE MMGTDB2A
GO

-- 1. Verify Applications FirstName
SELECT COUNT(*) as Total,
	   SUM(CASE WHEN FirstName = '' THEN 1 ELSE 0 END) as WithEmptyFirstName,
	   SUM(CASE WHEN FirstName IS NULL THEN 1 ELSE 0 END) as NullFirstName
FROM Applications
GO

-- 2. Verify ApplicationDocuments Version
SELECT COUNT(*) as Total,
	   SUM(CASE WHEN Version = 1 THEN 1 ELSE 0 END) as WithVersion1,
	   SUM(CASE WHEN Version IS NULL THEN 1 ELSE 0 END) as NullVersion
FROM ApplicationDocuments
GO

-- 3. Verify ApplicationReviewHistory Status
SELECT COUNT(*) as Total,
	   SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) as WithCompleted,
	   SUM(CASE WHEN Status IS NULL THEN 1 ELSE 0 END) as NullStatus
FROM ApplicationReviewHistory
GO

-- 4. Quick count check
SELECT 
	'Applications' as TableName, COUNT(*) as RecordCount FROM Applications
UNION ALL
SELECT 'ApplicationDocuments', COUNT(*) FROM ApplicationDocuments
UNION ALL
SELECT 'ApplicationReviewHistory', COUNT(*) FROM ApplicationReviewHistory
UNION ALL
SELECT 'Users', COUNT(*) FROM Users
UNION ALL
SELECT 'PMICompanies', COUNT(*) FROM PMICompanies
ORDER BY TableName
GO
```

---

## ⏱️ TIMING EXPECTATIONS

- **Sections 1-2:** ~10 seconds (validation, constraints)
- **Section 3.1:** ~1 second (PMICompanies)
- **Section 3.2:** ~1 second (Users)
- **Section 3.3:** ~5-10 seconds (Applications)
- **Section 3.4:** ~5 seconds (DocumentType mapping)
- **Section 3.5:** ~30-60 seconds (ApplicationDocuments - 14,000+ rows)
- **Section 3.6:** ~10-30 seconds (ApplicationReviewHistory - 31,000+ rows)
- **Section 4:** ~5 seconds (Re-enable constraints)
- **Section 5:** ~5 seconds (Validation)
- **Total:** ~2-5 minutes

---

## 🎯 WHAT HAPPENS NEXT

### Immediate After Success:
1. Save migration log/output
2. Run post-execution validation queries
3. Verify application still connects
4. Check UI/API functionality

### If All Good:
1. Migration is complete
2. Legacy data preserved
3. Schema aligned
4. System ready for use

### If Issues:
1. Document exact error
2. Investigate root cause
3. May need additional fixes (Wave 5?)
4. Rerun after fix

---

## 📞 QUICK REFERENCE

| What | Where |
|---|---|
| Main Script | Analysis/Phase2_COMPLETE_ALL_IN_ONE.sql |
| Quick Guide | Analysis/WAVE4_QUICK_EXECUTE.md |
| Detailed Info | Analysis/WAVE4_FIXES_APPLIED.md |
| All Waves | Analysis/PHASE2_MIGRATION_COMPLETE_UPDATE.md |
| Troubleshooting | Analysis/EXECUTION_GUIDE_WAVE3.md |

---

## 🚦 STATUS: READY TO EXECUTE

✅ All 4 waves of fixes applied
✅ Script verified
✅ Defaults sourced from models
✅ Documentation complete
✅ Ready for immediate execution

**Awaiting your execution trigger!**

