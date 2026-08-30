namespace MMGT.API.DTOs
{
    public class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? TraceId { get; set; }
        public Dictionary<string, string[]>? ValidationErrors { get; set; }
        public string? Details { get; set; }
    }

    public class SuccessResponse<T>
    {
        public T? Data { get; set; }
        public string Message { get; set; } = "Request completed successfully";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
