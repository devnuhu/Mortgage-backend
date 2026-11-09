using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMGT.API.Exceptions;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Controllers
{
    /// <summary>
    /// Gives the application form the borrower's name and phone number from CPAS.
    /// Read-only: nothing is saved. The same lookup (and the same messages) is used again when an application is created.
    /// </summary>
    [ApiController]
    [Route("api/borrower-lookup")]
    [Authorize(Roles = "CLIENT,PMI,SERVICE_MANAGER")]
    public class BorrowerLookupController : ControllerBase
    {
        private readonly ICpasBiodataService _cpas;
        private readonly ILogger<BorrowerLookupController> _logger;

        public BorrowerLookupController(ICpasBiodataService cpas, ILogger<BorrowerLookupController> logger)
        {
            _cpas = cpas;
            _logger = logger;
        }

        [HttpGet("{pin}")]
        public async Task<IActionResult> Get(string pin, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(pin) || pin.Trim().Length > 20)
                return BadRequest(new { message = "Enter a valid RSA PIN." });

            try
            {
                var biodata = await _cpas.GetBiodataAsync(pin, cancellationToken);
                return Ok(new
                {
                    firstName = biodata.FirstName,
                    middleName = biodata.MiddleName,
                    lastName = biodata.LastName,
                    phoneNumber = biodata.PhoneNumber
                });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[LOOKUP] Unexpected error while looking up RSA details");
                return StatusCode(500, new { message = "We could not verify your RSA details right now. Please try again in a few minutes." });
            }
        }
    }
}