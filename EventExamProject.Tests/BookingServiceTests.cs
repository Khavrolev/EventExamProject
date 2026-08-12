using EventExamProject.DataAccess;
using EventExamProject.DTOs.Event;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Services;
using FluentAssertions;

namespace EventExamProject.Tests;

public class BookingServiceTests
{
    private static EventDto CreateValidEventDto(string title = "Event", int totalSeats = 10) =>
        new()
        {
            Title = title,
            Description = "Description",
            StartAt = new DateTime(2026, 8, 1),
            EndAt = new DateTime(2026, 8, 1).AddHours(1),
            TotalSeats = totalSeats,
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

    [Fact]
    public async Task CreateBooking_ShouldDecreaseAvailableSeatsByOne_WhenBookingCreated()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 5));

        await bookingService.CreateBookingAsync(createdEvent.Id);

        var updatedEvent = await eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(4);
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceedForAllBookings_UpToSeatLimit()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 3));

        var first = await bookingService.CreateBookingAsync(createdEvent.Id);
        var second = await bookingService.CreateBookingAsync(createdEvent.Id);
        var third = await bookingService.CreateBookingAsync(createdEvent.Id);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();

        var updatedEvent = await eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNoAvailableSeatsException_AfterSeatsAreExhausted()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 2));

        await bookingService.CreateBookingAsync(createdEvent.Id);
        await bookingService.CreateBookingAsync(createdEvent.Id);

        await FluentActions.Awaiting(() => bookingService.CreateBookingAsync(createdEvent.Id))
            .Should().ThrowAsync<NoAvailableSeatsException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNoAvailableSeatsException_WhenNoSeatsAvailable()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 1));
        await bookingService.CreateBookingAsync(createdEvent.Id);

        await FluentActions.Awaiting(() => bookingService.CreateBookingAsync(createdEvent.Id))
            .Should().ThrowAsync<NoAvailableSeatsException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceed_AfterSeatIsReleasedByRejectedBooking()
    {
        var (bookingService, eventService, bookingStore) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 1));
        var firstBooking = await bookingService.CreateBookingAsync(createdEvent.Id);

        firstBooking.Reject();
        bookingStore.Update(firstBooking);
        var eventAfterReject = await eventService.GetEventByIdAsync(createdEvent.Id);
        eventAfterReject.ReleaseSeats();

        var eventBeforeSecondBooking = await eventService.GetEventByIdAsync(createdEvent.Id);
        eventBeforeSecondBooking.AvailableSeats.Should().Be(1);

        var secondBooking = await bookingService.CreateBookingAsync(createdEvent.Id);

        secondBooking.Id.Should().NotBe(firstBooking.Id);
        var eventAfterSecondBooking = await eventService.GetEventByIdAsync(createdEvent.Id);
        eventAfterSecondBooking.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_ShouldAllowOnlyExactSeatCount_UnderConcurrentRequests()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 5));

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            try
            {
                await bookingService.CreateBookingAsync(createdEvent.Id);
                return true;
            }
            catch (NoAvailableSeatsException)
            {
                return false;
            }
        })));

        results.Count(succeeded => succeeded).Should().Be(5);
        results.Count(succeeded => !succeeded).Should().Be(15);

        var updatedEvent = await eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_ShouldProduceUniqueIds_UnderConcurrentRequests()
    {
        var (bookingService, eventService, _) = CreateServices();
        var createdEvent = await eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 10));

        var bookings = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => bookingService.CreateBookingAsync(createdEvent.Id))));

        bookings.Should().HaveCount(10);
        bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
    }
}
