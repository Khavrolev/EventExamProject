using System.ComponentModel.DataAnnotations;
using EventExamProject.DataAccess.Interfaces;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Resources;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

internal class EventService(IEventRepository eventRepository) : IEventService
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
        return await eventRepository.GetAllAsync(filter, paginationParams);
    }

    public async Task<Event> GetEventByIdAsync(Guid id)
    {
        var foundEvent = await eventRepository.GetByIdAsync(id);

        return foundEvent ?? throw new NotFoundException($"Event with id {id} was not found");
    }

    public async Task<Event> CreateEventAsync(EventDto newEvent)
    {
        ValidateDates(newEvent);

        var newEventEntity = Event.Create(newEvent);
        await eventRepository.AddAsync(newEventEntity);

        return newEventEntity;
    }

    public async Task<Event> UpdateEventAsync(Guid id, EventDto updatedEvent)
    {
        var existingEvent = await eventRepository.GetByIdAsync(id);

        if (existingEvent == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        ValidateDates(updatedEvent);

        existingEvent.Update(updatedEvent);

        await eventRepository.UpdateAsync(existingEvent);

        return existingEvent;
    }

    public async Task DeleteEventAsync(Guid id)
    {
        var eventToDelete = await eventRepository.GetByIdAsync(id);

        if (eventToDelete == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        await eventRepository.DeleteAsync(eventToDelete);
    }
}
