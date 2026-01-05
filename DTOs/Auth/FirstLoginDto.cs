namespace MMGT.API.DTOs.Auth
{
    public class FirstLoginDto
    {
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}