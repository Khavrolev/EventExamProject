using System.ComponentModel.DataAnnotations;
using EventExamProject.DataAccess.Interfaces;
using EventExamProject.DTOs;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Resources;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

public class EventService(IEventStore eventStore) : IEventService
{
    private static void ValidateDates(EventDto dto)
    {
        if (dto.EndAt <= dto.StartAt)
        {
            throw new ValidationException(string.Format(ValidationMessages.DateGreaterThan, nameof(dto.EndAt), nameof(dto.StartAt)));
        }
    }

    public Task<PaginatedResult<Event>> GetAllEvents(EventFilterDto filter, PaginationParams paginationParams)
    {
        var filtered = eventStore.GetAll();

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
        var foundEvent = eventStore.GetById(id);

        return foundEvent == null ? throw new NotFoundException($"Event with id {id} was not found") : Task.FromResult(foundEvent);

    }

    public Task<Event> AddEvent(EventDto newEvent)
    {
        ValidateDates(newEvent);

        var newEventEntity = Event.Create(newEvent);
        eventStore.Add(newEventEntity);

        return Task.FromResult(newEventEntity);

    }

    public Task<Event> UpdateEvent(Guid id, EventDto updatedEvent)
    {
        var existingEvent = eventStore.GetById(id);

        if (existingEvent == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        ValidateDates(updatedEvent);

        existingEvent.Update(updatedEvent);
        eventStore.Update(existingEvent);

        return Task.FromResult(existingEvent);
    }

    public Task DeleteEvent(Guid id)
    {
        var eventToDelete = eventStore.GetById(id);

        if (eventToDelete == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");

        }

        eventStore.Delete(eventToDelete);

        return Task.CompletedTask;
    }
}
