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
            EndAt = new DateTime(2026, 8, 1).AddHours(1)
        };

    private static (BookingService BookingService, EventService EventService, InMemoryBookingStore BookingStore) CreateServices()
    {
        var eventService = new EventService(new InMemoryEventStore());
        var bookingStore = new InMemoryBookingStore();
        var bookingService = new BookingService(bookingStore, eventService);

        return (bookingService, eventService, bookingStore);
    }

    [Fact]
    public async Task CreateBooking_ShouldCreatePendingBooking_WhenEventExists()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEvent(CreateValidEventDto());

        var booking = await bookingService.CreateBooking(createdEvent.Id);

        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(createdEvent.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateBooking_ShouldCreateBookingsWithUniqueIds_WhenCalledMultipleTimesForSameEvent()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEvent(CreateValidEventDto());

        var first = await bookingService.CreateBooking(createdEvent.Id);
        var second = await bookingService.CreateBooking(createdEvent.Id);
        var third = await bookingService.CreateBooking(createdEvent.Id);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetBookingById_ShouldReturnBooking_WhenBookingExists()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEvent(CreateValidEventDto());
        var created = await bookingService.CreateBooking(createdEvent.Id);

        var found = await bookingService.GetBookingById(created.Id);

        found.Id.Should().Be(created.Id);
        found.EventId.Should().Be(created.EventId);
        found.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task GetBookingById_ShouldReflectStatusChange_AfterBookingIsConfirmed()
    {
        var (bookingService, eventService, bookingStore) = CreateServices();
        var createdEvent = await eventService.CreateEvent(CreateValidEventDto());
        var created = await bookingService.CreateBooking(createdEvent.Id);

        created.Confirm();
        bookingStore.Update(created);

        var found = await bookingService.GetBookingById(created.Id);

        found.Status.Should().Be(BookingStatus.Confirmed);
        found.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        var (bookingService, _, _) = CreateServices();

        await FluentActions.Awaiting(() => bookingService.CreateBooking(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNotFoundException_WhenEventWasDeleted()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEvent(CreateValidEventDto());
        await eventService.DeleteEvent(createdEvent.Id);

        await FluentActions.Awaiting(() => bookingService.CreateBooking(createdEvent.Id))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetBookingById_ShouldThrowNotFoundException_WhenBookingDoesNotExist()
    {
        var (bookingService, _, _) = CreateServices();

        await FluentActions.Awaiting(() => bookingService.GetBookingById(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }
}
