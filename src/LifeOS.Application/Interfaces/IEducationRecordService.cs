using LifeOS.Application.DTOs.Education;
namespace LifeOS.Application.Interfaces;
public interface IEducationRecordService
{
    Task<DegreeDto?> SaveDegreeAsync(Guid userId, Guid? id, CreateDegreeRequest request, CancellationToken ct);
    Task<CourseDto?> SaveCourseAsync(Guid userId, Guid? id, CreateCourseRequest request, CancellationToken ct);
    Task<bool> DeleteDegreeAsync(Guid userId, Guid id, CancellationToken ct);
}
