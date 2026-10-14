namespace LifeOS.Application.Services;
public static class JobIdentity
{
    public static string? CanonicalUrl(string? input)
    {
        if(string.IsNullOrWhiteSpace(input))return null;
        if(input.Length>2048 || !Uri.TryCreate(input.Trim(),UriKind.Absolute,out var uri) || (uri.Scheme!="https" && uri.Scheme!="http") || !string.IsNullOrEmpty(uri.UserInfo))throw new InvalidOperationException("Enter an HTTP or HTTPS job URL without credentials.");
        var builder=new UriBuilder(uri){Host=uri.Host.ToLowerInvariant(),Path=uri.AbsolutePath.TrimEnd('/')};
        var query=uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Where(p=>{var key=Uri.UnescapeDataString(p.Split('=')[0]);return !key.StartsWith("utm_",StringComparison.OrdinalIgnoreCase)&&!key.Equals("gclid",StringComparison.OrdinalIgnoreCase)&&!key.Equals("fbclid",StringComparison.OrdinalIgnoreCase);}).OrderBy(p=>p,StringComparer.Ordinal);
        builder.Query=string.Join('&',query);return builder.Uri.AbsoluteUri;
    }
    public static string Text(string? input)=>input?.Trim().ToLowerInvariant()??"";
}
