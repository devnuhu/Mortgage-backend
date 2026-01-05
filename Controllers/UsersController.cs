using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MMGT.API.DTOs.User;
using MMGT.API.Services.Interfaces;
using MMGT.API.Data;
using Microsoft.EntityFrameworkCore;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ApplicationDbContext _context;

        public UsersController(IUserService userService, ApplicationDbContext context)
        {
            _userService = userService;
            _context = context;
        }

        [HttpGet("profile")]
        public async Task<ActionResult<UserDto>> GetProfile()
        {
            try
            {
                var userId = GetCurrentUserId();
                var user = await _userService.GetUserByIdAsync(userId);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<List<UserDto>>> GetAllUsers()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.PMICompany)
                    .Where(u => u.IsActive)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Role = u.Role,
                        IsFirstLogin = u.IsFirstLogin,
                        IsActive = u.IsActive,
                        CreatedAt = u.CreatedAt,
                        LastLoginAt = u.LastLoginAt,
                        PMICompanyId = u.PMICompanyId,
                        PMICompanyName = u.PMICompany != null ? u.PMICompany.PmiName : null,
                        PMIEmail = u.PMICompany != null ? u.PMICompany.EmailAddress : null
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("by-pmi/{pmiCompanyId}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<List<UserDto>>> GetUsersByPMICompany(Guid pmiCompanyId)
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.PMICompany)
                    .Where(u => u.PMICompanyId == pmiCompanyId && u.IsActive)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Role = u.Role,
                        IsFirstLogin = u.IsFirstLogin,
                        IsActive = u.IsActive,
                        CreatedAt = u.CreatedAt,
                        LastLoginAt = u.LastLoginAt,
                        PMICompanyId = u.PMICompanyId,
                        PMICompanyName = u.PMICompany.PmiName,
                        PMIEmail = u.PMICompany.EmailAddress
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var user = await _userService.CreateUserAsync(request);
                return CreatedAtAction(nameof(GetProfile), new { id = user.Id }, user);
            }
            catch (DbUpdateException dbEx)
            {
                // Log the detailed database exception
                Console.WriteLine($"❌ Database Error: {dbEx.Message}");
                Console.WriteLine($"❌ Inner Exception: {dbEx.InnerException?.Message}");
                Console.WriteLine($"❌ Stack Trace: {dbEx.StackTrace}");

                // Return more specific error
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                return BadRequest(new
                {
                    message = "Database error occurred",
                    detail = innerMessage,
                    type = "DatabaseError"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ General Error: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult> UpdateUser(Guid id, [FromBody] object updateData)
        {
            try
            {
                var result = await _userService.UpdateUserAsync(id, updateData);
                if (result)
                    return Ok(new { message = "User updated successfully" });
                else
                    return NotFound(new { message = "User not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/reset-password")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult> ResetUserPassword(Guid id)
        {
            try
            {
                var result = await _userService.ResetUserPasswordAsync(id);
                if (result != null)
                    return Ok(new
                    {
                        message = "Password reset successfully",
                        tempPassword = result
                    });
                else
                    return NotFound(new { message = "User not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/resend-first-login")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult> ResendFirstLogin(Guid id)
        {
            try
            {
                var ok = await _userService.ResendFirstLoginLinkAsync(id);
                if (!ok) return NotFound(new { message = "User not found or inactive" });
                return Accepted(new { message = "First login link queued for resend" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult> DeleteUser(Guid id)
        {
            try
            {
                var result = await _userService.DeleteUserAsync(id);
                if (result)
                    return Ok(new { message = "User deleted successfully" });
                else
                    return NotFound(new { message = "User not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw new Exception("Invalid user token");
            }
            return userId;
        }
    }
}