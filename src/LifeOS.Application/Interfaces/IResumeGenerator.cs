using LifeOS.Application.DTOs.Documents;

namespace LifeOS.Application.Interfaces;

public interface IResumeGenerator
{
    Task<byte[]> GenerateResumePdfAsync(ResumeDataDto data, string template, CancellationToken ct = default);
    Task<byte[]> GenerateCoverLetterPdfAsync(CoverLetterDataDto data, CancellationToken ct = default);
}

public interface IDocumentStorage
{
    Task<string> SaveAsync(byte[] content, string filename, string userId, CancellationToken ct = default);
    Task<byte[]> LoadAsync(string storagePath, CancellationToken ct = default);
    void Delete(string storagePath);
}
