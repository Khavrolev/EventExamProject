using System.ComponentModel.DataAnnotations;
using EventExamProject.DTOs;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Resources;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

public class EventService :IEventService
{
    private readonly List<Event> _events = [];

    private static void ValidateDates(EventDto dto)
    {
        if (dto.EndAt <= dto.StartAt)
        {
            throw new ValidationException(ValidationMessages.EndAtAfterStartAt);
        }
    }
    
    public Task<PaginatedResult<Event>> GetAllEvents(EventFilterDto filter, PaginationParams paginationParams)
    {
        var filtered = _events.AsEnumerable();
        
        if (!string.IsNullOrEmpty(filter.Title))
        {
            filtered = filtered.Where(e => e.Title.Contains(filter.Title, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.From.HasValue)
        {
            filtered = filtered.Where(e=>e.StartAt >= filter.From);
        }
        
        if (filter.To.HasValue)
        {
            filtered = filtered.Where(e=>e.EndAt <= filter.To);
        }
        
        var filteredList = filtered.ToList();

        var paginated = filteredList
            .Skip((paginationParams.Page - 1) * paginationParams.PageSize)
            .Take(paginationParams.PageSize)
            .ToList();

        return Task.FromResult(new PaginatedResult<Event> {
            Data = paginated, TotalCount = filteredList.Count(), Page = paginationParams.Page, PageSize = paginationParams.PageSize
        });
    }

    public Task<Event> GetEventById(Guid id)
    {
        var foundEvent = _events.Find(e => e.Id.Equals(id));

        return foundEvent == null ? throw new NotFoundException($"Event with id {id} was not found") : Task.FromResult(foundEvent);

    }
    
    public Task<Event> AddEvent(EventDto newEvent)
    {
        ValidateDates(newEvent);

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

    public Task<Event> UpdateEvent(Guid id, EventDto updatedEvent)
    {
        var index = _events.FindIndex(e => e.Id.Equals(id));
        
        if (index == -1)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        ValidateDates(updatedEvent);

        var updatedEntity = new Event
        {
            Id = id,
            Title = updatedEvent.Title,
            Description = updatedEvent.Description,
            StartAt = updatedEvent.StartAt,
            EndAt = updatedEvent.EndAt
        };
        _events[index] = updatedEntity;
        
        return Task.FromResult(updatedEntity);
    }

    public Task DeleteEvent(Guid id)
    {
        var eventToDelete = _events.Find(e => e.Id.Equals(id));
        
        if (eventToDelete == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
            
        }
        
        _events.Remove(eventToDelete);
        
        return Task.CompletedTask;
    }
}