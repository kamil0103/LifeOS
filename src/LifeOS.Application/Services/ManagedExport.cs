namespace LifeOS.Application.Services;

public static class ManagedExport
{
    private static string Begin(string section) => $"[LifeOS {section} export begins]\n";
    private static string End(string section) => $"[LifeOS {section} export ends]\n";
    public static string Wrap(string content, string section) => "\n" + Begin(section) + content.TrimEnd() + "\n" + End(section);

    public static (int Start, int End) Find(IEnumerable<(int Index, string Text)> runs, string section, int appendIndex)
    {
        var text = new System.Text.StringBuilder();
        var indexes = new List<int>();
        foreach (var run in runs)
        {
            text.Append(run.Text);
            for (var i = 0; i < run.Text.Length; i++) indexes.Add(run.Index + i);
        }
        var body = text.ToString();
        var start = body.IndexOf(Begin(section), StringComparison.Ordinal);
        if (start < 0) return (appendIndex, appendIndex);
        var end = body.IndexOf(End(section), start, StringComparison.Ordinal);
        if (end < 0 || body.IndexOf(Begin(section), start + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("LifeOS export markers are incomplete or duplicated. Restore them before syncing.");
        var last = end + End(section).Length - 1;
        var first = start > 0 && body[start - 1] == '\n' ? start - 1 : start;
        return (indexes[first], indexes[last] + 1);
    }
}
