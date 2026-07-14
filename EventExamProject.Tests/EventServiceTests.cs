using System.ComponentModel.DataAnnotations;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Exceptions;
using EventExamProject.Services;

namespace EventExamProject.Tests;

public class EventServiceTests
{
    private static EventDto CreateValidDto(string title = "Event", DateTime? startAt = null, DateTime? endAt = null) =>
        new()
        {
            Title = title,
            Description = "Description",
            StartAt = startAt ?? new DateTime(2026, 8, 1),
            EndAt = endAt ?? new DateTime(2026, 8, 1).AddHours(1)
        };

    [Fact]
    public async Task AddEvent_ShouldCreateEvent_WhenDataIsValid()
    {
        var service = new EventService();
        var dto = CreateValidDto("Conference");

        var created = await service.AddEvent(dto);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(dto.Title, created.Title);
        Assert.Equal(dto.StartAt, created.StartAt);
        Assert.Equal(dto.EndAt, created.EndAt);
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnAllEvents_WhenNoFiltersApplied()
    {
        var service = new EventService();
        await service.AddEvent(CreateValidDto("Event 1"));
        await service.AddEvent(CreateValidDto("Event 2"));

        var result = await service.GetAllEvents(new EventFilterDto(), new PaginationParams());

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task GetEventById_ShouldReturnEvent_WhenEventExists()
    {
        var service = new EventService();
        var created = await service.AddEvent(CreateValidDto("Conference"));

        var found = await service.GetEventById(created.Id);

        Assert.Equal(created.Id, found.Id);
        Assert.Equal(created.Title, found.Title);
    }

    [Fact]
    public async Task UpdateEvent_ShouldUpdateFields_WhenEventExists()
    {
        var service = new EventService();
        var created = await service.AddEvent(CreateValidDto("Old title"));
        var updateDto = CreateValidDto("New title");

        var updated = await service.UpdateEvent(created.Id, updateDto);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("New title", updated.Title);
    }

    [Fact]
    public async Task DeleteEvent_ShouldRemoveEvent_WhenEventExists()
    {
        var service = new EventService();
        var created = await service.AddEvent(CreateValidDto());

        var deleted = await service.DeleteEvent(created.Id);

        Assert.True(deleted);
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetEventById(created.Id));
    }

    [Fact]
    public async Task GetAllEvents_ShouldFilterByTitle_WhenTitleFilterApplied()
    {
        var service = new EventService();
        await service.AddEvent(CreateValidDto("Team Meeting"));
        await service.AddEvent(CreateValidDto("Conference"));
        await service.AddEvent(CreateValidDto("team standup"));

        var result = await service.GetAllEvents(new EventFilterDto { Title = "team" }, new PaginationParams());

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Data, e => Assert.Contains("team", e.Title, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAllEvents_ShouldFilterByDateRange_WhenFromAndToApplied()
    {
        var service = new EventService();
        var baseDate = new DateTime(2026, 8, 1);
        await service.AddEvent(CreateValidDto("Early", baseDate, baseDate.AddHours(1)));
        await service.AddEvent(CreateValidDto("Middle", baseDate.AddDays(5), baseDate.AddDays(5).AddHours(1)));
        await service.AddEvent(CreateValidDto("Late", baseDate.AddDays(10), baseDate.AddDays(10).AddHours(1)));

        var filter = new EventFilterDto { From = baseDate.AddDays(2), To = baseDate.AddDays(7) };
        var result = await service.GetAllEvents(filter, new PaginationParams());

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Middle", result.Data.Single().Title);
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnCorrectPage_WhenPaginationApplied()
    {
        var service = new EventService();
        for (var i = 1; i <= 5; i++)
        {
            await service.AddEvent(CreateValidDto($"Event {i}"));
        }

        var result = await service.GetAllEvents(new EventFilterDto(), new PaginationParams { Page = 2, PageSize = 2 });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(["Event 3", "Event 4"], result.Data.Select(e => e.Title));
    }

    [Fact]
    public async Task GetAllEvents_ShouldCombineFilters_WithLogicalAnd()
    {
        var service = new EventService();
        var baseDate = new DateTime(2026, 8, 1);
        await service.AddEvent(CreateValidDto("Team Meeting", baseDate, baseDate.AddHours(1)));
        await service.AddEvent(CreateValidDto("team standup", baseDate.AddDays(10), baseDate.AddDays(10).AddHours(1)));
        await service.AddEvent(CreateValidDto("Conference", baseDate, baseDate.AddHours(1)));

        var filter = new EventFilterDto { Title = "team", From = baseDate.AddDays(-1), To = baseDate.AddDays(2) };
        var result = await service.GetAllEvents(filter, new PaginationParams());

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Team Meeting", result.Data.Single().Title);
    }

    [Fact]
    public async Task GetEventById_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var service = new EventService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetEventById(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateEvent_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var service = new EventService();
        var dto = CreateValidDto();

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateEvent(Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task AddEvent_ShouldThrowValidationException_WhenEndAtIsBeforeStartAt()
    {
        var service = new EventService();
        var invalidDto = CreateValidDto(startAt: new DateTime(2026, 8, 2), endAt: new DateTime(2026, 8, 1));

        await Assert.ThrowsAsync<ValidationException>(() => service.AddEvent(invalidDto));
    }

    [Fact]
    public async Task UpdateEvent_ShouldThrowValidationException_WhenEndAtIsBeforeStartAt()
    {
        var service = new EventService();
        var created = await service.AddEvent(CreateValidDto());
        var invalidDto = CreateValidDto(startAt: new DateTime(2026, 8, 2), endAt: new DateTime(2026, 8, 1));

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateEvent(created.Id, invalidDto));
    }
}
