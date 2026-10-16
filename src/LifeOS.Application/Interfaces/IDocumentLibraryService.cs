using LifeOS.Application.DTOs.Documents;
namespace LifeOS.Application.Interfaces;
public interface IDocumentLibraryService
{
    Task<List<GeneratedDocumentDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<byte[]?> PreviewAsync(Guid userId, Guid id, CancellationToken ct);
    Task<GeneratedDocumentDto?> UpdateLinksAsync(Guid userId, Guid id, DocumentLinksRequest request, CancellationToken ct);
    Task<DocumentLinkTargets> ResolveLinksAsync(Guid userId, DocumentLinksRequest request, CancellationToken ct);
}
