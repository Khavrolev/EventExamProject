using Events.Application.DTOs;

namespace Events.Application.Services.Interfaces;

public interface IEventService
{
    Task<PaginatedResultDto<EventInfoDto>> GetAllEventsAsync(EventFilterDto filter, PaginationParamsDto paginationParams);
    Task<IReadOnlyList<EventInfoDto>> GetTopPopularEventsAsync(int count = 10);
    Task<EventInfoDto> GetEventByIdAsync(Guid id);
    Task<EventInfoDto> CreateEventAsync(EventDto newEvent);
    Task<EventInfoDto> UpdateEventAsync(Guid id, EventDto updatedEvent);
    Task DeleteEventAsync(Guid id);
}
