using System.Xml.Linq;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JobDiscoveryController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public JobDiscoveryController(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(sub!);
    }

    [HttpGet("rss")]
    public async Task<ActionResult<List<RssJobDto>>> FetchRss([FromQuery] string url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest(new ProblemDetails { Title = "URL required", Detail = "Provide an RSS feed URL." });

        var client = _httpClientFactory.CreateClient();
        try
        {
            var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return BadRequest(new ProblemDetails { Title = "Fetch failed", Detail = "Could not fetch RSS feed." });

            var xml = await response.Content.ReadAsStringAsync(ct);
            var doc = XDocument.Parse(xml);
            var ns = XNamespace.Get("http://purl.org/dc/elements/1.1/");

            var items = doc.Descendants("item").Select(item => new RssJobDto
            {
                Title = item.Element("title")?.Value ?? "Untitled",
                Description = item.Element("description")?.Value?.Substring(0, Math.Min(500, item.Element("description")?.Value?.Length ?? 0)),
                Link = item.Element("link")?.Value ?? "",
                PublishedAt = item.Element("pubDate")?.Value,
                Company = item.Element(ns + "creator")?.Value ?? item.Element("author")?.Value
            }).Take(20).ToList();

            return items;
        }
        catch (Exception ex)
        {
            return BadRequest(new ProblemDetails { Title = "RSS parse failed", Detail = ex.Message });
        }
    }

    [HttpPost("rss/import")]
    public async Task<ActionResult<Job>> ImportRssJob([FromBody] ImportRssJobRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        var job = new Job
        {
            UserId = userId,
            Title = request.Title,
            Company = request.Company ?? "Unknown",
            Description = request.Description ?? "",
            Url = request.Link,
            Location = request.Location,
            Status = "saved",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(ct);
        return job;
    }
}

public class RssJobDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Link { get; set; }
    public string? PublishedAt { get; set; }
    public string? Company { get; set; }
}

public class ImportRssJobRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Link { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
}
