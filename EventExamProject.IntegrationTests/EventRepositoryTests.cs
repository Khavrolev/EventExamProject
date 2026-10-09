using EventExamProject.DataAccess.Repositories;
using EventExamProject.Application.DTOs.Event;
using EventExamProject.Application.DTOs.Pagination;
using EventExamProject.IntegrationTests.Infrastructure;
using EventExamProject.Domain.Entities;
using EventExamProject.Domain.ValueObjects;
using FluentAssertions;

namespace EventExamProject.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class EventRepositoryTests(DatabaseFixture fixture) : RepositoryTestBase(fixture)
{
    private static Event CreateEvent(string title = "Event", DateTime? startAt = null, DateTime? endAt = null, int totalSeats = 10)
    {
        var start = startAt ?? new DateTime(2026, 8, 1);
        var end = endAt ?? start.AddHours(1);

        return Event.Create(new EventDetails(title, "Description", start, end, totalSeats));
    }

    [Fact]
    public async Task AddAsync_ShouldPersistEvent_ToDatabase()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var newEvent = CreateEvent("Conference");

        // Act
        await repository.AddAsync(newEvent);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Events.FindAsync(newEvent.Id);

        stored.Should().NotBeNull();
        stored!.Title.Should().Be("Conference");
        stored.TotalSeats.Should().Be(10);
        stored.AvailableSeats.Should().Be(10);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEvent_WhenEventExists()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var newEvent = CreateEvent();
        await repository.AddAsync(newEvent);

        // Act
        var found = await repository.GetByIdAsync(newEvent.Id);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(newEvent.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenEventDoesNotExist()
    {
        // Arrange
        var repository = new EventRepository(Context);

        // Act
        var found = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges_ToDatabase()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var existingEvent = CreateEvent("Old title");
        await repository.AddAsync(existingEvent);

        // Act
        existingEvent.Update(new EventDetails(
            "New title",
            "Updated description",
            existingEvent.StartAt,
            existingEvent.EndAt,
            existingEvent.TotalSeats));
        await repository.UpdateAsync(existingEvent);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Events.FindAsync(existingEvent.Id);

        stored!.Title.Should().Be("New title");
        stored.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEvent_FromDatabase()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var eventToDelete = CreateEvent();
        await repository.AddAsync(eventToDelete);

        // Act
        await repository.DeleteAsync(eventToDelete);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Events.FindAsync(eventToDelete.Id);

        stored.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldCascadeDeleteItsBookings()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var eventWithBooking = CreateEvent();
        await repository.AddAsync(eventWithBooking);

        var booking = Booking.CreatePending(eventWithBooking.Id);
        Context.Bookings.Add(booking);
        await Context.SaveChangesAsync();

        // Act
        await repository.DeleteAsync(eventWithBooking);

        // Assert
        await using var verificationContext = CreateContext();
        var storedBooking = await verificationContext.Bookings.FindAsync(booking.Id);

        storedBooking.Should().BeNull("the foreign key is configured with ON DELETE CASCADE");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEvents_WhenNoFiltersApplied()
    {
        // Arrange
        var repository = new EventRepository(Context);
        await repository.AddAsync(CreateEvent("Event 1"));
        await repository.AddAsync(CreateEvent("Event 2"));

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto(), new PaginationParamsDto());

        // Assert
        result.TotalCount.Should().Be(2);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByTitle_CaseInsensitively()
    {
        // Arrange
        var repository = new EventRepository(Context);
        await repository.AddAsync(CreateEvent("Team Meeting"));
        await repository.AddAsync(CreateEvent("Conference"));
        await repository.AddAsync(CreateEvent("team standup"));

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto { Title = "team" }, new PaginationParamsDto());

        // Assert
        result.TotalCount.Should().Be(2);
        result.Data.Should().OnlyContain(e => e.Title.Contains("team", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEvents_WhenTitleFilterIsEmptyString()
    {
        // Arrange
        var repository = new EventRepository(Context);
        await repository.AddAsync(CreateEvent("Event 1"));
        await repository.AddAsync(CreateEvent("Event 2"));

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto { Title = "" }, new PaginationParamsDto());

        // Assert
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByDateRange_WhenFromAndToApplied()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var baseDate = new DateTime(2026, 8, 1);
        await repository.AddAsync(CreateEvent("Early", baseDate, baseDate.AddHours(1)));
        await repository.AddAsync(CreateEvent("Middle", baseDate.AddDays(5), baseDate.AddDays(5).AddHours(1)));
        await repository.AddAsync(CreateEvent("Late", baseDate.AddDays(10), baseDate.AddDays(10).AddHours(1)));

        // Act
        var filter = new EventFilterDto { From = baseDate.AddDays(2), To = baseDate.AddDays(7) };
        var result = await repository.GetAllAsync(filter, new PaginationParamsDto());

        // Assert
        result.Data.Should().ContainSingle().Which.Title.Should().Be("Middle");
    }

    [Fact]
    public async Task GetAllAsync_ShouldIncludeEvent_WhenStartAtEqualsFromBoundary()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var boundary = new DateTime(2026, 8, 1);
        await repository.AddAsync(CreateEvent("Boundary", boundary, boundary.AddHours(1)));

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto { From = boundary }, new PaginationParamsDto());

        // Assert
        result.Data.Should().ContainSingle().Which.Title.Should().Be("Boundary");
    }

    [Fact]
    public async Task GetAllAsync_ShouldIncludeEvent_WhenEndAtEqualsToBoundary()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var boundary = new DateTime(2026, 8, 1);
        await repository.AddAsync(CreateEvent("Boundary", boundary.AddHours(-1), boundary));

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto { To = boundary }, new PaginationParamsDto());

        // Assert
        result.Data.Should().ContainSingle().Which.Title.Should().Be("Boundary");
    }

    [Fact]
    public async Task GetAllAsync_ShouldCombineFilters_WithLogicalAnd()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var baseDate = new DateTime(2026, 8, 1);
        await repository.AddAsync(CreateEvent("Team Meeting", baseDate, baseDate.AddHours(1)));
        await repository.AddAsync(CreateEvent("team standup", baseDate.AddDays(10), baseDate.AddDays(10).AddHours(1)));
        await repository.AddAsync(CreateEvent("Conference", baseDate, baseDate.AddHours(1)));

        // Act
        var filter = new EventFilterDto { Title = "team", From = baseDate.AddDays(-1), To = baseDate.AddDays(2) };
        var result = await repository.GetAllAsync(filter, new PaginationParamsDto());

        // Assert
        result.Data.Should().ContainSingle().Which.Title.Should().Be("Team Meeting");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnCorrectPage_WhenPaginationApplied()
    {
        // Arrange
        var repository = new EventRepository(Context);
        var baseDate = new DateTime(2026, 8, 1);
        for (var i = 1; i <= 5; i++)
        {
            await repository.AddAsync(CreateEvent($"Event {i}", baseDate.AddDays(i)));
        }

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto(), new PaginationParamsDto { Page = 2, PageSize = 2 });

        // Assert
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.Data.Select(e => e.Title).Should().Equal("Event 3", "Event 4");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyData_WhenPageExceedsAvailableData()
    {
        // Arrange
        var repository = new EventRepository(Context);
        await repository.AddAsync(CreateEvent("Event 1"));

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto(), new PaginationParamsDto { Page = 999, PageSize = 10 });

        // Assert
        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyData_WhenPageSizeIsZero()
    {
        // Arrange
        var repository = new EventRepository(Context);
        await repository.AddAsync(CreateEvent("Event 1"));

        // Act
        var result = await repository.GetAllAsync(new EventFilterDto(), new PaginationParamsDto { Page = 1, PageSize = 0 });

        // Assert
        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
    }
}
