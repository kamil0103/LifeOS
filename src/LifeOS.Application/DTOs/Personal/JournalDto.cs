namespace LifeOS.Application.DTOs.Personal;

public class JournalEntryDto
{
    public string LocalDate { get; set; } = "";
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset EntryDate { get; set; }
    public string? Mood { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateJournalEntryRequest
{
    public Guid? RequestId { get; set; }
    public string? Date { get; set; }
    public DateTimeOffset? ExpectedUpdatedAt { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset? EntryDate { get; set; }
    public string? Mood { get; set; }
}

public class UpdateJournalEntryRequest : CreateJournalEntryRequest { }

public class JournalSettingsDto
{
    public string? GoogleDocId { get; set; }
    public bool HasServiceAccount { get; set; }
    public string? ServiceAccountEmail { get; set; }
    public bool AutoSync { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
}

public class SaveJournalSettingsRequest
{
    public string? GoogleDocId { get; set; }
    public bool AutoSync { get; set; }
}
