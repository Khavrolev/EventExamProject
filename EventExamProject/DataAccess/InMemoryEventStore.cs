using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Exceptions;
using EventExamProject.Models;

namespace EventExamProject.DataAccess;

public class InMemoryEventStore : IEventStore
{
    private readonly List<Event> _events = [];

    public void Add(Event eventEntity)
    {
        _events.Add(eventEntity);
    }

    public Event? GetById(Guid id)
    {
        return _events.Find(e => e.Id.Equals(id));
    }

    public IEnumerable<Event> GetAll()
    {
        return _events.AsEnumerable();
    }

    public void Update(Event eventEntity)
    {
        var index = _events.FindIndex(e => e.Id.Equals(eventEntity.Id));

        if (index == -1)
        {
            throw new NotFoundException($"Event with id {eventEntity.Id} was not found");
        }

        _events[index] = eventEntity;
    }

    public void Delete(Event eventEntity)
    {
        _events.Remove(eventEntity);
    }
}
