using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MMGT.API.DTOs.PMI;
using MMGT.API.DTOs;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SUPER_ADMIN,ADMIN,LEAD_REVIEWER,REVIEWER")]
    public class PMIController : ControllerBase
    {
        private readonly IPMIService _pmiService;

        public PMIController(IPMIService pmiService)
        {
            _pmiService = pmiService;
        }

        [HttpGet]
        public async Task<ActionResult<List<PMICompanyDto>>> GetAllPMICompanies()
        {
            try
            {
                var pmiCompanies = await _pmiService.GetAllPMICompaniesAsync();
                return Ok(pmiCompanies);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("paged")]
        public async Task<ActionResult<PaginatedResponse<PMICompanyDto>>> GetAllPMICompaniesPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
                var result = await _pmiService.GetAllPMICompaniesPagedAsync(pagination);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PMICompanyDto>> GetPMICompany(Guid id)
        {
            try
            {
                var pmiCompany = await _pmiService.GetPMICompanyByIdAsync(id);
                return Ok(pmiCompany);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<PMICompanyDto>> CreatePMICompany([FromBody] CreatePMIRequest request)
        {
            try
            {
                var pmiCompany = await _pmiService.CreatePMICompanyAsync(request);
                return CreatedAtAction(nameof(GetPMICompany), new { id = pmiCompany.Id }, pmiCompany);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PMICompanyDto>> UpdatePMICompany(Guid id, [FromBody] UpdatePMIRequest request)
        {
            try
            {
                var pmiCompany = await _pmiService.UpdatePMICompanyAsync(id, request);
                return Ok(pmiCompany);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeletePMICompany(Guid id)
        {
            try
            {
                var result = await _pmiService.DeletePMICompanyAsync(id);
                if (result)
                    return Ok(new { message = "PMI Company deleted successfully" });
                else
                    return NotFound(new { message = "PMI Company not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPatch("{id}/status")]
        public async Task<ActionResult> UpdatePMIStatus(Guid id, [FromBody] UpdateStatusRequest request)
        {
            try
            {
                var result = await _pmiService.UpdatePMIStatusAsync(id, request.Status);
                if (result)
                    return Ok(new { message = "PMI Company status updated successfully" });
                else
                    return NotFound(new { message = "PMI Company not found" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }
}