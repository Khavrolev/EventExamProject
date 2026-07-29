using EventExamProject.DTOs;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Models;

namespace EventExamProject.Services.Interfaces;

public interface IEventService
{
    Task<PaginatedResultDto<Event>> GetAllEventsAsync(EventFilterDto filter, PaginationParamsDto paginationParams);
    Task<Event> GetEventByIdAsync(Guid id);
    Task<Event> CreateEventAsync(EventDto newEvent);
    Task<Event> UpdateEventAsync(Guid id, EventDto updatedEvent);
    Task DeleteEventAsync(Guid id);
}
