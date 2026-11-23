using Events.Application.Abstractions;
using Events.Application.Common;
using Events.Application.DTOs;
using Events.Application.Services;
using Events.Domain.Entities;
using Events.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventExamProject.UnitTests;

public class EventServiceCachingTests
{
    private readonly Mock<IEventRepository> _repositoryMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly IOptions<RedisOptions> _options;
    private readonly EventService _service;

    public EventServiceCachingTests()
    {
        _repositoryMock = new Mock<IEventRepository>();
        _cacheMock = new Mock<ICacheService>();
        _options = Options.Create(new RedisOptions
        {
            EventTtlMinutes = 10,
            Top10TtlMinutes = 5
        });

        _service = new EventService(_repositoryMock.Object, _cacheMock.Object, _options);
    }

    private static Event CreateTestEvent(Guid id, string title = "Test Event", int totalSeats = 100, int availableSeats = 50)
    {
        var @event = Event.Create(new EventDetails(
            title,
            "Description",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            totalSeats));
        @event.Id = id;
        @event.TryReserveSeats(totalSeats - availableSeats);
        return @event;
    }

    [Fact]
    public async Task GetEventById_WhenCacheHit_ReturnsFromCacheAndDoesNotCallRepository()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var cacheKey = EventCacheKeys.Event(eventId);
        var cachedDto = new EventInfoDto
        {
            Id = eventId,
            Title = "Cached Event",
            Description = "From Redis",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100,
            AvailableSeats = 80
        };

        _cacheMock
            .Setup(c => c.GetAsync<EventInfoDto>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        // Act
        var result = await _service.GetEventByIdAsync(eventId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cachedDto.Id, result.Id);
        Assert.Equal(cachedDto.Title, result.Title);

        _cacheMock.Verify(c => c.GetAsync<EventInfoDto>(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEventById_WhenCacheMiss_FetchesFromRepositoryAndSavesToCache()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var cacheKey = EventCacheKeys.Event(eventId);
        var dbEvent = CreateTestEvent(eventId, "DB Event", 100, 75);

        _cacheMock
            .Setup(c => c.GetAsync<EventInfoDto>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventInfoDto?)null);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dbEvent);

        // Act
        var result = await _service.GetEventByIdAsync(eventId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.Id);
        Assert.Equal(dbEvent.Title, result.Title);

        _repositoryMock.Verify(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetAsync(
            cacheKey,
            It.Is<EventInfoDto>(dto => dto.Id == eventId && dto.Title == dbEvent.Title),
            It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateEvent_WhenCalled_SavesToRepositoryUpdatesEventCacheAndInvalidatesTop()
    {
        // Arrange
        var newEventDto = new EventDto
        {
            Title = "New Brand Event",
            Description = "Brand New",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 200
        };

        // Act
        var result = await _service.CreateEventAsync(newEventDto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);

        _repositoryMock.Verify(r => r.AddAsync(It.Is<Event>(e => e.Title == newEventDto.Title)), Times.Once);
        _cacheMock.Verify(c => c.SetAsync(
            EventCacheKeys.Event(result.Id),
            It.Is<EventInfoDto>(dto => dto.Id == result.Id && dto.Title == newEventDto.Title),
            It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes == 10),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEvent_WhenCalled_UpdatesRepositoryUpdatesEventCacheAndInvalidatesTop()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var existingEvent = CreateTestEvent(eventId, "Old Title", 100, 90);
        var updateDto = new EventDto
        {
            Title = "Updated Title",
            Description = "Updated Description",
            StartAt = DateTime.UtcNow.AddDays(2),
            EndAt = DateTime.UtcNow.AddDays(3),
            TotalSeats = 150
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        // Act
        var result = await _service.UpdateEventAsync(eventId, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Title", result.Title);

        _repositoryMock.Verify(r => r.UpdateAsync(existingEvent), Times.Once);
        _cacheMock.Verify(c => c.SetAsync(
            EventCacheKeys.Event(eventId),
            It.Is<EventInfoDto>(dto => dto.Id == eventId && dto.Title == "Updated Title"),
            It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes == 10),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEvent_WhenCalled_DeletesFromRepositoryRemovesEventCacheAndInvalidatesTop()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var existingEvent = CreateTestEvent(eventId, "To Delete", 100, 100);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        // Act
        await _service.DeleteEventAsync(eventId);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(existingEvent), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync(EventCacheKeys.Event(eventId), It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEvent_WhenRepositoryFails_DoesNotUpdateCache()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var existingEvent = CreateTestEvent(eventId, "Original", 100, 100);
        var updateDto = new EventDto
        {
            Title = "New Title",
            Description = "Desc",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100
        };

        _repositoryMock
            .Setup(r => r.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Event>()))
            .ThrowsAsync(new InvalidOperationException("DB Failure"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateEventAsync(eventId, updateDto));

        _cacheMock.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<EventInfoDto>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _cacheMock.Verify(c => c.RemoveAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTopPopularEvents_WhenCacheHit_ReturnsFromCacheAndDoesNotCallRepository()
    {
        // Arrange
        var cachedTop = new List<EventInfoDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Top 1 Event",
                StartAt = DateTime.UtcNow.AddDays(1),
                EndAt = DateTime.UtcNow.AddDays(2),
                TotalSeats = 100,
                AvailableSeats = 10
            }
        };

        _cacheMock
            .Setup(c => c.GetAsync<List<EventInfoDto>>(EventCacheKeys.Top10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedTop);

        // Act
        var result = await _service.GetTopPopularEventsAsync(10);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(cachedTop[0].Id, result[0].Id);

        _cacheMock.Verify(c => c.GetAsync<List<EventInfoDto>>(EventCacheKeys.Top10, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.GetTopPopularAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTopPopularEvents_WithCustomCount_UsesCountInCacheKey()
    {
        // Arrange
        const int count = 5;
        var expectedKey = EventCacheKeys.Top(count);
        var cachedTop = new List<EventInfoDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Top 5 Event",
                StartAt = DateTime.UtcNow.AddDays(1),
                EndAt = DateTime.UtcNow.AddDays(2),
                TotalSeats = 100,
                AvailableSeats = 10
            }
        };

        _cacheMock
            .Setup(c => c.GetAsync<List<EventInfoDto>>(expectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedTop);

        // Act
        var result = await _service.GetTopPopularEventsAsync(count);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("events:top5", expectedKey);
        _cacheMock.Verify(c => c.GetAsync<List<EventInfoDto>>(expectedKey, It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.GetTopPopularAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTopPopularEvents_WhenCacheMiss_FetchesFromRepositoryAndSavesToCache()
    {
        // Arrange
        var topEvents = new List<Event>
        {
            CreateTestEvent(Guid.NewGuid(), "Most Popular", 100, 5),
            CreateTestEvent(Guid.NewGuid(), "Second Popular", 100, 20)
        };

        _cacheMock
            .Setup(c => c.GetAsync<List<EventInfoDto>>(EventCacheKeys.Top10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<EventInfoDto>?)null);

        _repositoryMock
            .Setup(r => r.GetTopPopularAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topEvents);

        // Act
        var result = await _service.GetTopPopularEventsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Most Popular", result[0].Title);

        _repositoryMock.Verify(r => r.GetTopPopularAsync(10, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.SetAsync(
            EventCacheKeys.Top10,
            It.Is<List<EventInfoDto>>(list => list.Count == 2),
            It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalMinutes == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
