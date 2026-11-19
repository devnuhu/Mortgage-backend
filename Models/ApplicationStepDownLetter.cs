namespace MMGT.API.Models
{
    /// <summary>The signed request letter from the client for a step-down request (PDF, JPG or PNG, under 200 KB). One per request.</summary>
    public class ApplicationStepDownLetter
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RequestId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int SizeBytes { get; set; }
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public Guid UploadedById { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}