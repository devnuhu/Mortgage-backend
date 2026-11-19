namespace MMGT.API.Models
{
    /// <summary>
    /// A maker-checker request to step an application down (archive it, recoverable).
    /// Service Manager raises it, LEAD_REVIEWER approves, APP_MANAGER actions it.
    /// </summary>
    public class ApplicationStepDownRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ApplicationId { get; set; }
        public string Status { get; set; } = StepDownStatus.Draft;
        public string Reason { get; set; } = string.Empty;
        // PMI name at the time of the request (used in the borrower SMS and in history)
        public string PmiCompanyName { get; set; } = string.Empty;
        public Guid RequestedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }
        public DateTime? CancellationRequestedAt { get; set; }
        public string? CancelReason { get; set; }
        public Guid? DecidedById { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string? DecisionComment { get; set; }
        public Guid? ActionedById { get; set; }
        public DateTime? ActionedAt { get; set; }
        public string? ActionComment { get; set; }
        public Guid? RestoredById { get; set; }
        public DateTime? RestoredAt { get; set; }
        public string? RestoreReason { get; set; }
        // Route chosen when the request was submitted: true = maker, HOD Benefit Admin, checker. false = maker, checker only.
        public bool RequiresHodApproval { get; set; } = true;
        // How many step-downs in a row the same RSA PIN already had when this request was submitted.
        public int PriorStepDownCount { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
