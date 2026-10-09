using EventExamProject.Application.DTOs.Event;
using EventExamProject.Application.DTOs.Pagination;
using EventExamProject.Domain.Entities;

namespace EventExamProject.Application.Abstractions;

public interface IEventRepository
{
    Task<PaginatedResultDto<Event>> GetAllAsync(EventFilterDto filter, PaginationParamsDto paginationParams);
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Event newEvent);
    Task UpdateAsync(Event existingEvent);
    Task DeleteAsync(Event eventToDelete);
}
