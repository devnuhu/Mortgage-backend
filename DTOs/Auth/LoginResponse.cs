using MMGT.API.DTOs.User;
namespace MMGT.API.DTOs.Auth
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool RequiresPasswordChange { get; set; } 
        public UserDto UserData { get; set; } = new();
    }
}
