namespace MMGT.API.DTOs.Assignment
{
    public class MyQueueItemDto
    {
        public Guid AssignmentId { get; set; }
        public DateTime AssignedAt { get; set; }
        public string AssignedByName { get; set; } = "System";
        public string? ReassignmentReason { get; set; }

        public Guid ApplicationId { get; set; }
        public string ApplicationNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string BorrowerName { get; set; } = string.Empty;
        public string RsaPin { get; set; } = string.Empty;
        public string? PmiName { get; set; }
        public decimal LoanAmount { get; set; }
        public decimal PropertyValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ResubmittedAt { get; set; }
        public int ResubmissionCount { get; set; }
        public bool IsLocked { get; set; }
        public bool? IsHni { get; set; }            // null = not checked yet
        public DateTime? HniCheckedAt { get; set; }

        // How long it has been waiting for this reviewer: since the assignment, or since the client's latest resubmission if that is later.
        public DateTime WaitingSince { get; set; }
        public double WaitingHours { get; set; }
        public bool IsOverdue { get; set; }

        public int DocumentsTotal { get; set; }
        public int DocumentsApproved { get; set; }
        public int DocumentsPending { get; set; }
        public int DocumentsRejected { get; set; }   // Rejected + RequestedRevision (sent back to the client)
        public DateTime? LastDocumentActivityAt { get; set; }

        public string? LastActionStatus { get; set; }
        public string? LastActionBy { get; set; }
        public DateTime? LastActionAt { get; set; }
        public string? LastActionComment { get; set; }

        // reviewDocuments | waitingForClient | readyToDecide | decided | locked
        public string NextStep { get; set; } = string.Empty;
    }

    public class MyQueueResponseDto
    {
        public List<MyQueueItemDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public string Scope { get; set; } = "open";
    }

    public class MyQueueSummaryDto
    {
        public int Open { get; set; }
        public int Submitted { get; set; }
        public int UnderReview { get; set; }
        public int Pending { get; set; }
        public int ReturnedByClient { get; set; }     // returned + resubmitted
        public int Overdue { get; set; }
        public int WithPendingDocuments { get; set; }
        public int WaitingForClient { get; set; }     // has rejected / revision documents
        public int ReadyToDecide { get; set; }        // no pending and no rejected documents
        public int Locked { get; set; }
        public int HniOpen { get; set; }            // open applications flagged HNI
        public int DecidedToday { get; set; }         // by me, Nigeria day
        public int DecidedLast7Days { get; set; }
        public int OverdueHours { get; set; }
    }
}