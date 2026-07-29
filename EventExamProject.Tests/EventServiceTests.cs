using System.ComponentModel.DataAnnotations;
using EventExamProject.DataAccess;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Exceptions;
using EventExamProject.Services;
using FluentAssertions;

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
    public async Task CreateEvent_ShouldCreateEvent_WhenDataIsValid()
    {
        var service = new EventService(new InMemoryEventStore());
        var dto = CreateValidDto("Conference");

        var created = await service.CreateEventAsync(dto);

        created.Id.Should().NotBe(Guid.Empty);
        created.Title.Should().Be(dto.Title);
        created.StartAt.Should().Be(dto.StartAt);
        created.EndAt.Should().Be(dto.EndAt);
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnAllEvents_WhenNoFiltersApplied()
    {
        var service = new EventService(new InMemoryEventStore());
        await service.CreateEventAsync(CreateValidDto("Event 1"));
        await service.CreateEventAsync(CreateValidDto("Event 2"));

        var result = await service.GetAllEventsAsync(new EventFilterDto(), new PaginationParamsDto());

        result.TotalCount.Should().Be(2);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEventById_ShouldReturnEvent_WhenEventExists()
    {
        var service = new EventService(new InMemoryEventStore());
        var created = await service.CreateEventAsync(CreateValidDto("Conference"));

        var found = await service.GetEventByIdAsync(created.Id);

        found.Id.Should().Be(created.Id);
        found.Title.Should().Be(created.Title);
    }

    [Fact]
    public async Task UpdateEvent_ShouldUpdateFields_WhenEventExists()
    {
        var service = new EventService(new InMemoryEventStore());
        var created = await service.CreateEventAsync(CreateValidDto("Old title"));
        var updateDto = CreateValidDto("New title");

        var updated = await service.UpdateEventAsync(created.Id, updateDto);

        updated.Id.Should().Be(created.Id);
        updated.Title.Should().Be("New title");
    }

    [Fact]
    public async Task DeleteEvent_ShouldRemoveEvent_WhenEventExists()
    {
        var service = new EventService(new InMemoryEventStore());
        var created = await service.CreateEventAsync(CreateValidDto());

        await service.DeleteEventAsync(created.Id);

        await FluentActions.Awaiting(() => service.GetEventByIdAsync(created.Id))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetAllEvents_ShouldFilterByTitle_WhenTitleFilterApplied()
    {
        var service = new EventService(new InMemoryEventStore());
        await service.CreateEventAsync(CreateValidDto("Team Meeting"));
        await service.CreateEventAsync(CreateValidDto("Conference"));
        await service.CreateEventAsync(CreateValidDto("team standup"));

        var result = await service.GetAllEventsAsync(new EventFilterDto { Title = "team" }, new PaginationParamsDto());

        result.TotalCount.Should().Be(2);
        result.Data.Should().OnlyContain(e => e.Title.Contains("team", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAllEvents_ShouldFilterByDateRange_WhenFromAndToApplied()
    {
        var service = new EventService(new InMemoryEventStore());
        var baseDate = new DateTime(2026, 8, 1);
        await service.CreateEventAsync(CreateValidDto("Early", baseDate, baseDate.AddHours(1)));
        await service.CreateEventAsync(CreateValidDto("Middle", baseDate.AddDays(5), baseDate.AddDays(5).AddHours(1)));
        await service.CreateEventAsync(CreateValidDto("Late", baseDate.AddDays(10), baseDate.AddDays(10).AddHours(1)));

        var filter = new EventFilterDto { From = baseDate.AddDays(2), To = baseDate.AddDays(7) };
        var result = await service.GetAllEventsAsync(filter, new PaginationParamsDto());

        result.Data.Should().ContainSingle().Which.Title.Should().Be("Middle");
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnCorrectPage_WhenPaginationApplied()
    {
        var service = new EventService(new InMemoryEventStore());
        for (var i = 1; i <= 5; i++)
        {
            await service.CreateEventAsync(CreateValidDto($"Event {i}"));
        }

        var result = await service.GetAllEventsAsync(new EventFilterDto(), new PaginationParamsDto { Page = 2, PageSize = 2 });

        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Data.Select(e => e.Title).Should().Equal("Event 3", "Event 4");
    }

    [Fact]
    public async Task GetAllEvents_ShouldCombineFilters_WithLogicalAnd()
    {
        var service = new EventService(new InMemoryEventStore());
        var baseDate = new DateTime(2026, 8, 1);
        await service.CreateEventAsync(CreateValidDto("Team Meeting", baseDate, baseDate.AddHours(1)));
        await service.CreateEventAsync(CreateValidDto("team standup", baseDate.AddDays(10), baseDate.AddDays(10).AddHours(1)));
        await service.CreateEventAsync(CreateValidDto("Conference", baseDate, baseDate.AddHours(1)));

        var filter = new EventFilterDto { Title = "team", From = baseDate.AddDays(-1), To = baseDate.AddDays(2) };
        var result = await service.GetAllEventsAsync(filter, new PaginationParamsDto());

        result.Data.Should().ContainSingle().Which.Title.Should().Be("Team Meeting");
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnAllEvents_WhenTitleFilterIsEmptyString()
    {
        var service = new EventService(new InMemoryEventStore());
        await service.CreateEventAsync(CreateValidDto("Event 1"));
        await service.CreateEventAsync(CreateValidDto("Event 2"));

        var result = await service.GetAllEventsAsync(new EventFilterDto { Title = "" }, new PaginationParamsDto());

        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAllEvents_ShouldIncludeEvent_WhenStartAtEqualsFromBoundary()
    {
        var service = new EventService(new InMemoryEventStore());
        var boundary = new DateTime(2026, 8, 1);
        await service.CreateEventAsync(CreateValidDto("Boundary", boundary, boundary.AddHours(1)));

        var result = await service.GetAllEventsAsync(new EventFilterDto { From = boundary }, new PaginationParamsDto());

        result.Data.Should().ContainSingle().Which.Title.Should().Be("Boundary");
    }

    [Fact]
    public async Task GetAllEvents_ShouldIncludeEvent_WhenEndAtEqualsToBoundary()
    {
        var service = new EventService(new InMemoryEventStore());
        var boundary = new DateTime(2026, 8, 1);
        await service.CreateEventAsync(CreateValidDto("Boundary", boundary.AddHours(-1), boundary));

        var result = await service.GetAllEventsAsync(new EventFilterDto { To = boundary }, new PaginationParamsDto());

        result.Data.Should().ContainSingle().Which.Title.Should().Be("Boundary");
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnEmptyData_WhenPageExceedsAvailableData()
    {
        var service = new EventService(new InMemoryEventStore());
        await service.CreateEventAsync(CreateValidDto("Event 1"));

        var result = await service.GetAllEventsAsync(new EventFilterDto(), new PaginationParamsDto { Page = 999, PageSize = 10 });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllEvents_ShouldReturnEmptyData_WhenPageSizeIsZero()
    {
        var service = new EventService(new InMemoryEventStore());
        await service.CreateEventAsync(CreateValidDto("Event 1"));

        var result = await service.GetAllEventsAsync(new EventFilterDto(), new PaginationParamsDto { Page = 1, PageSize = 0 });

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetEventById_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var service = new EventService(new InMemoryEventStore());

        await FluentActions.Awaiting(() => service.GetEventByIdAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteEvent_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var service = new EventService(new InMemoryEventStore());

        await FluentActions.Awaiting(() => service.DeleteEventAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateEvent_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var service = new EventService(new InMemoryEventStore());
        var dto = CreateValidDto();

        await FluentActions.Awaiting(() => service.UpdateEventAsync(Guid.NewGuid(), dto))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateEvent_ShouldThrowValidationException_WhenEndAtIsBeforeStartAt()
    {
        var service = new EventService(new InMemoryEventStore());
        var invalidDto = CreateValidDto(startAt: new DateTime(2026, 8, 2), endAt: new DateTime(2026, 8, 1));

        await FluentActions.Awaiting(() => service.CreateEventAsync(invalidDto))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateEvent_ShouldThrowValidationException_WhenEndAtIsBeforeStartAt()
    {
        var service = new EventService(new InMemoryEventStore());
        var created = await service.CreateEventAsync(CreateValidDto());
        var invalidDto = CreateValidDto(startAt: new DateTime(2026, 8, 2), endAt: new DateTime(2026, 8, 1));

        await FluentActions.Awaiting(() => service.UpdateEventAsync(created.Id, invalidDto))
            .Should().ThrowAsync<ValidationException>();
    }
}
