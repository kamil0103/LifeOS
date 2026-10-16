using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LifeOS.Application.DTOs.Coach;
namespace LifeOS.Application.Services;
public static class MissionRules
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public static List<MissionPriority> Read(Guid missionId, string json)
    {
        var items = JsonSerializer.Deserialize<List<MissionPriority>>(json, Json) ?? [];
        for (var i = 0; i < items.Count; i++) if (string.IsNullOrEmpty(items[i].Id)) items[i].Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(missionId + ":" + i)))[..32].ToLowerInvariant();
        return items;
    }
    public static DateTimeOffset NextVersion(DateTimeOffset previous) => DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), previous.ToUnixTimeMilliseconds() + 1));
    public static List<MissionPriority> Validate(List<MissionPriority>? items, IReadOnlyList<MissionPriority> originals)
    {
        if (items == null || items.Count is < 1 or > 10 || items.Any(p => p == null)) throw new InvalidOperationException("Review between 1 and 10 mission tasks.");
        var result = new List<MissionPriority>(); var seen = new HashSet<string>();
        foreach (var p in items) {
            var old = originals.FirstOrDefault(o => o.Id == p.Id);
            if (!string.IsNullOrEmpty(p.Id) && old == null) throw new InvalidOperationException("A task does not belong to this mission draft. Reload before editing.");
            var id = old?.Id ?? Guid.NewGuid().ToString("N"); if (!seen.Add(id)) throw new InvalidOperationException("Each mission task must appear once.");
            if (old?.Completed == true) {
                if(p.Title!=old.Title||p.Category!=old.Category||p.Priority!=old.Priority||p.Reason!=old.Reason)throw new InvalidOperationException("Completed tasks cannot be rewritten. Add a new task instead.");
                result.Add(new() { Id=old.Id,Title=old.Title,Category=old.Category,Priority=old.Priority,Reason=old.Reason,Completed=true });continue;
            }
            var title = p.Title?.Trim(); var category = p.Category?.Trim(); var priority = p.Priority?.ToLowerInvariant(); var reason = p.Reason?.Trim() ?? "";
            if (string.IsNullOrEmpty(title) || title.Length > 200 || string.IsNullOrEmpty(category) || category.Length > 40 || reason.Length > 1000 || !new[] { "high", "medium", "low" }.Contains(priority)) throw new InvalidOperationException("Each task needs a title (200 characters), category (40), listed priority and reason of at most 1000 characters.");
            if (old?.Completed == true && (title != old.Title || category != old.Category || priority != old.Priority || reason != old.Reason)) throw new InvalidOperationException("Completed tasks cannot be rewritten. Add a new task instead.");
            result.Add(new() { Id = id, Title = title, Category = category, Priority = priority!, Reason = reason, Completed = old?.Completed ?? false });
        }
        if (originals.Any(o => o.Completed && !seen.Contains(o.Id))) throw new InvalidOperationException("Keep completed tasks in the mission.");
        return result;
    }
}
