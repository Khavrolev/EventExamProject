using EventExamProject.Application.DTOs.Event;
using EventExamProject.Application.DTOs.Pagination;

namespace EventExamProject.Application.Services.Interfaces;

public interface IEventService
{
    Task<PaginatedResultDto<EventInfoDto>> GetAllEventsAsync(EventFilterDto filter, PaginationParamsDto paginationParams);
    Task<EventInfoDto> GetEventByIdAsync(Guid id);
    Task<EventInfoDto> CreateEventAsync(EventDto newEvent);
    Task<EventInfoDto> UpdateEventAsync(Guid id, EventDto updatedEvent);
    Task DeleteEventAsync(Guid id);
}
