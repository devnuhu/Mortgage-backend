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

        #region Reviewer Selection (for Assignment/Reassignment)

        /// <summary>
        /// Get all active ADMIN users (reviewers) for selection in assignment workflows
        /// Accessible to Lead Reviewers for manual assignment/reassignment operations
        /// </summary>
        [HttpGet("reviewers")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN,SUPER_ADMIN")]
        public async Task<ActionResult<List<ReviewerDto>>> GetAllReviewers()
        {
            try
            {
                var reviewers = await _context.Users
                    .Where(u => u.IsActive && u.Role == "ADMIN")
                    .Select(u => new ReviewerDto
                    {
                        Id = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        Role = u.Role,
                        IsActive = u.IsActive
                    })
                    .OrderBy(r => r.FirstName)
                    .ThenBy(r => r.LastName)
                    .ToListAsync();

                return Ok(reviewers);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get only available ADMIN users (reviewers) not on leave or marked unavailable
        /// Useful for Lead Reviewers to see who is currently available for assignment
        /// </summary>
        [HttpGet("reviewers/available")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN,SUPER_ADMIN")]
        public async Task<ActionResult<List<ReviewerDto>>> GetAvailableReviewers()
        {
            try
            {
                var availableReviewers = await _context.Users
                    .Where(u => u.IsActive && u.Role == "ADMIN")
                    .ToListAsync();

                // Filter by availability status from ReviewerAvailability table
                var availabilityStatuses = await _context.ReviewerAvailabilities
                    .AsNoTracking()
                    .Where(a => a.Status == "Available")
                    .Select(a => a.ReviewerId)
                    .ToListAsync();

                var reviewers = availableReviewers
                    .Where(u => availabilityStatuses.Contains(u.Id))
                    .Select(u => new ReviewerDto
                    {
                        Id = u.Id,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        Role = u.Role,
                        IsActive = u.IsActive
                    })
                    .OrderBy(r => r.FirstName)
                    .ThenBy(r => r.LastName)
                    .ToList();

                return Ok(reviewers);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get ADMIN users (reviewers) with their current workload and availability information
        /// Helps Lead Reviewers make informed assignment decisions based on workload distribution
        /// </summary>
        [HttpGet("reviewers/with-workload")]
        [Authorize(Roles = "LEAD_REVIEWER,ADMIN,SUPER_ADMIN")]
        public async Task<ActionResult<List<ReviewerWithWorkloadDto>>> GetReviewersWithWorkload()
        {
            try
            {
                var reviewers = await _context.Users
                    .Where(u => u.IsActive && u.Role == "ADMIN")
                    .ToListAsync();

                // Get workload for each reviewer
                var workloads = await _context.ApplicationAssignments
                    .Where(a => a.IsActive)
                    .GroupBy(a => a.ReviewerId)
                    .Select(g => new { ReviewerId = g.Key, Count = g.Count() })
                    .ToListAsync();

                // Get availability information
                var availabilityData = await _context.ReviewerAvailabilities
                    .AsNoTracking()
                    .ToListAsync();

                // Get performance metrics
                var performanceMetrics = await _context.ReviewerPerformanceMetrics
                    .AsNoTracking()
                    .ToListAsync();

                var result = reviewers
                    .Select(u =>
                    {
                        var workload = workloads.FirstOrDefault(w => w.ReviewerId == u.Id)?.Count ?? 0;
                        var availability = availabilityData.FirstOrDefault(a => a.ReviewerId == u.Id);
                        var performance = performanceMetrics.FirstOrDefault(p => p.ReviewerId == u.Id);

                        return new ReviewerWithWorkloadDto
                        {
                            Id = u.Id,
                            FirstName = u.FirstName,
                            LastName = u.LastName,
                            Email = u.Email,
                            Role = u.Role,
                            IsActive = u.IsActive,
                            ActiveAssignmentCount = workload,
                            IsAvailable = availability?.Status == "Available" || availability == null,
                            ApprovalRatePercentage = performance?.ApprovalRatePercentage ?? 0,
                            PerformanceRating = DeterminePerformanceRating(performance?.ApprovalRatePercentage ?? 0),
                            UnavailabilityReason = availability?.Reason,
                            UnavailableUntil = availability?.StatusEndDate
                        };
                    })
                    .OrderBy(r => !r.IsAvailable) // Available reviewers first
                    .ThenBy(r => r.ActiveAssignmentCount) // Then by workload (lowest first)
                    .ThenBy(r => r.FirstName)
                    .ThenBy(r => r.LastName)
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        #endregion

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                throw new Exception("Invalid user token");
            }
            return userId;
        }

        /// <summary>
        /// Determine performance rating based on approval rate percentage
        /// </summary>
        private string DeterminePerformanceRating(double approvalRate)
        {
            if (approvalRate >= 90) return "Excellent";
            if (approvalRate >= 80) return "Good";
            if (approvalRate >= 70) return "Satisfactory";
            if (approvalRate >= 60) return "Fair";
            return "Needs Improvement";
        }
    }
}