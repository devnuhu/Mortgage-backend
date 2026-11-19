using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMGT.API.Models;

namespace MMGT.API.Controllers
{
    /// <summary>The roles a user can be given. Used by the create-user and edit-user screens.</summary>
    [ApiController]
    [Route("api/roles")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public class RolesController : ControllerBase
    {
        [HttpGet]
        public ActionResult<IEnumerable<RoleInfo>> GetRoles() => Ok(UserRoles.All);
    }
}
