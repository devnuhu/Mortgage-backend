# Phase 2: Application Connectivity Verification

## Status: ✅ Connectivity Framework Ready

This document ensures the application can connect to MMGTDB2A and access migrated legacy documents post-migration.

---

## Pre-Application Startup Verification

**Before starting the application**, confirm:

```
✅ MMGTDB2A database is accessible on 172.16.50.42
✅ Database migrations have been applied (IsLegacy column exists)
✅ DocumentTypes seeded (24 types)
✅ All 59,105 records successfully migrated
✅ All post-migration validation PASSED
✅ SQL Server network accessibility confirmed
✅ Connection string in appsettings.json points to MMGTDB2A
```

---

## Application Startup Configuration

### Step 1: Verify Connection String

**File**: `appsettings.json` or `appsettings.Production.json`

**Expected Configuration**:

```json
{
  "ConnectionStrings": {
	"DefaultConnection": "Server=172.16.50.42;Database=MMGTDB2A;User Id=sa;Password=***;Encrypt=false;TrustServerCertificate=true;"
  },
  "FileStorage": {
	"StorageType": "Database",
	"MaxFileSizeInMB": 0.3,
	"AllowedExtensions": [".pdf"],
	"PhysicalPath": "C:\\uploads\\"  // For legacy disk file access
  }
}
```

**Verification Steps**:
1. Open `appsettings.json` in Visual Studio
2. Verify `DefaultConnection` points to `MMGTDB2A` (172.16.50.42)
3. Verify `StorageType` is `"Database"` (for BLOB storage of new uploads)
4. Verify `PhysicalPath` matches legacy file location
5. No changes needed if already correct

---

### Step 2: Database Migrations on Startup

**File**: `Program.cs`

**Expected Behavior**:

```csharp
// From Program.cs startup:
using (var scope = app.Services.CreateScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
	await dbContext.Database.MigrateAsync();  // Applies pending migrations
	await DbSeeder.SeedAsync(dbContext);      // Seeds DocumentTypes
}
```

**What Happens**:
1. Application starts
2. Opens connection to MMGTDB2A
3. Applies any pending migrations (including IsLegacy column)
4. Seeds DocumentTypes (24 types)
5. Application ready for requests

---

### Step 3: Start Application

**In Visual Studio**:

1. **Set startup project**: Right-click on main API project → Set as Startup Project
2. **Choose debug mode**: 
   - Debug mode with breakpoint capability: Press F5
   - Release mode for performance: Ctrl+F5
3. **Monitor Application Output**:
   - Watch for connection string log (confirm MMGTDB2A)
   - Watch for migration application messages
   - Watch for seed completion
   - Look for errors (red text)

**Expected Console Output**:

```
info: Microsoft.EntityFrameworkCore.Infrastructure
	  Entity Framework Core 8.0.19 initialized 'ApplicationDbContext' using provider 'Microsoft.EntityFrameworkCore.SqlServer' with options: None
info: Microsoft.EntityFrameworkCore.Database.Command
	  Executed DbCommand (123ms) [Parameters=]
	  SELECT 1
info: Microsoft.EntityFrameworkCore.Migrations
	  Applying migration...
info: DbSeeder
	  Seeding DocumentTypes...
info: Your application is running.
Your application started successfully on http://localhost:5000
```

---

## Endpoint Connectivity Tests

After application starts, test these endpoints:

### TEST 1: Health Check / API Status

**Endpoint**: `GET /api/health` or `GET /` (if available)

**Expected Response**:
```
HTTP 200 OK
{ "status": "healthy", "database": "connected" }
```

**If fails**: Check application logs for connection errors

---

### TEST 2: List Applications (Migrated Data)

**Endpoint**: `GET /api/applications` or similar

**Headers**:
```
Authorization: Bearer [your-auth-token]
Content-Type: application/json
```

**Expected Response**:
```
HTTP 200 OK
[
  {
	"id": "13789e5c-...",
	"applicationNumber": "APP-001",
	"status": "Approved",
	"submissionDate": "2024-01-15",
	"totalDocuments": 5,
	"documents": [...]
  },
  ...
]
```

**If fails**: 
- Check authorization token
- Verify migrated Applications table has records
- Check application logs

---

### TEST 3: Get Files for Application (Legacy Documents)

**Endpoint**: `GET /api/file/by-application/{applicationId}`

**Path Variables**:
- `applicationId`: A valid GUID from migrated Applications

**Headers**:
```
Authorization: Bearer [your-auth-token]
```

**Expected Response**:
```
HTTP 200 OK
[
  {
	"fileId": "12a4b5c6-...",
	"fileName": "national_identity.pdf",
	"fileType": "application/pdf",
	"isLegacy": true,
	"fileSize": 245000,
	"uploadedAt": "2024-01-10",
	"isBlobStored": false
  },
  {
	"fileId": "22b4c5d6-...",
	"fileName": "proof_of_income.pdf",
	"fileType": "application/pdf",
	"isLegacy": true,
	"fileSize": 312000,
	"uploadedAt": "2024-01-12",
	"isBlobStored": false
  },
  ...
]
```

**Key Property**: `"isLegacy": true` (all migrated documents should be true)

**If fails**:
- Verify ApplicationId exists in ApplicationDocuments table
- Check FileService.GetFilesByApplicationAsync method
- Verify database query is returning results

---

### TEST 4: Download Legacy Document (Critical Test)

**Endpoint**: `GET /api/file/download/{fileId}`

**Path Variables**:
- `fileId`: A valid GUID from TEST 3 response

**Headers**:
```
Authorization: Bearer [your-auth-token]
```

**Expected Response**:
```
HTTP 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename="national_identity.pdf"
[Binary PDF content]
```

**How to Test**:
1. **In Postman or browser**:
   - Set URL to endpoint
   - Add bearer token
   - Click Send
   - Should download PDF file

2. **In PowerShell**:
   ```powershell
   $headers = @{
	   "Authorization" = "Bearer $token"
   }
   $response = Invoke-WebRequest -Uri "https://api.domain.com/api/file/download/{fileId}" -Headers $headers
   $response.StatusCode  # Should be 200
   ```

**File Retrieval Logic** (FileService.DownloadFileAsync):
```
1. Load ApplicationDocument from database
2. Check if FileContent exists (IS NOT NULL)
   - If yes: Return BLOB bytes
   - If no: Continue to step 3
3. Check if FilePath exists
   - If yes: Read file from disk path
   - If no: Return 404
4. Return file bytes to client
```

**Expected for Legacy Documents**:
- FileContent = NULL
- FilePath = valid disk path "C:\uploads\..."
- FileService reads bytes from disk
- Returns file to client

**If fails**:
- Verify FilePath is not NULL
- Verify specified disk path exists
- Verify file permissions (application has read access)
- Check FileService logs for errors

---

### TEST 5: Try to Replace Legacy Document (Should Fail Gracefully)

**Endpoint**: `PUT /api/file/replace/{fileId}`

**Path Variables**:
- `fileId`: A legacy document ID (from TEST 3)

**Body**:
```json
{
  "file": [binary PDF content]
}
```

**Expected Response**:
```
HTTP 400 Bad Request
{
  "error": "Cannot modify legacy documents"
}
```

**What Should Happen** (ReplaceFileAsync):
```
1. Load ApplicationDocument from database
2. Check if IsLegacy = 1
   - If yes: Throw 400 error ✅ (This is CORRECT)
   - If no: Continue to replace
```

**If returns 200 OK instead**:
- ❌ ERROR: Legacy document was modified!
- Restore from backup
- Check FileService.ReplaceFileAsync implementation

---

### TEST 6: Try to Delete Legacy Document (Should Fail Gracefully)

**Endpoint**: `DELETE /api/file/{fileId}`

**Path Variables**:
- `fileId`: A legacy document ID

**Expected Response**:
```
HTTP 400 Bad Request
{
  "error": "Cannot delete legacy documents"
}
```

**What Should Happen** (DeleteFileAsync):
```
1. Load ApplicationDocument from database
2. Check if IsLegacy = 1 OR FilePath is populated (legacy indicators)
   - If yes: Throw 400 error ✅ (This is CORRECT)
   - If no: Proceed with delete
```

**If returns 200 OK instead**:
- ❌ ERROR: Legacy document was deleted!
- Restore from backup
- Check FileService.DeleteFileAsync implementation

---

### TEST 7: Upload New Document (Should Create BLOB)

**Endpoint**: `POST /api/file/upload`

**Form Data**:
```
applicationId: [valid application GUID]
file: [new PDF file <= 300 KB]
```

**Expected Response**:
```
HTTP 200 OK
{
  "fileId": "33c4d5e6-...",
  "fileName": "supporting_docs.pdf",
  "fileType": "application/pdf",
  "fileSize": 85000,
  "isBlobStored": true,
  "isLegacy": false
}
```

**Key Property**: `"isBlobStored": true, "isLegacy": false`

**What Should Happen** (UploadFileAsync):
```
1. Validate file size (<= 300 KB)
2. Validate file type (.pdf only)
3. Create ApplicationDocument with:
   - FileContent = [binary PDF bytes] ✅ (BLOB storage)
   - FilePath = null ✅ (Not disk-based)
   - IsLegacy = 0 ✅ (New document)
4. Save to database
5. Return metadata
```

**If fails**:
- Verify file size <= 300 KB
- Verify file is .pdf only
- Check database FileContent column for bytes
- Verify FilePath is null

---

## Full Connectivity Test Suite

**Run this complete test sequence**:

```bash
# 1. Application startup
# - Monitor logs for: "Starting application", "Database connected"
# - No red errors in output

# 2. Health check
GET http://localhost:5000/api/health
# Expected: 200 OK

# 3. List applications
GET http://localhost:5000/api/applications
Authorization: Bearer [token]
# Expected: 200 OK, array of 13,789 applications

# 4. Get files for an app
GET http://localhost:5000/api/file/by-application/{appId}
Authorization: Bearer [token]
# Expected: 200 OK, array with isLegacy=true

# 5. Download legacy doc
GET http://localhost:5000/api/file/download/{fileId}
Authorization: Bearer [token]
# Expected: 200 OK, binary PDF content

# 6. Try replace (should fail)
PUT http://localhost:5000/api/file/replace/{fileId}
Authorization: Bearer [token]
# Expected: 400 Bad Request

# 7. Try delete (should fail)
DELETE http://localhost:5000/api/file/{fileId}
Authorization: Bearer [token]
# Expected: 400 Bad Request

# 8. Upload new document
POST http://localhost:5000/api/file/upload
Authorization: Bearer [token]
Form: applicationId={appId}, file=[PDF]
# Expected: 200 OK, isBlobStored=true

# 9. Verify new doc is BLOB
GET http://localhost:5000/api/file/by-application/{appId}
Authorization: Bearer [token]
# NEW doc should have: isLegacy=false, isBlobStored=true
# OLD docs should have: isLegacy=true, isBlobStored=false
```

---

## Success Criteria

✅ **Application connectivity is successful if:**

- [x] Application starts without connection errors
- [x] Connection to MMGTDB2A confirmed in logs
- [x] All migrations applied successfully
- [x] DocumentTypes seeded
- [x] TEST 1 (Health): Passes
- [x] TEST 2 (List Applications): Returns 13,789 apps
- [x] TEST 3 (Get Files): Returns legacy documents with isLegacy=true
- [x] TEST 4 (Download Legacy): File downloaded from disk
- [x] TEST 5 (Replace Legacy): Returns 400 error (protected)
- [x] TEST 6 (Delete Legacy): Returns 400 error (protected)
- [x] TEST 7 (Upload New): Creates BLOB document with isLegacy=false

---

## Failure Scenarios

### Scenario A: "Database connection failed"

**Error in logs**:
```
error: Microsoft.EntityFrameworkCore.Relational
	  An exception occurred in the database while iterating the results
	  of a query for context type 'ApplicationDbContext'.
	  A network-related or instance-specific error occurred while establishing
	  a connection to SQL Server instance 'UNKNOWN'.
```

**Troubleshooting**:
1. Verify SQL Server 172.16.50.42 is running
2. Verify connection string in appsettings.json
3. Verify network connectivity: `ping 172.16.50.42`
4. Verify database MMGTDB2A exists
5. Verify SQL credentials

---

### Scenario B: "Migration XX not found"

**Error in logs**:
```
error: Microsoft.EntityFrameworkCore.Migrations
	  The migration '20260625000000_AddIsLegacyToApplicationDocuments' has not been applied to the database 'MMGTDB2A'.
```

**Troubleshooting**:
1. Verify MMGTDB2A is the target (not MMGTDB2A_BAK or other)
2. Run: `UPDATE [MMGTDB2A].__EFMigrationsHistory SET Version = ...` (manually if needed)
3. Or: Restore database and re-run migrations

---

### Scenario C: "File not found when downloading"

**Error Response**:
```
404 Not Found
{
  "error": "File not found"
}
```

**Troubleshooting**:
1. Verify FilePath is not NULL: `SELECT FilePath FROM ApplicationDocuments WHERE Id = [fileId]`
2. Verify disk file exists at FilePath path
3. Verify application process has read permissions
4. Check FileService logs

---

### Scenario D: "Legacy document was modified/deleted"

**Problem**: TEST 5 or TEST 6 succeeded when should have failed

**Root Cause**: FileService.ReplaceFileAsync or DeleteFileAsync not checking IsLegacy

**Immediate Action**:
1. Restore database from backup
2. Fix FileService code (verify IsLegacy check exists)
3. Rebuild and redeploy
4. Re-run tests

---

## Performance Baseline

**Record expected application startup time**:

```
Application startup: ~5-10 seconds total
├─ Database connection: ~1 second
├─ Migrations applied: ~2 seconds
├─ DocumentTypes seeded: ~1 second
└─ Application ready: Total ~5-10 seconds
```

**If startup takes > 30 seconds**:
- Check for slow network
- Check for database lock
- Review application logs for bottlenecks

---

## Sign-Off

**Connectivity Verification Sign-Off**:

```
Application: [Your API Project Name]
Environment: [Development / Staging / Production]
Database: MMGTDB2A (172.16.50.42)
Tested By: [Name]
Date: [YYYY-MM-DD]

Connectivity Tests:
✅ Application Start: PASS
✅ TEST 1 (Health): PASS
✅ TEST 2 (List Apps): PASS
✅ TEST 3 (Get Files): PASS
✅ TEST 4 (Download Legacy): PASS
✅ TEST 5 (Replace Protected): PASS
✅ TEST 6 (Delete Protected): PASS
✅ TEST 7 (Upload New): PASS

Overall Status: ✅ APPLICATION CONNECTIVITY VERIFIED
Issues: [None / List any]

Sign-Off: _________________________ Date: _________
```

---

## Next Steps

1. **Monitor Application Logs** (24 hours):
   - Watch for connection errors
   - Watch for legacy file access errors
   - Watch for anomalies

2. **User Acceptance Testing** (UAT):
   - Have business team test application
   - Verify all workflows function with migrated data
   - Test document download/upload

3. **Performance Testing** (Optional):
   - Run load tests against MMGTDB2A
   - Monitor query times
   - Ensure no degradation vs MMGTDB2

4. **Archive Documentation**:
   - Store execution logs
   - Store validation results
   - Store sign-off forms

---

**Phase 2 Application Connectivity Verification - Complete**

*Document: Phase2_ApplicationConnectivity_Verification.md*  
*Created: 2025-01-15*  
*Status: Ready for Testing*
