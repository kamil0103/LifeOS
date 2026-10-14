using LifeOS.Application.Services;
using Xunit;
namespace LifeOS.Tests;
public class LocalDayTests
{
    [Theory]
    [InlineData("2026-03-08T12:00:00Z", 23)]
    [InlineData("2026-11-01T12:00:00Z", 25)]
    public void DaylightSavingDaysHaveCorrectBoundaries(string instant, int hours)
    {
        var day = LocalDay.At(DateTimeOffset.Parse(instant), "America/Los_Angeles");
        Assert.Equal(hours, (day.End - day.Start).TotalHours);
        Assert.Equal(day.Start, day.Offset(-1).End);
        Assert.Equal(day.End, day.Offset(1).Start);
    }
    [Fact]
    public void UtcMidnightIsStillPreviousDayInLosAngeles()
    {
        var day = LocalDay.At(DateTimeOffset.Parse("2026-10-15T01:00:00Z"), "America/Los_Angeles");
        Assert.Equal(new DateTime(2026, 10, 14), day.Date);
        Assert.Equal(DateTimeOffset.Parse("2026-10-14T07:00:00Z"), day.Start);
    }
    [Fact]
    public void InvalidZoneFallsBackToDefault()
    {
        Assert.Equal("America/Los_Angeles", LocalDay.At(DateTimeOffset.UtcNow, "missing").Zone.Id);
    }
    [Fact]
    public void ExportPreservesTextOutsideManagedSection()
    {
        var managed = ManagedExport.Wrap("old", "journal");
        var text = "Personal notes\n" + managed + "Other notes\n";
        var region = ManagedExport.Find(new[] { (1, text) }, "journal", text.Length);
        var result = text[..(region.Start - 1)] + ManagedExport.Wrap("new", "journal") + text[(region.End - 1)..];
        Assert.StartsWith("Personal notes\n", result);
        Assert.EndsWith("Other notes\n", result);
        Assert.Contains("new", result);
        Assert.DoesNotContain("old", result);
    }
    [Fact]
    public void FirstExportAppendsRatherThanReplacingExistingContent()
    {
        Assert.Equal((14, 14), ManagedExport.Find(new[] { (1, "Personal notes") }, "journal", 14));
    }
    [Fact]
    public void IncompleteMarkersFailWithoutDeletingContent()
    {
        Assert.Throws<InvalidOperationException>(() => ManagedExport.Find(new[] { (1, "[LifeOS journal export begins]\nnotes") }, "journal", 50));
    }
}
