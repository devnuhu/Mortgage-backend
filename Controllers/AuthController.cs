using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using MMGT.API.DTOs.Auth;
using MMGT.API.Services.Interfaces;
using MMGT.API.Data;
using MMGT.API.Utils;
using MMGT.API.Exceptions;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context;

        public AuthController(IAuthService authService, ApplicationDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);
                return Ok(response);
            }
            catch (AuthenticationException ex) when (ex.Message.Contains("disabled"))
            {
                // Return 403 Forbidden for login access disabled
                return StatusCode(StatusCodes.Status403Forbidden, new LoginAccessDisabledResponse
                {
                    Code = "LOGIN_ACCESS_DISABLED",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                // Debug logging
                Console.WriteLine("=== CHANGE PASSWORD DEBUG START ===");
                Console.WriteLine($"User authenticated: {User.Identity.IsAuthenticated}");
                Console.WriteLine($"User identity name: {User.Identity.Name}");
                Console.WriteLine($"User claims count: {User.Claims.Count()}");

                // Log all claims
                foreach (var claim in User.Claims)
                {
                    Console.WriteLine($"Claim - Type: {claim.Type}, Value: {claim.Value}");
                }

                // Extract user ID from JWT token claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                Console.WriteLine($"Found userIdClaim: '{userIdClaim}'");

                if (string.IsNullOrEmpty(userIdClaim))
                {
                    Console.WriteLine("ERROR: userIdClaim is null or empty");
                    return Unauthorized(new { message = "No user ID found in token" });
                }

                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    Console.WriteLine($"ERROR: Could not parse '{userIdClaim}' as Guid");
                    return Unauthorized(new { message = "Invalid user ID format in token" });
                }

                Console.WriteLine($"Successfully parsed userId: {userId}");
                Console.WriteLine("=== CHANGE PASSWORD DEBUG END ===");

                var result = await _authService.ChangePasswordAsync(userId, request);
                if (result)
                    return Ok(new { message = "Password changed successfully" });
                else
                    return BadRequest(new { message = "Failed to change password" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"EXCEPTION in ChangePassword: {ex.Message}");
                Console.WriteLine($"STACK TRACE: {ex.StackTrace}");
                return BadRequest(new { message = ex.Message });
            }
        }

        // New: Redeem first-login token and set password
        [HttpPost("first-login")]
        public async Task<ActionResult> FirstLogin([FromBody] FirstLoginDto dto)
        {
            try
            {
                var user = await _context.Users.FindAsync(dto.UserId);
                if (user == null) return NotFound(new { message = "User not found" });

                if (user.FirstLoginTokenExpiresAt == null || user.FirstLoginTokenExpiresAt < DateTime.UtcNow)
                    return BadRequest(new { message = "Token expired" });

                var expectedHash = SecurityHelpers.HashToken(dto.Token);
                if (user.FirstLoginTokenHash != expectedHash)
                    return BadRequest(new { message = "Invalid token" });

                // set new password and clear token
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                user.IsFirstLogin = false;
                user.FirstLoginTokenHash = null;
                user.FirstLoginTokenExpiresAt = null;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Password set successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("test")]
        public ActionResult Test()
        {
            return Ok(new { message = "Auth API is working!", timestamp = DateTime.UtcNow });
        }

        [HttpGet("debug-token")]
        [Authorize]
        public ActionResult DebugToken()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                var allClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

                return Ok(new
                {
                    IsAuthenticated = User.Identity.IsAuthenticated,
                    UserId = userIdClaim,
                    AllClaims = allClaims
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("admin/reset-password")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<AdminResetPasswordResponse>> AdminResetPassword([FromBody] AdminResetPasswordRequest request)
        {
            try
            {
                // Extract admin user ID from JWT token claims
                var adminIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(adminIdClaim) || !Guid.TryParse(adminIdClaim, out var adminId))
                {
                    return Unauthorized(new { message = "Invalid admin user ID in token" });
                }

                var result = await _authService.AdminResetPasswordAsync(adminId, request.UserId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all role login access control settings
        /// Accessible by: LEAD_REVIEWER, ADMIN
        /// </summary>
        [HttpGet("role-access-controls")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<List<RoleLoginAccessDto>>> GetAllRoleLoginAccess()
        {
            try
            {
                var result = await _authService.GetAllRoleLoginAccessAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get login access control settings for a specific role
        /// Accessible by: LEAD_REVIEWER, ADMIN
        /// </summary>
        [HttpGet("role-access-controls/{role}")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<RoleLoginAccessDto>> GetRoleLoginAccess(string role)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    return BadRequest(new { message = "Role parameter is required" });
                }

                var result = await _authService.GetRoleLoginAccessAsync(role);
                if (result == null)
                {
                    return NotFound(new { message = $"No login access configuration found for role: {role}" });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Create or update login access control settings for a specific role
        /// Accessible by: LEAD_REVIEWER, ADMIN
        /// </summary>
        [HttpPut("role-access-controls/{role}")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<RoleLoginAccessDto>> UpdateRoleLoginAccess(
            string role,
            [FromBody] UpdateRoleLoginAccessRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    return BadRequest(new { message = "Role parameter is required" });
                }

                if (request == null)
                {
                    return BadRequest(new { message = "Request body is required" });
                }

                // Extract user ID from JWT token claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Invalid user ID in token" });
                }

                var result = await _authService.UpdateRoleLoginAccessAsync(role, request, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Schedule automatic disable/enable of login access for a specific role
        /// The role will be disabled from ScheduledStartTime to ScheduledEndTime (UTC), then automatically re-enabled
        /// Accessible by: LEAD_REVIEWER, ADMIN
        /// </summary>
        [HttpPut("role-access-controls/{role}/schedule")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<RoleLoginAccessDto>> ScheduleRoleAccess(
            string role,
            [FromBody] ScheduleRoleAccessRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    return BadRequest(new { message = "Role parameter is required" });
                }

                if (request == null)
                {
                    return BadRequest(new { message = "Request body is required" });
                }

                // Extract user ID from JWT token claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Invalid user ID in token" });
                }

                var result = await _authService.ScheduleRoleAccessAsync(role, request, userId);
                return Ok(result);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Cancel a scheduled disable/enable of login access for a specific role
        /// Accessible by: LEAD_REVIEWER, ADMIN
        /// </summary>
        [HttpDelete("role-access-controls/{role}/schedule")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN")]
        public async Task<ActionResult<RoleLoginAccessDto>> CancelScheduledAccess(string role)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(role))
                {
                    return BadRequest(new { message = "Role parameter is required" });
                }

                // Extract user ID from JWT token claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("UserId")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(new { message = "Invalid user ID in token" });
                }

                var result = await _authService.CancelScheduledAccessAsync(role, userId);
                return Ok(result);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ResourceNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}