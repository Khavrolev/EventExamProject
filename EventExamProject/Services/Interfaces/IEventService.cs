using EventExamProject.DTOs;
using EventExamProject.Models;

namespace EventExamProject.Services.Interfaces;

public interface IEventService
{
    Task<List<Event>> GetAllEvents(EventFilterDto filter);
    Task<Event> GetEventById(Guid id);
    Task<Event> AddEvent(EventDto newEvent);
    Task<Event> UpdateEvent(Guid id, EventDto updatedEvent);
    Task<bool> DeleteEvent(Guid id);
}