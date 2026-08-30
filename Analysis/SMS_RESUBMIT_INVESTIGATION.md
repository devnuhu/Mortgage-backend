# SMS Resubmission Investigation & Fix

## Issue Summary
SMS is not being sent when applications are resubmitted. The user reports SMS failures for "resubmit" status updates.

## Root Cause Analysis

### Two Different Resubmission Flows

#### **Flow 1: REVIEWER marks application as "resubmitted"**
- **Where:** `ApplicationService.UpdateApplicationStatusAsync()` - Line 327
- **Trigger:** Reviewer calls API with status = "resubmitted"
- **What Happens:**
  1. Status updated to "resubmitted"
  2. Review history recorded
  3. SMS SHOULD be sent (line 407)
  4. Condition: `normalizedStatus == "resubmitted"` → Message: "Dear Client, your Mortgage application has been resubmitted and is currently being Reviewed"

#### **Flow 2: CLIENT resubmits after rejection**
- **Where:** `ApplicationService.SubmitApplicationAsync()` - Line 457
- **Trigger:** Client submits application from draft/rejected/resubmitted state
- **What Happens:**
  1. If previous status was "rejected" OR "resubmitted"
  2. Status set to "RETURNED" (not "resubmitted") - Line 514
  3. Review history recorded with status = "returned"
  4. NO SMS sent by this flow
  5. SMS only sent for status "submitted" → Line 597

### Critical Findings

#### **Issue 1: Phone Number May Be Missing**
- **Problem:** Application.PhoneNumber is optional (nullable string?)
- **Location:** Models/Application.cs, line 35
- **Impact:** If phone number is null/empty, SMS won't be sent even if status matches
- **Check:** Look at CreateApplicationRequest to see if PhoneNumber is required

#### **Issue 2: Status When Client Resubmits**
- **When Client clicks "Resubmit":** Status becomes "RETURNED", not "RESUBMITTED"
- **Where:** SubmitApplicationAsync, line 514
- **Why:** This is intentional to show admin they have a "returned" application
- **SMS Result:** No SMS sent for "returned" status (only "submitted", "rejected", "resubmitted")

#### **Issue 3: SMS for "submitted" Status in SubmitApplicationAsync**
- **Location:** Line 597
- **Message:** "Dear Client, your mortgage application has been received and is currently under review. Access ARM Pensions."
- **Condition:** Only sent when previousStatus == "draft"
- **For Returns:** When previousStatus is "rejected" or "resubmitted", status becomes "returned" and NO SMS is sent

## Current Logging Points Added

### Application Service Logging
```
[SMS-DEBUG]        - Status normalization and phone number presence check
[SMS-MATCHED]      - Status matches trigger (rejected/resubmitted)
[SMS-WARN]         - Phone number missing but SMS message prepared
[SMS-INFO]         - SMS about to be sent
[SMS-SUCCESS]      - SMS sent successfully
[SMS-FAILED]       - SMS service returned false
[SMS-ERROR]        - Exception during SMS sending
```

### SMS Service Logging
```
[SMS-INPUT]        - Null/empty phone or message validation
[SMS-FORMAT]       - Phone number formatting started
[SMS-FORMAT-STEP1] - Trim whitespace
[SMS-FORMAT-STEP2] - Remove leading '+'
[SMS-FORMAT-STEP3] - Convert '0' to '234'
[SMS-FORMAT-FAIL]  - Formatting validation failures
[SMS-FORMAT-SUCCESS] - Formatting successful
[SMS-QUEUE]        - About to insert into SMS2Vendor queue
[SMS-QUEUED]       - Successfully inserted
[SMS-EXCEPTION]    - Exception with full details
[FMT-*]           - Detailed phone formatting debug logs
```

## Diagnostic Steps

### Step 1: Enable Detailed Logging
1. Rebuild the solution
2. Run the application
3. Trigger the resubmission in your test environment

### Step 2: Check Application Logs
Look for patterns:
```
[SMS-DEBUG] Processing SMS for Application APP-2024-001...
[SMS-MATCHED] Matched status 'resubmitted' - SMS message prepared...
[SMS-FORMAT] Formatting phone number...
[SMS-QUEUED] ✓ SMS queued successfully...
```

### Step 3: Identify the Failure Point
Based on log output, determine where it fails:

**If no SMS-DEBUG logs:**
- ✗ UpdateApplicationStatusAsync not being called
- CHECK: Is the status update API being called at all?

**If SMS-DEBUG but no SMS-MATCHED:**
- ✗ normalizedStatus != "resubmitted"
- CHECK: What is the actual status value? (might have extra spaces, different case)
- SOLUTION: Verify status value in API request

**If SMS-MATCHED but SMS-WARN about missing phone:**
- ✗ application.PhoneNumber is null or empty
- CHECK: Is phone number being captured when application created?
- SOLUTION: Ensure phone number is passed in CreateApplicationRequest and stored

**If SMS-FORMAT-SUCCESS but SMS-QUEUED fails:**
- ✗ Database insert failing
- CHECK: Is SmsNotificationDbContext properly configured?
- CHECK: Can you manually insert into SMS2Vendor table?
- SOLUTION: Verify SMS database connection string and permissions

**If SMS-EXCEPTION logs:**
- ✗ Exception thrown during SMS send
- CHECK: Full exception details in log
- SOLUTION: Based on specific exception type

## Data Flow Verification

### Scenario 1: Reviewer Marks as Resubmitted
```
1. Reviewer calls: PUT /api/Applications/{id}/status
   Body: { "status": "resubmitted", "comments": "..." }

2. UpdateApplicationStatusAsync() called
   - application loaded
   - status updated to "resubmitted"
   - review history created

3. SMS Logic (Line 395-430):
   - normalizedStatus = "resubmitted" ✓
   - smsMessage = "Dear Client, your Mortgage application has been resubmitted..."
   - Check: application.PhoneNumber != null ?
	 - YES: Call SendSmsAsync()
	 - NO: Log warning, no SMS sent

4. SmsService.SendSmsAsync():
   - Validate phone number not empty ✓
   - Validate message not empty ✓
   - Format phone number (0812... → 2348012...)
   - If format fails: Return false
   - If format succeeds: Insert into SMS2Vendor
   - Return success/failure
```

### Scenario 2: Client Resubmits After Rejection
```
1. Client clicks "Resubmit" on rejected application

2. SubmitApplicationAsync() called
   - previousStatus = "rejected"
   - Set status to "returned" (NOT "resubmitted")
   - Add review history with status "returned"

3. SMS Logic (Line 597):
   - Only for previousStatus == "draft"
   - Since previousStatus is "rejected", NO SMS sent

4. Result: NO SMS sent to borrower
   - This is CURRENT BEHAVIOR
   - May need FIX: Send SMS when returning application
```

## Potential Fixes

### Fix 1: Send SMS for "returned" Status (When Client Resubmits)
If you want SMS to be sent when client resubmits after rejection:

**Location:** SubmitApplicationAsync, around line 597

Add this check:
```csharp
// If this was a returned/resubmitted application being resubmitted by client
if ((previousStatus == "rejected" || previousStatus == "resubmitted") && 
	!string.IsNullOrWhiteSpace(application.PhoneNumber))
{
	var returnedSmsMessage = "Dear Client, your updated mortgage application has been received and is under review.";
	await _smsService.SendSmsAsync(application.PhoneNumber, returnedSmsMessage, application.ApplicationNumber);
}
```

### Fix 2: Ensure Phone Number is Captured
**Location:** DTOs/Application/CreateApplicationRequest.cs

Check if PhoneNumber is required:
```csharp
public class CreateApplicationRequest
{
	[Required]  // ← Add this if not present
	[Phone]     // ← Add this to validate format
	public string PhoneNumber { get; set; }
}
```

### Fix 3: Validate Phone Number Format Earlier
Add validation in CreateApplicationAsync to fail early if phone is invalid:
```csharp
if (string.IsNullOrWhiteSpace(request.PhoneNumber))
{
	throw new ValidationException("Phone number is required");
}

var formattedPhone = _smsService.FormatPhoneNumber(request.PhoneNumber);
if (string.IsNullOrEmpty(formattedPhone))
{
	throw new ValidationException("Phone number format is invalid for SMS delivery");
}
```

## Database Check Query

Run this to see if SMS2Vendor records are being created:

```sql
-- Check recent SMS records
SELECT TOP 10 
	SenderID, 
	PhoneNo, 
	Message, 
	SendStatus,
	CreatedDate  -- if this column exists
FROM SMS2Vendor
ORDER BY [column_with_timestamp] DESC;

-- Count by sender
SELECT SenderID, COUNT(*) as Count
FROM SMS2Vendor
GROUP BY SenderID;
```

## Configuration Verification

Ensure these are configured:
```json
{
  "ConnectionStrings": {
	"SmsNotificationDb": "Server=...;Database=SMSNotification;..."
  }
}
```

And registered in Program.cs:
```csharp
services.AddDbContext<SmsNotificationDbContext>(options =>
	options.UseSqlServer(configuration.GetConnectionString("SmsNotificationDb")));

services.AddScoped<ISmsService, SmsService>();
```

## Summary of Findings

| Finding | Severity | Impact |
|---------|----------|--------|
| Phone number may be null | HIGH | SMS won't send if empty |
| "returned" status doesn't trigger SMS | MEDIUM | Client resubmissions won't get SMS |
| No detailed logging previously | MEDIUM | Hard to diagnose issues |
| Status case/whitespace issues possible | LOW | Unlikely but string comparison could fail |

## Next Actions

1. **Rebuild and redeploy** with new logging
2. **Test resubmission flow** and capture logs
3. **Check output** for failure point
4. **Apply appropriate fix** based on failure location
5. **Add phone number requirement** validation
6. **Test all scenarios** (created, submitted, status updates, returned)
