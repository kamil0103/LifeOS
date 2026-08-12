using System.Text;
using System.Text.Json;

namespace LifeOS.Infrastructure.AI;

/// <summary>
/// Cleans and repairs AI-generated JSON: strips markdown fences, extracts the
/// JSON object, and repairs truncated output (unterminated strings, missing
/// closing brackets/braces, dangling keys/commas).
/// </summary>
public static class JsonRepair
{
    public static string CleanAndRepair(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var cleaned = raw.Trim();

        // Strip markdown code fences
        if (cleaned.StartsWith("```json")) cleaned = cleaned[7..];
        else if (cleaned.StartsWith("```")) cleaned = cleaned[3..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..^3];
        cleaned = cleaned.Trim();

        // Skip anything before the first opening brace or bracket (object or array)
        var firstBrace = cleaned.IndexOf('{');
        var firstBracket = cleaned.IndexOf('[');
        int start;
        if (firstBrace < 0) start = firstBracket;
        else if (firstBracket < 0) start = firstBrace;
        else start = Math.Min(firstBrace, firstBracket);
        if (start < 0) return cleaned; // no JSON found; return as-is
        if (start > 0) cleaned = cleaned[start..];

        // Fast path: already valid
        if (IsValid(cleaned)) return cleaned;

        // Walk and track string/bracket state
        var stack = new Stack<char>();
        var inString = false;
        var escaped = false;
        var sb = new StringBuilder(cleaned.Length + 8);

        foreach (var c in cleaned)
        {
            if (escaped) { sb.Append(c); escaped = false; continue; }
            if (inString && c == '\\') { sb.Append(c); escaped = true; continue; }
            if (c == '"') { inString = !inString; sb.Append(c); continue; }
            if (inString) { sb.Append(c); continue; }
            if (c == '{' || c == '[') { stack.Push(c); sb.Append(c); continue; }
            if (c == '}' || c == ']')
            {
                if (stack.Count > 0) stack.Pop();
                sb.Append(c);
                continue;
            }
            sb.Append(c);
        }

        // Close an unterminated string
        if (inString) sb.Append('"');

        var result = sb.ToString().TrimEnd();

        // Drop a dangling key with no value (ends with ':')
        if (result.EndsWith(':'))
        {
            var idx = result.LastIndexOfAny(new[] { ',', '{', '[' });
            if (idx >= 0)
            {
                var ch = result[idx];
                result = result[..idx];
                if ((ch == '{' || ch == '[') && stack.Count > 0) stack.Pop();
            }
            result = result.TrimEnd();
        }

        // Drop trailing comma (invalid before a closing bracket)
        if (result.EndsWith(',')) result = result[..^1].TrimEnd();

        // Remove commas directly before closing brackets/braces anywhere
        result = System.Text.RegularExpressions.Regex.Replace(result, ",(\\s*[}\\]])", "$1");

        // Append missing closing brackets/braces in reverse order
        while (stack.Count > 0)
        {
            var open = stack.Pop();
            result += open == '{' ? '}' : ']';
        }

        return result;
    }

    private static bool IsValid(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
