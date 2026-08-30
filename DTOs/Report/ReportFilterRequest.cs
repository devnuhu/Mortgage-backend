namespace MMGT.API.DTOs.Report
{
    public class ReportFilterRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 100;

        // Date filtering
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // Date field to filter on
        public string DateField { get; set; } = "CreatedAt"; // CreatedAt, SubmittedAt, UpdatedAt

        // Status filtering
        public List<string>? Statuses { get; set; }

        // Priority filtering
        public List<string>? Priorities { get; set; }

        // PMI Company filtering
        public Guid? PmiCompanyId { get; set; }

        // User filtering
        public Guid? UserId { get; set; }

        // Text search
        public string? SearchTerm { get; set; }

        // Sorting
        public string SortBy { get; set; } = "CreatedAt"; // CreatedAt, SubmittedAt, UpdatedAt, ApplicationNumber, FirstName, LastName
        public bool SortDescending { get; set; } = true;

        public void Validate()
        {
            if (PageNumber < 1) PageNumber = 1;
            if (PageSize < 1) PageSize = 1;
            if (PageSize > 500) PageSize = 500; // Max page size

            // Normalize date field
            var validDateFields = new[] { "CreatedAt", "SubmittedAt", "UpdatedAt" };
            if (!validDateFields.Contains(DateField))
                DateField = "CreatedAt";

            // Normalize sort by
            var validSortFields = new[] { "CreatedAt", "SubmittedAt", "UpdatedAt", "ApplicationNumber", "FirstName", "LastName" };
            if (!validSortFields.Contains(SortBy))
                SortBy = "CreatedAt";

            // Normalize and lowercase statuses
            if (Statuses != null && Statuses.Count > 0)
            {
                Statuses = Statuses.Select(s => s.ToLowerInvariant().Trim()).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
                if (Statuses.Count == 0)
                    Statuses = null;
            }

            // Normalize and lowercase priorities
            if (Priorities != null && Priorities.Count > 0)
            {
                Priorities = Priorities.Select(p => p.ToLowerInvariant().Trim()).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
                if (Priorities.Count == 0)
                    Priorities = null;
            }

            // Clean up search term
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                SearchTerm = SearchTerm.Trim();
                if (SearchTerm.Length < 2)
                    SearchTerm = null;
            }
            else
            {
                SearchTerm = null;
            }
        }
    }
}
