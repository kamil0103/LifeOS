namespace LifeOS.Application.DTOs.Skills;

public class CertificateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? DateObtained { get; set; }
    public string? Expiry { get; set; }
    public string? CredentialId { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
}

public class CreateCertificateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? DateObtained { get; set; }
    public string? Expiry { get; set; }
    public string? CredentialId { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
}

public class UpdateCertificateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? DateObtained { get; set; }
    public string? Expiry { get; set; }
    public string? CredentialId { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
}
