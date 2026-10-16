using LifeOS.Application.DTOs.Documents;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController, Authorize, Route("api/resume-draft")]
public class ResumeDraftController(IResumeDraftService drafts, ICurrentUserService user) : ControllerBase
{
    private Guid UserId => user.UserId ?? throw new UnauthorizedAccessException();
    [HttpGet] public async Task<ActionResult<ResumeDraftDto>> Get(CancellationToken ct) => await drafts.GetAsync(UserId, ct) is { } result ? Ok(result) : NotFound();
    [HttpPut] public Task<ResumeDraftDto> Save(SaveResumeDraftRequest request, CancellationToken ct) => drafts.SaveAsync(UserId, request, ct);
    [HttpPost("preview")] public async Task<IActionResult> Preview(SaveResumeDraftRequest request, CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return File(await drafts.PreviewAsync(request.Data, ct), "application/pdf"); }
}
