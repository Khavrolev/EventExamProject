using EventExamProject.Models;

namespace EventExamProject.DataAccess.Interfaces;

public interface IEventStore
{
    void Add(Event eventEntity);
    Event? GetById(Guid id);
    IEnumerable<Event> GetAll();
    void Update(Event eventEntity);
    void Delete(Event eventEntity);
}
