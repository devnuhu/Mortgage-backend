namespace MMGT.API.DTOs.Auth
{
    public class AdminResetPasswordResponse
    {
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string TemporaryPassword { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
