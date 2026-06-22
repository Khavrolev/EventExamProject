using EventExamProject.DTOs;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

public class EventService :IEventService
{
    private static List<Event> Events { get; } = [];
    
    public Task<List<Event>> GetAllEvents()
    {
        return Task.FromResult(Events);
    }

    public Task<Event?> GetEventById(Guid id)
    {
        return Task.FromResult(Events.Find(e => e.Id.Equals(id) ));
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
        Events.Add(newEventEntity);
        
        return Task.FromResult(newEventEntity);

    }

    public Task<Event?> UpdateEvent(Guid id, EventDto updatedEvent)
    {
        var index = Events.FindIndex(e => e.Id.Equals(id));
        
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
        Events[index] = updatedEntity;
        
        return Task.FromResult<Event?>(updatedEntity);
    }

    public Task<bool> DeleteEvent(Guid id)
    {
        var eventToDelete = Events.Find(e => e.Id.Equals(id));
        
        if (eventToDelete == null)
        {
            return Task.FromResult(false);
        }
        
        Events.Remove(eventToDelete);
        
        return Task.FromResult(true);
    }
}