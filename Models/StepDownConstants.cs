namespace MMGT.API.Models
{
    public static class StepDownStatus
    {
        public const string Draft = "Draft";
        public const string Submitted = "Submitted";
        public const string CancellationRequested = "CancellationRequested";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
        public const string Removed = "Removed";
        public const string SteppedDown = "SteppedDown";
        public const string Restored = "Restored";

        // A request in one of these states keeps its application locked. One open request per application.
        public static readonly string[] Open = { Draft, Submitted, CancellationRequested, Approved };
    }

    public static class StepDownAction
    {
        public const string Created = "Created";
        public const string Submitted = "Submitted";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string CancellationRequested = "CancellationRequested";
        public const string CancellationAccepted = "CancellationAccepted";
        public const string CancellationDeclined = "CancellationDeclined";
        public const string DraftCancelled = "DraftCancelled";
        public const string DraftRemoved = "DraftRemoved";
        public const string SteppedDown = "SteppedDown";
        public const string Restored = "Restored";
        public const string SmsQueued = "SmsQueued";
        public const string SmsNotSent = "SmsNotSent";
        public const string EmailNotSent = "EmailNotSent";
        public const string LetterUploaded = "LetterUploaded";
    }

    public static class StepDownRoles
    {
        public const string Maker = "SERVICE_MANAGER";
        public const string Checker = "MORTGAGE_LEAD";
        // Head of Benefit Administration: approves a submitted request. (Role names live in these constants only.)
        public const string Approver = "HOD_BENEFIT_ADMIN";
    }
    public static class StepDownRouting
    {
        // After this many step-downs IN A ROW for the same RSA PIN, the next request also needs the HOD Benefit Admin.
        public const int HodAfterInARow = 2;
        // The signed request letter must be SMALLER than this (500 KB).
        public const int MaxLetterBytes = 500 * 1024;
    }
}
