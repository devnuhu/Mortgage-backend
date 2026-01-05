using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MMGT.API.DTOs.File;
using MMGT.API.Services.Interfaces;

namespace MMGT.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class FileController : ControllerBase
    {
        private readonly IFileService _fileService;
        private readonly IApplicationService _applicationService;

        public FileController(IFileService fileService, IApplicationService applicationService)
        {
            _fileService = fileService;
            _applicationService = applicationService;
        }

        [HttpPost("upload")]
        [Authorize(Roles = "CLIENT")]
        public async Task<ActionResult<FileUploadResponse>> Upload([FromForm] FileUploadForm form)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Verify application ownership and status
                var app = await _applicationService.GetApplicationByIdAsync(form.ApplicationId);
                if (app.UserId != userId)
                    return Forbid();

                if (app.Status != "rejected" && app.Status != "draft")
                    return Forbid(); // allow upload in draft or when rejected

                var response = await _fileService.UploadFileAsync(form.File, form.ApplicationId, form.DocumentType, userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("replace")]
        [Authorize(Roles = "CLIENT")]
        public async Task<ActionResult<FileUploadResponse>> Replace([FromForm] FileReplaceForm form)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Validate existing file and application
                var existing = await _fileService.GetFileAsync(form.ExistingFileId);
                var app = await _applicationService.GetApplicationByIdAsync(existing.ApplicationId);

                // Ensure owner and rejected status
                if (app.UserId != userId)
                    return Forbid();

                if (app.Status != "rejected")
                    return Forbid();

                var response = await _fileService.ReplaceFileAsync(form.ExistingFileId, form.File, userId);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("application/{applicationId}")]
        public async Task<ActionResult<List<FileUploadResponse>>> GetFilesByApplication(Guid applicationId)
        {
            try
            {
                var files = await _fileService.GetFilesByApplicationAsync(applicationId);
                return Ok(files);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("download/{fileId}")]
        public async Task<IActionResult> DownloadFile(Guid fileId)
        {
            try
            {
                var document = await _fileService.GetFileAsync(fileId);
                var fileBytes = await _fileService.DownloadFileAsync(fileId);
                return File(fileBytes, document.FileType, document.FileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{fileId}")]
        [Authorize(Roles = "CLIENT")]
        public async Task<ActionResult> DeleteFile(Guid fileId)
        {
            try
            {
                var userId = GetCurrentUserId();

                var document = await _fileService.GetFileAsync(fileId);
                var app = await _applicationService.GetApplicationByIdAsync(document.ApplicationId);

                if (app.UserId != userId)
                    return Forbid();

                if (app.Status != "rejected" && app.Status != "draft")
                    return Forbid();

                var result = await _fileService.DeleteFileAsync(fileId, userId);
                return result ? Ok(new { message = "File deleted successfully" })
                              : NotFound(new { message = "File not found" });
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