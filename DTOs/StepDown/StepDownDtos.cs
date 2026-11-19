namespace MMGT.API.DTOs.StepDown
{
    public class StepDownActor
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class CreateStepDownRequest
    {
        public Guid ApplicationId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class StepDownCommentRequest
    {
        public string? Comment { get; set; }
    }

    public class StepDownReasonRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class StepDownDecisionRequest
    {
        public bool Accept { get; set; }
        public string? Comment { get; set; }
    }

    public class StepDownRequestDto
    {
        public Guid Id { get; set; }
        public Guid ApplicationId { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string BorrowerName { get; set; } = string.Empty;
        public string RsaPin { get; set; } = string.Empty;
        public bool? IsHni { get; set; }
        public string ApplicationStatus { get; set; } = string.Empty;
        public bool ApplicationLocked { get; set; }
        public bool ApplicationArchived { get; set; }
        /// <summary>The newest step-down request for the same application (it may be this one).</summary>
        public Guid? LatestRequestId { get; set; }
        public string? LatestRequestStatus { get; set; }
        /// <summary>When this application was last stepped down (null if never). Use it with ApplicationArchived.</summary>
        public DateTime? LastSteppedDownAt { get; set; }
        public string PmiCompanyName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? WaitingFor { get; set; }
        /// <summary>false = maker, checker only. true = maker, HOD Benefit Admin, checker. On a draft this is a preview of the route.</summary>
        public bool RequiresHodApproval { get; set; }
        /// <summary>Step-downs in a row already made for this RSA PIN (any application).</summary>
        public int PriorStepDownCount { get; set; }
        public bool HasLetter { get; set; }
        public string? LetterFileName { get; set; }
        public int? LetterSizeBytes { get; set; }
        public DateTime? LetterUploadedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
        public Guid RequestedById { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? CancellationRequestedAt { get; set; }
        public string? CancelReason { get; set; }
        public Guid? DecidedById { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string? DecisionComment { get; set; }
        public Guid? ActionedById { get; set; }
        public DateTime? ActionedAt { get; set; }
        public string? ActionComment { get; set; }
        public DateTime? RestoredAt { get; set; }
        public string? RestoreReason { get; set; }
    }

    public class StepDownLetterFile
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
    }
    
    public class StepDownAuditDto
    {
        public Guid Id { get; set; }
        public Guid RequestId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string PerformedByName { get; set; } = string.Empty;
        public string PerformedByRole { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; }
        public string? Comment { get; set; }
        public string? Details { get; set; }
    }
}
