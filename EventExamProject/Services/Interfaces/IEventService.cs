using EventExamProject.DTOs;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Models;

namespace EventExamProject.Services.Interfaces;

public interface IEventService
{
    Task<PaginatedResult<Event>> GetAllEvents(EventFilterDto filter, PaginationParams paginationParams);
    Task<Event> GetEventById(Guid id);
    Task<Event> AddEvent(EventDto newEvent);
    Task<Event> UpdateEvent(Guid id, EventDto updatedEvent);
    Task DeleteEvent(Guid id);
}