using System.ComponentModel.DataAnnotations;
using EventExamProject.DataAccess;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Resources;
using EventExamProject.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.Services;

internal class EventService(AppDbContext context) : IEventService
{
    private static void ValidateDates(EventDto dto)
    {
        if (dto.EndAt <= dto.StartAt)
        {
            throw new ValidationException(string.Format(ValidationMessages.DateGreaterThan, nameof(dto.EndAt), nameof(dto.StartAt)));
        }
    }

    public async Task<PaginatedResultDto<Event>> GetAllEventsAsync(EventFilterDto filter, PaginationParamsDto paginationParams)
    {
        var filtered = context.Events.AsQueryable();

        if (!string.IsNullOrEmpty(filter.Title))
        {
            var title = filter.Title.ToLower();
            filtered = filtered.Where(e => e.Title.ToLower().Contains(title));
        }

        if (filter.From.HasValue)
        {
            filtered = filtered.Where(e => e.StartAt >= filter.From);
        }

        if (filter.To.HasValue)
        {
            filtered = filtered.Where(e => e.EndAt <= filter.To);
        }

        var totalCount = await filtered.CountAsync();

        var paginated = await filtered
            .Skip((paginationParams.Page - 1) * paginationParams.PageSize)
            .Take(paginationParams.PageSize)
            .ToListAsync();

        return new PaginatedResultDto<Event>
        {
            Data = paginated, TotalCount = totalCount, Page = paginationParams.Page, PageSize = paginationParams.PageSize
        };
    }

    public async Task<Event> GetEventByIdAsync(Guid id)
    {
        var foundEvent = await context.Events.FindAsync(id);

        return foundEvent ?? throw new NotFoundException($"Event with id {id} was not found");
    }

    public async Task<Event> CreateEventAsync(EventDto newEvent)
    {
        ValidateDates(newEvent);

        var newEventEntity = Event.Create(newEvent);
        context.Events.Add(newEventEntity);

        await context.SaveChangesAsync();

        return newEventEntity;
    }

    public async Task<Event> UpdateEventAsync(Guid id, EventDto updatedEvent)
    {
        var existingEvent = await context.Events.FindAsync(id);

        if (existingEvent == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        ValidateDates(updatedEvent);

        existingEvent.Update(updatedEvent);

        await context.SaveChangesAsync();

        return existingEvent;
    }

    public async Task DeleteEventAsync(Guid id)
    {
        var eventToDelete = await context.Events.FindAsync(id);

        if (eventToDelete == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        context.Events.Remove(eventToDelete);

        await context.SaveChangesAsync();
    }
}
