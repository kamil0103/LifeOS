namespace LifeOS.Domain.Entities;

public class Certificate : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? DateObtained { get; set; }
    public string? Expiry { get; set; }
    public string? CredentialId { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
}
