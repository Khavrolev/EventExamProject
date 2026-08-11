using EventExamProject.DataAccess;
using EventExamProject.DTOs.Event;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Services;
using FluentAssertions;

namespace EventExamProject.Tests;

public class BookingServiceTests
{
    private static EventDto CreateValidEventDto(string title = "Event") =>
        new()
        {
            Title = title,
            Description = "Description",
            StartAt = new DateTime(2026, 8, 1),
            EndAt = new DateTime(2026, 8, 1).AddHours(1),
            TotalSeats = 10,
        };

    private static (BookingService BookingService, EventService EventService, InMemoryBookingStore BookingStore) CreateServices()
    {
        var eventStore = new InMemoryEventStore();
        var eventService = new EventService(eventStore);
        var bookingStore = new InMemoryBookingStore();
        var bookingService = new BookingService(bookingStore, eventStore, eventService);

        return (bookingService, eventService, bookingStore);
    }

    [Fact]
    public async Task CreateBooking_ShouldCreatePendingBooking_WhenEventExists()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto());

        var booking = await bookingService.CreateBookingAsync(createdEvent.Id);

        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(createdEvent.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateBooking_ShouldCreateBookingsWithUniqueIds_WhenCalledMultipleTimesForSameEvent()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto());

        var first = await bookingService.CreateBookingAsync(createdEvent.Id);
        var second = await bookingService.CreateBookingAsync(createdEvent.Id);
        var third = await bookingService.CreateBookingAsync(createdEvent.Id);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetBookingById_ShouldReturnBooking_WhenBookingExists()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto());
        var created = await bookingService.CreateBookingAsync(createdEvent.Id);

        var found = await bookingService.GetBookingByIdAsync(created.Id);

        found.Id.Should().Be(created.Id);
        found.EventId.Should().Be(created.EventId);
        found.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task GetBookingById_ShouldReflectStatusChange_AfterBookingIsConfirmed()
    {
        var (bookingService, eventService, bookingStore) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto());
        var created = await bookingService.CreateBookingAsync(createdEvent.Id);

        created.Confirm();
        bookingStore.Update(created);

        var found = await bookingService.GetBookingByIdAsync(created.Id);

        found.Status.Should().Be(BookingStatus.Confirmed);
        found.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var (bookingService, _, _) = CreateServices();

        await FluentActions.Awaiting(() => bookingService.CreateBookingAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNotFoundException_WhenEventWasDeleted()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto());
        await eventService.DeleteEventAsync(createdEvent.Id);

        await FluentActions.Awaiting(() => bookingService.CreateBookingAsync(createdEvent.Id))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetBookingById_ShouldThrowNotFoundException_WhenBookingDoesNotExist()
    {
        var (bookingService, _, _) = CreateServices();

        await FluentActions.Awaiting(() => bookingService.GetBookingByIdAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }
}
