using LifeOS.Application.DTOs.Jobs;
using LifeOS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LifeOS.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyNotesController(ICompanyResearchService research) : ControllerBase
{
    private Guid UserId => Guid.Parse((User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub"))!.Value);
    [HttpGet]
    public async Task<ActionResult<List<CompanyNoteDto>>> GetNotes(CancellationToken ct) => await research.ListAsync(UserId, ct);
    [HttpPost]
    public async Task<ActionResult<CompanyNoteDto>> CreateNote(CreateCompanyNoteRequest r, CancellationToken ct) => Ok(await research.SaveAsync(UserId, null, r, ct));
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CompanyNoteDto>> UpdateNote(Guid id, UpdateCompanyNoteRequest r, CancellationToken ct) => await research.SaveAsync(UserId, id, r, ct) is { } result ? Ok(result) : NotFound();
    [HttpGet("{id:guid}/jobs")]
    public async Task<ActionResult<List<CompanyRelatedJobDto>>> RelatedJobs(Guid id, CancellationToken ct) => await research.RelatedJobsAsync(UserId, id, ct) is { } result ? Ok(result) : NotFound();
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken ct) => await research.DeleteAsync(UserId, id, ct) ? NoContent() : NotFound();
}
