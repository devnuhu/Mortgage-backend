namespace MMGT.API.DTOs
{
    public class PaginationRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;

        public void Validate()
        {
            if (PageNumber < 1)
                PageNumber = 1;
            if (PageSize < 1)
                PageSize = 50;
            if (PageSize > 100)
                PageSize = 100; // Max 100 items per page
        }
    }
}
