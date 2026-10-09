using System.ComponentModel.DataAnnotations;
using EventExamProject.Application.Abstractions;
using EventExamProject.Application.DTOs.Event;
using EventExamProject.Application.DTOs.Pagination;
using EventExamProject.Domain.Exceptions;
using EventExamProject.Domain.Entities;
using EventExamProject.Domain.ValueObjects;
using EventExamProject.Application.Resources;
using EventExamProject.Application.Services.Interfaces;

namespace EventExamProject.Application.Services;

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

        var newEventEntity = Event.Create(new EventDetails(
            newEvent.Title,
            newEvent.Description,
            newEvent.StartAt,
            newEvent.EndAt,
            newEvent.TotalSeats!.Value));
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

        existingEvent.Update(new EventDetails(
            updatedEvent.Title,
            updatedEvent.Description,
            updatedEvent.StartAt,
            updatedEvent.EndAt,
            updatedEvent.TotalSeats!.Value));

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
