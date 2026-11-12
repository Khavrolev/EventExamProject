using Events.Application.DTOs;
using Events.Domain.Entities;

namespace Events.Application.Abstractions;

public interface IEventRepository
{
    Task<PaginatedResultDto<Event>> GetAllAsync(EventFilterDto filter, PaginationParamsDto paginationParams);
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Event newEvent);
    Task UpdateAsync(Event existingEvent);
    Task DeleteAsync(Event eventToDelete);
}
