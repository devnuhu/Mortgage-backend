# Additional Schema Issues Found - Third Set of Fixes

## New Issues Discovered in Second Migration Run

### Error 1: Applications - Missing Priority Column
```
Msg 515: Cannot insert the value NULL into column 'Priority'
```

**Analysis:**
- Applications table in target has a Priority column
- It is NOT NULL
- Not in source MMGTDBCOPY.dbo.Applications
- Need to add default value

**Fix:** Add Priority column with default value

### Error 2: ApplicationReviewHistory - Missing Status Column
```
Msg 515: Cannot insert the value NULL into column 'Status'
```

**Analysis:**
- ApplicationReviewHistory table in target has a Status column
- It is NOT NULL
- Not in source MMGTDBCOPY.dbo.ApplicationReviewHistory
- Need to add default value

**Fix:** Add Status column with default value

### Error 3: Temp Table Already Exists
```
ERROR DocumentType mapping: There is already an object named '#UniqueDocTypes' in the database.
```

**Analysis:**
- Temp table from previous run still exists
- Script doesn't clean up or check before creating
- Causes cascade failure for DocumentType mapping

**Fix:** Add DROP IF EXISTS before creating temp tables

### Error 4: FK Constraint Blocking Users Delete
```
Msg 547: The DELETE statement conflicted with a REFERENCE constraint "FK_DocumentTypes_Users_CreatedBy"
```

**Analysis:**
- identity_document.CreatedBy references Users table
- Cannot delete Users while FK constraint exists
- Need to disable this specific FK or handle differently

**Fix:** Include DocumentTypes in FK constraint disable, or exclude from delete

---

## Updated Fix Strategy

### Fix #1: Applications - Add Priority Default
```sql
-- Add Priority column
-- Options:
-- A) Priority = 0 (default/normal)
-- B) Priority = 1 (high)
-- C) Based on LoanAmount (risk-based)
-- Decision: Use 0 (Normal/Default priority)

INSERT INTO Applications (Id, ApplicationNumber, UserId, PMICompanyId, LoanAmount, 
	Status, CreatedAt, UpdatedAt, Priority)
SELECT a.Id, a.ApplicationNumber, a.UserId, a.PMICompanyId, a.LoanAmount, 
	a.Status, a.CreatedAt, a.UpdatedAt, 0 as Priority
FROM MMGTDBCOPY.dbo.Applications a
```

### Fix #2: ApplicationReviewHistory - Add Status Default
```sql
-- Add Status column
-- Options:
-- A) Status = 'Pending' (under review)
-- B) Status = 'Completed' (review is complete)
-- C) Status = 'Approved' (approved after review)
-- Decision: Use 'Completed' (history records are completed)

INSERT INTO ApplicationReviewHistory (Id, ApplicationId, ReviewedById, 
	CreatedAt, Status)
SELECT arh.Id, arh.ApplicationId, arh.ReviewedById, arh.CreatedAt, 
	'Completed' as Status
FROM MMGTDBCOPY.dbo.ApplicationReviewHistory arh
```

### Fix #3: Clean Up Temp Tables
```sql
-- Add at the start of DocumentType mapping section
IF OBJECT_ID('tempdb..#UniqueDocTypes') IS NOT NULL
	DROP TABLE #UniqueDocTypes

IF OBJECT_ID('tempdb..#DocTypeMapping') IS NOT NULL
	DROP TABLE #DocTypeMapping
```

### Fix #4: Include DocumentTypes in FK Disable/Enable
```sql
-- When disabling constraints, also include DocumentTypes
ALTER TABLE MMGTDB2A.dbo.DocumentTypes NOCHECK CONSTRAINT ALL

-- When re-enabling, also include DocumentTypes
ALTER TABLE MMGTDB2A.dbo.DocumentTypes CHECK CONSTRAINT ALL

-- When deleting Users, handle the circular FK:
-- Option A: Don't delete Users from target (preserve seeded users)
-- Option B: Delete with constraint disabled
-- Decision: Don't delete any users - they are seeded and shouldn't be removed
```

---

## Recommended Approach

The cleanest solution is to **NOT DELETE USERS** since they are seeded by DbSeeder and include admin accounts needed for the system to function.

Instead:
1. Insert new users from source where email doesn't exist
2. Leave existing (seeded) users untouched
3. Skip Users delete entirely

```sql
-- New approach for Users:
IF CAN_DELETE_SAFELY_FROM_FK
	DELETE FROM MMGTDB2A.dbo.Users WHERE 1=1
ELSE
	-- Just don't delete - preserve seeded users
	PRINT 'Skipping Users delete - preserving seeded admin accounts'
```

---

## Column Analysis

### Applications Table (Target)
Columns that need defaults (not in source or NOT NULL):
- Priority (NOT NULL) - DEFAULT to 0
- DecisionBy (allow NULL) - already removed
- DecisionAt (allow NULL) - already removed

### ApplicationReviewHistory Table (Target)
Columns that need  defaults (not in source or NOT NULL):
- Status (NOT NULL) - DEFAULT to 'Completed'
- ReviewNotes (allow NULL) - already removed
- ApprovedAt (allow NULL) - already removed

---

## Next Steps for Migration Script

1. **Add Priority to Applications INSERT**
   - Line: After LoanAmount, before Status
   - Value: 0 (default priority)

2. **Add Status to ApplicationReviewHistory INSERT**
   - Line: After CreatedAt
   - Value: 'Completed' (review history is completed)

3. **Clean temp tables before creating**
   - Line: Before creating #UniqueDocTypes
   - Add: DROP IF EXISTS checks

4. **Fix Users maintenance**
   - Option: Don't delete Users
   - Reason: Preserve seeded admin accounts

5. **Fix DocumentTypes-FK issue**
   - Include DocumentTypes in FK disable/enable
   - Or handle Users separately

---

## Decision Points for User

### 1. Applications.Priority Default
- [ ] 0 (Normal/Default) - RECOMMENDED
- [ ] 1 (High)
- [ ] Other value?

### 2. ApplicationReviewHistory.Status Default
- [ ] 'Completed' - RECOMMENDED
- [ ] 'Pending'
- [ ] 'Approved'
- [ ] Other value?

### 3. Users Handling
- [ ] Keep as-is (don't delete, just INSERT new) - RECOMMENDED
- [ ] Delete all Users then re-import
- [ ] Other approach?

### 4. FK Constraint for DocumentTypes.CreatedBy
- [ ] Disable/enable like other tables - RECOMMENDED
- [ ] Handle specially
- [ ] Other approach?

