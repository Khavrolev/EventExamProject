using EventExamProject.DTOs;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

public class EventService :IEventService
{
    private readonly List<Event> _events = [];
    
    public Task<List<Event>> GetAllEvents()
    {
        return Task.FromResult(_events);
    }

    public Task<Event?> GetEventById(Guid id)
    {
        return Task.FromResult(_events.Find(e => e.Id.Equals(id) ));
    }
    
    public Task<Event> AddEvent(EventDto newEvent)
    {
        var newEventEntity = new Event
        {
            Id = Guid.NewGuid(),
            Title = newEvent.Title,
            Description = newEvent.Description,
            StartAt = newEvent.StartAt,
            EndAt = newEvent.EndAt
        };  
        _events.Add(newEventEntity);
        
        return Task.FromResult(newEventEntity);

    }

    public Task<Event?> UpdateEvent(Guid id, EventDto updatedEvent)
    {
        var index = _events.FindIndex(e => e.Id.Equals(id));
        
        if (index == -1)
        {
            return Task.FromResult<Event?>(null);
        }

        var updatedEntity = new Event
        {
            Id = id,
            Title = updatedEvent.Title,
            Description = updatedEvent.Description,
            StartAt = updatedEvent.StartAt,
            EndAt = updatedEvent.EndAt
        };
        _events[index] = updatedEntity;
        
        return Task.FromResult<Event?>(updatedEntity);
    }

    public Task<bool> DeleteEvent(Guid id)
    {
        var eventToDelete = _events.Find(e => e.Id.Equals(id));
        
        if (eventToDelete == null)
        {
            return Task.FromResult(false);
        }
        
        _events.Remove(eventToDelete);
        
        return Task.FromResult(true);
    }
}