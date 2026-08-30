namespace MMGT.API.Utils
{
    /// <summary>
    /// SMS message templates for application notifications
    /// </summary>
    public static class SmsMessageTemplates
    {
        /// <summary>
        /// Sent when application is submitted by borrower
        /// Acknowledgement that application has been received
        /// </summary>
        public const string ApplicationSubmitted = 
            "Dear Client, your mortgage application has been received and is currently under review.";

        /// <summary>
        /// Sent when application status is updated to "approved"
        /// Notifies borrower of successful review
        /// </summary>
        public const string ApplicationApproved = 
            "Dear Client, your application has been successfully reviewed and is currently being processed.";

        /// <summary>
        /// Sent when application status is updated to "rejected"
        /// Notifies borrower of application rejection
        /// </summary>
        public const string ApplicationRejected = 
            "Dear Client, your application was declined due to incomplete documentation and has informed your PMI for update";

        /// <summary>
        /// Sent when application is resubmitted by PFA
        /// Notifies borrower that their application has been resubmitted by their PFA
        /// </summary>
        public const string ApplicationResubmitted = 
            "Dear Client, your mortgage application has been resubmitted by your PFA and is currently under review.";

        /// <summary>
        /// Get SMS message for specific application status
        /// </summary>
        /// <param name="status">Application status (approved, rejected, resubmitted, submitted)</param>
        /// <returns>Message template or null if no message for this status</returns>
        public static string? GetMessageForStatus(string status)
        {
            return status?.ToLower().Trim() switch
            {
                "approved" => ApplicationApproved,
                "rejected" => ApplicationRejected,
                "resubmitted" => ApplicationResubmitted,
                "submitted" => ApplicationSubmitted,
                _ => null
            };
        }
    }
}
