using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMGT.API.DTOs.Document;
using MMGT.API.Models;
using MMGT.API.Services;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentTypesController : ControllerBase
    {
        private readonly IDocumentTypeService _service;

        public DocumentTypesController(IDocumentTypeService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<DocumentTypeDto>>> GetAll()
        {
            var list = await _service.GetAllDocumentTypesAsync();
            var dtos = list.Select(dt => new DocumentTypeDto
            {
                Id = dt.Id,
                Code = dt.Code,
                Name = dt.Name,
                Description = dt.Description,
                IsMandatory = dt.IsMandatory,
                DisplayOrder = dt.DisplayOrder,
                MaxFileSizeKB = dt.MaxFileSizeKB,
                IsActive = dt.IsActive,
                CreatedAt = dt.CreatedAt,
                CreatedBy = dt.CreatedBy,
                ModifiedAt = dt.ModifiedAt,
                ModifiedBy = dt.ModifiedBy
            }).ToList();

            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DocumentTypeDto>> Get(Guid id)
        {
            var dt = await _service.GetDocumentTypeByIdAsync(id);
            if (dt == null) return NotFound();

            var dto = new DocumentTypeDto
            {
                Id = dt.Id,
                Code = dt.Code,
                Name = dt.Name,
                Description = dt.Description,
                IsMandatory = dt.IsMandatory,
                DisplayOrder = dt.DisplayOrder,
                MaxFileSizeKB = dt.MaxFileSizeKB,
                IsActive = dt.IsActive,
                CreatedAt = dt.CreatedAt,
                CreatedBy = dt.CreatedBy,
                ModifiedAt = dt.ModifiedAt,
                ModifiedBy = dt.ModifiedBy
            };

            return Ok(dto);
        }

        [HttpPost]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<DocumentTypeDto>> Create([FromBody] DocumentTypeCreateRequest req)
        {
            var userId = GetCurrentUserId();
            var dt = await _service.CreateDocumentTypeAsync(req.Code, req.Name, req.Description, req.IsMandatory, req.DisplayOrder, req.MaxFileSizeKB, userId);
            var dto = new DocumentTypeDto
            {
                Id = dt.Id,
                Code = dt.Code,
                Name = dt.Name,
                Description = dt.Description,
                IsMandatory = dt.IsMandatory,
                DisplayOrder = dt.DisplayOrder,
                MaxFileSizeKB = dt.MaxFileSizeKB,
                IsActive = dt.IsActive,
                CreatedAt = dt.CreatedAt,
                CreatedBy = dt.CreatedBy
            };
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult<DocumentTypeDto>> Update(Guid id, [FromBody] DocumentTypeUpdateRequest req)
        {
            var userId = GetCurrentUserId();
            var dt = await _service.UpdateDocumentTypeAsync(id, req.Name, req.Description, req.IsMandatory, req.DisplayOrder, req.MaxFileSizeKB, userId);
            var dto = new DocumentTypeDto
            {
                Id = dt.Id,
                Code = dt.Code,
                Name = dt.Name,
                Description = dt.Description,
                IsMandatory = dt.IsMandatory,
                DisplayOrder = dt.DisplayOrder,
                MaxFileSizeKB = dt.MaxFileSizeKB,
                IsActive = dt.IsActive,
                CreatedAt = dt.CreatedAt,
                CreatedBy = dt.CreatedBy,
                ModifiedAt = dt.ModifiedAt,
                ModifiedBy = dt.ModifiedBy
            };
            return Ok(dto);
        }

        [HttpPost("deactivate/{id}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult> Deactivate(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _service.DeactivateDocumentTypeAsync(id, userId);
            return result ? NoContent() : BadRequest();
        }

        [HttpPost("reactivate/{id}")]
        [Authorize(Roles = "SUPER_ADMIN")]
        public async Task<ActionResult> Reactivate(Guid id)
        {
            var userId = GetCurrentUserId();
            var result = await _service.ReactivateDocumentTypeAsync(id, userId);
            return result ? NoContent() : BadRequest();
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                throw new Exception("Invalid user token");
            return userId;
        }
    }
}
