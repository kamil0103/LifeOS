namespace LifeOS.Application.DTOs.Skills;
public record EvidenceSourceDto(string Type, Guid Id, string Label, string Url);
public record SkillEvidenceDto(Guid Id, string SourceType, Guid SourceId, string? Notes, string Label, string? Url, bool Available);
public record SaveSkillEvidenceRequest(string SourceType, Guid SourceId, string? Notes);
