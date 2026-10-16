using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController, Authorize]
[Route("api/job-applications/{applicationId:guid}/interviews")]
public class ApplicationInterviewsController(IApplicationInterviewService interviews) : ControllerBase
{
    private Guid UserId => Guid.Parse((User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub"))!.Value);
    [HttpGet] public async Task<ActionResult<List<ApplicationInterviewDto>>> List(Guid applicationId, CancellationToken ct) => await interviews.ListAsync(UserId, applicationId, ct) is { } result ? Ok(result) : NotFound();
    [HttpPost] public async Task<ActionResult<ApplicationInterviewDto>> Create(Guid applicationId, SaveInterviewRequest request, CancellationToken ct) => await interviews.SaveAsync(UserId, applicationId, null, request, ct) is { } result ? Ok(result) : NotFound();
    [HttpPut("{id:guid}")] public async Task<ActionResult<ApplicationInterviewDto>> Update(Guid applicationId, Guid id, SaveInterviewRequest request, CancellationToken ct) => await interviews.SaveAsync(UserId, applicationId, id, request, ct) is { } result ? Ok(result) : NotFound();
}
