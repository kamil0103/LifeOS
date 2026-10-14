namespace LifeOS.Application.Services;

public record LocalDay(DateTime Date, TimeZoneInfo Zone, DateTimeOffset Start, DateTimeOffset End)
{
    public static LocalDay At(DateTimeOffset instant, string? zoneId)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId ?? "America/Los_Angeles"); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"); }
        catch (InvalidTimeZoneException) { zone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles"); }
        return ForDate(TimeZoneInfo.ConvertTime(instant, zone).Date, zone);
    }

    public LocalDay Offset(int days) => ForDate(Date.AddDays(days), Zone);
    public DateTime DateOf(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).Date;

    private static LocalDay ForDate(DateTime date, TimeZoneInfo zone)
    {
        var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        var end = start.AddDays(1);
        // Some zones advance their clocks at midnight.
        while (zone.IsInvalidTime(start)) start = start.AddMinutes(1);
        while (zone.IsInvalidTime(end)) end = end.AddMinutes(1);
        return new(date.Date, zone,
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(start, zone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(end, zone)));
    }
}
