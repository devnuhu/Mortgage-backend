namespace MMGT.API.DTOs.Application
{
    /// <summary>Optional body of POST api/applications/{id}/submit. The note is shown to the reviewer when the client resubmits after a rejection.</summary>
    public class SubmitApplicationRequest
    {
        public string? Note { get; set; }
    }
}