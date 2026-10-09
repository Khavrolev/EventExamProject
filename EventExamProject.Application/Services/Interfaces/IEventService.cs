using EventExamProject.Application.DTOs.Event;
using EventExamProject.Application.DTOs.Pagination;
using EventExamProject.Domain.Entities;

namespace EventExamProject.Application.Services.Interfaces;

public interface IEventService
{
    Task<PaginatedResultDto<Event>> GetAllEventsAsync(EventFilterDto filter, PaginationParamsDto paginationParams);
    Task<Event> GetEventByIdAsync(Guid id);
    Task<Event> CreateEventAsync(EventDto newEvent);
    Task<Event> UpdateEventAsync(Guid id, EventDto updatedEvent);
    Task DeleteEventAsync(Guid id);
}
