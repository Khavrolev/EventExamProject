using System.ComponentModel.DataAnnotations;
using Events.Application.Abstractions;
using Events.Application.Common;
using Events.Application.DTOs;
using Events.Application.Services.Interfaces;
using Events.Domain.Entities;
using Events.Domain.Exceptions;
using Events.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Events.Application.Services;

public class EventService(
    IEventRepository eventRepository,
    ICacheService cacheService,
    IOptions<RedisOptions>? redisOptions = null) : IEventService
{
    private readonly RedisOptions _options = redisOptions?.Value ?? new RedisOptions();

    private static void ValidateDates(EventDto dto)
    {
        if (dto.EndAt <= dto.StartAt)
        {
            throw new ValidationException($"{nameof(dto.EndAt)} must be later than {nameof(dto.StartAt)}");
        }
    }

    public async Task<PaginatedResultDto<EventInfoDto>> GetAllEventsAsync(EventFilterDto filter, PaginationParamsDto paginationParams)
    {
        var result = await eventRepository.GetAllAsync(filter, paginationParams);

        return new PaginatedResultDto<EventInfoDto>
        {
            Data = result.Data.Select(EventInfoDto.FromEvent).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<IReadOnlyList<EventInfoDto>> GetTopPopularEventsAsync(int count = 10)
    {
        var cacheKey = EventCacheKeys.Top10;
        var cached = await cacheService.GetAsync<List<EventInfoDto>>(cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        var topEvents = await eventRepository.GetTopPopularAsync(count);
        var result = topEvents.Select(EventInfoDto.FromEvent).ToList();

        var ttl = TimeSpan.FromMinutes(_options.Top10TtlMinutes);
        await cacheService.SetAsync(cacheKey, result, ttl);

        return result;
    }

    public async Task<EventInfoDto> GetEventByIdAsync(Guid id)
    {
        var cacheKey = EventCacheKeys.Event(id);
        var cached = await cacheService.GetAsync<EventInfoDto>(cacheKey);
        if (cached is not null)
        {
            return cached;
        }

        var foundEvent = await eventRepository.GetByIdAsync(id);
        if (foundEvent is null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        var dto = EventInfoDto.FromEvent(foundEvent);
        var ttl = TimeSpan.FromMinutes(_options.EventTtlMinutes);
        await cacheService.SetAsync(cacheKey, dto, ttl);

        return dto;
    }

    public async Task<EventInfoDto> CreateEventAsync(EventDto newEvent)
    {
        ValidateDates(newEvent);

        var newEventEntity = Event.Create(new EventDetails(
            newEvent.Title,
            newEvent.Description,
            newEvent.StartAt,
            newEvent.EndAt,
            newEvent.TotalSeats!.Value));
        await eventRepository.AddAsync(newEventEntity);

        return EventInfoDto.FromEvent(newEventEntity);
    }

    public async Task<EventInfoDto> UpdateEventAsync(Guid id, EventDto updatedEvent)
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
        await cacheService.RemoveAsync(EventCacheKeys.Event(id));

        return EventInfoDto.FromEvent(existingEvent);
    }

    public async Task DeleteEventAsync(Guid id)
    {
        var eventToDelete = await eventRepository.GetByIdAsync(id);

        if (eventToDelete == null)
        {
            throw new NotFoundException($"Event with id {id} was not found");
        }

        await eventRepository.DeleteAsync(eventToDelete);
        await cacheService.RemoveAsync(EventCacheKeys.Event(id));
    }
}
