using Microsoft.AspNetCore.Mvc;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public ActionResult Get()
        {
            return Ok(new
            {
                message = "MMGT API is running!",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        [HttpGet("database")]
        public ActionResult TestDatabase()
        {
            return Ok(new
            {
                message = "Database connection test endpoint",
                status = "Ready for implementation"
            });
        }
    }
}