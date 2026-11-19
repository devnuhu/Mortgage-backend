using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MMGT.API.DTOs.StepDown;
using MMGT.API.Models;
using MMGT.API.Services;

namespace MMGT.API.Controllers
{
    /// <summary>
    /// Step-down (archive) requests with maker-checker approval.
    /// Maker = SERVICE_MANAGER, approver = HOD_BENEFIT_ADMIN, checker = MORTGAGE_LEAD (see StepDownRoles).
    /// The service repeats every role and "not your own request" check, so these attributes are the first gate, not the only one.
    /// </summary>
    [ApiController]
    [Route("api/stepdown")]
    [Authorize]
    public class StepDownController : ControllerBase
    {
        private const string ReadRoles = "LEAD_REVIEWER," + StepDownRoles.Maker + "," + StepDownRoles.Approver + "," + StepDownRoles.Checker + ",SUPER_ADMIN,ADMIN";
        private readonly IStepDownService _service;

        public StepDownController(IStepDownService service)
        {
            _service = service;
        }

        private StepDownActor Actor()
        {
            var idClaim = User.FindFirst("UserId");
            if (idClaim == null || !Guid.TryParse(idClaim.Value, out var userId))
                throw new InvalidOperationException("User not authenticated.");
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? string.Empty;
            return new StepDownActor { UserId = userId, Role = role };
        }

        private async Task<IActionResult> Run<T>(Func<StepDownActor, Task<T>> action)
        {
            try { return Ok(await action(Actor())); }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // ----- maker -----
        [HttpPost]
        [Authorize]
        public Task<IActionResult> Create([FromBody] CreateStepDownRequest body) =>
            Run(a => _service.CreateAsync(body.ApplicationId, body.Reason, a));

        [HttpPost("{id:guid}/submit")]
        [Authorize]
        public Task<IActionResult> Submit(Guid id) => Run(a => _service.SubmitAsync(id, a));

        [HttpPost("{id:guid}/cancel-draft")]
        [Authorize]
        public Task<IActionResult> CancelDraft(Guid id, [FromBody] StepDownReasonRequest? body) =>
            Run(a => _service.CancelDraftAsync(id, body?.Reason, a));

        [HttpPost("{id:guid}/remove")]
        [Authorize]
        public Task<IActionResult> Remove(Guid id) => Run(a => _service.RemoveDraftAsync(id, a));

        [HttpPost("{id:guid}/request-cancellation")]
        [Authorize]
        public Task<IActionResult> RequestCancellation(Guid id, [FromBody] StepDownReasonRequest body) =>
            Run(a => _service.RequestCancellationAsync(id, body.Reason, a));

        // ----- Head of Benefit Administration -----
        [HttpPost("{id:guid}/hod-approve")]
        [Authorize(Roles = StepDownRoles.Approver)]
        public Task<IActionResult> HodApprove(Guid id, [FromBody] StepDownCommentRequest? body) =>
            Run(a => _service.HodApproveAsync(id, body?.Comment, a));

        [HttpPost("{id:guid}/hod-reject")]
        [Authorize(Roles = StepDownRoles.Approver)]
        public Task<IActionResult> HodReject(Guid id, [FromBody] StepDownCommentRequest body) =>
            Run(a => _service.HodRejectAsync(id, body.Comment ?? string.Empty, a));

        // ----- checker -----
        [HttpPost("{id:guid}/step-down")]
        [Authorize(Roles = StepDownRoles.Checker)]
        public Task<IActionResult> StepDown(Guid id, [FromBody] StepDownCommentRequest? body) =>
            Run(a => _service.StepDownAsync(id, body?.Comment, a));

        [HttpPost("{id:guid}/checker-reject")]
        [Authorize(Roles = StepDownRoles.Checker)]
        public Task<IActionResult> CheckerReject(Guid id, [FromBody] StepDownCommentRequest body) =>
            Run(a => _service.CheckerRejectAsync(id, body.Comment ?? string.Empty, a));

        [HttpPost("{id:guid}/restore")]
        [Authorize(Roles = StepDownRoles.Checker)]
        public Task<IActionResult> Restore(Guid id, [FromBody] StepDownReasonRequest body) =>
            Run(a => _service.RestoreAsync(id, body.Reason, a));

        // ----- whoever holds the request decides a cancellation (HOD before approval, checker after it) -----
        [HttpPost("{id:guid}/decide-cancellation")]
        [Authorize(Roles = StepDownRoles.Approver + "," + StepDownRoles.Checker)]
        public Task<IActionResult> DecideCancellation(Guid id, [FromBody] StepDownDecisionRequest body) =>
            Run(a => _service.DecideCancellationAsync(id, body.Accept, body.Comment, a));

        // ----- signed request letter from the client (PDF, JPG or PNG, smaller than 500 KB). Upload while the request is a draft. -----
        [HttpPost("{id:guid}/letter")]
        [Authorize]
        [RequestSizeLimit(1048576)]
        public Task<IActionResult> UploadLetter(Guid id, IFormFile? file) =>
            Run(async a =>
            {
                if (file == null || file.Length == 0) throw new InvalidOperationException("Choose the signed request letter to upload.");
                if (file.Length >= StepDownRouting.MaxLetterBytes)
                    throw new InvalidOperationException($"The signed request letter must be smaller than 500 KB. This file is {file.Length / 1024.0:0.#} KB.");
                using var ms = new MemoryStream((int)file.Length);
                await file.CopyToAsync(ms);
                return await _service.SaveLetterAsync(id, file.FileName, ms.ToArray(), a);
            });
        
        [HttpGet("{id:guid}/letter")]
        [Authorize(Roles = ReadRoles)]
        public async Task<IActionResult> DownloadLetter(Guid id)
        {
            try
            {
                var f = await _service.GetLetterAsync(id);
                return f == null ? NotFound(new { message = "No letter has been uploaded for this request." }) : File(f.Content, f.ContentType, f.FileName);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }
        
        // ----- reading -----
        [HttpGet]
        [Authorize(Roles = ReadRoles)]
        public Task<IActionResult> List([FromQuery] string? scope, [FromQuery] string? status,
            [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50) =>
            Run(a => _service.ListAsync(scope, status, a, pageNumber, pageSize));

        [HttpGet("{id:guid}")]
        [Authorize(Roles = ReadRoles)]
        public async Task<IActionResult> Get(Guid id)
        {
            try
            {
                var d = await _service.GetAsync(id);
                return d == null ? NotFound(new { message = "Step-down request not found." }) : Ok(d);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        [HttpGet("summary")]
        [Authorize(Roles = ReadRoles)]
        public Task<IActionResult> Summary() => Run(_ => _service.GetSummaryAsync());

        [HttpGet("{id:guid}/audit")]
        [Authorize(Roles = ReadRoles)]
        public Task<IActionResult> Audit(Guid id) => Run(_ => _service.GetAuditAsync(id));

        [HttpGet("application/{applicationId:guid}")]
        [Authorize(Roles = ReadRoles)]
        public Task<IActionResult> ForApplication(Guid applicationId) =>
            Run(_ => _service.ListForApplicationAsync(applicationId));
    }
}
