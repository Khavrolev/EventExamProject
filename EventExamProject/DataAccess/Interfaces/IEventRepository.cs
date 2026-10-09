using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Domain.Entities;

namespace EventExamProject.DataAccess.Interfaces;

public interface IEventRepository
{
    Task<PaginatedResultDto<Event>> GetAllAsync(EventFilterDto filter, PaginationParamsDto paginationParams);
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Event newEvent);
    Task UpdateAsync(Event existingEvent);
    Task DeleteAsync(Event eventToDelete);
}
