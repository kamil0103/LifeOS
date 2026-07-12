using LifeOS.Application.DTOs.Documents;

namespace LifeOS.Application.Interfaces;

public interface IResumeDataBuilder
{
    Task<ResumeDataDto> BuildAsync(Guid userId, CancellationToken ct = default);
}
