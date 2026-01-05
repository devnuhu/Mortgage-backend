namespace MMGT.API.DTOs.PMI
{
    public class PMICompanyDto
    {
        public Guid Id { get; set; }
        public string PmiName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string ResourcePersonName { get; set; } = string.Empty;
        public string ResourcePersonEmail { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int TotalApplications { get; set; }
        public int PendingApplications { get; set; }
        public int ApprovedApplications { get; set; }
    }
}