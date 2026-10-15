using System.Globalization;
using System.Text.RegularExpressions;
namespace LifeOS.Application.Services;
public static class ProfessionalRecordRules
{
    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public static string Name(string? value, string label)
    {
        var text = Text(value); if (text == null || text.Length > 300) throw new InvalidOperationException($"Enter a {label} of at most 300 characters."); return text;
    }
    public static string? Url(string? value)
    {
        value = Text(value); if (value == null) return null;
        if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("Use an http or https URL without embedded credentials.");
        return value;
    }
    public static string GitHub(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            throw new InvalidOperationException("Use a repository URL such as https://github.com/owner/repository.");
        var parts = uri.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || !Regex.IsMatch(parts[0], "^[A-Za-z0-9][A-Za-z0-9-]{0,38}$") || !Regex.IsMatch(parts[1], "^[A-Za-z0-9_.-]{1,100}$"))
            throw new InvalidOperationException("Use the repository root URL, without a branch or file path.");
        var repo = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];
        if (repo is "" or "." or "..") throw new InvalidOperationException("Invalid repository name.");
        return $"https://github.com/{parts[0]}/{repo}";
    }
    public static (DateOnly Start, DateOnly End)? DateRange(string? value)
    {
        value = Text(value); if (value == null) return null;
        if (Regex.IsMatch(value, "^[0-9]{4}$") && int.TryParse(value, out var year) && year >= 1 && year <= 9999) return (new(year, 1, 1), new(year, 12, 31));
        if (Regex.IsMatch(value, "^[0-9]{4}-[0-9]{2}$") && DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
            return (month, new(month.Year, month.Month, DateTime.DaysInMonth(month.Year, month.Month)));
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return (day, day);
        throw new InvalidOperationException("Use dates in YYYY, YYYY-MM or YYYY-MM-DD format.");
    }
    public static void Dates(string? start, string? end)
    {
        var a = DateRange(start); var b = DateRange(end);
        if (a.HasValue && b.HasValue && a.Value.Start > b.Value.End) throw new InvalidOperationException("The end or expiry date cannot be before the start or obtained date.");
    }
    public static string Expiry(string? value, DateOnly today)
    {
        if (Text(value) == null) return "no_expiry";
        try { var end = DateRange(value)!.Value.End; return end < today ? "expired" : end.DayNumber - today.DayNumber <= 30 ? "expiring_soon" : "valid"; }
        catch (InvalidOperationException) { return "unknown"; }
    }
}
