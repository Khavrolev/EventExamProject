using EventExamProject.DataAccess;
using EventExamProject.DTOs.Event;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Services;
using EventExamProject.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventExamProject.Tests;

public class BookingServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly IBookingService _bookingService;
    private readonly IEventService _eventService;
    private readonly AppDbContext _context;

    public BookingServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _bookingService = _scope.ServiceProvider.GetRequiredService<IBookingService>();
        _eventService = _scope.ServiceProvider.GetRequiredService<IEventService>();
        _context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }

    private static EventDto CreateValidEventDto(string title = "Event", int totalSeats = 10) =>
        new()
        {
            Title = title,
            Description = "Description",
            StartAt = new DateTime(2026, 8, 1),
            EndAt = new DateTime(2026, 8, 1).AddHours(1),
            TotalSeats = totalSeats,
        };

    [Fact]
    public async Task CreateBooking_ShouldCreatePendingBooking_WhenEventExists()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());

        var booking = await _bookingService.CreateBookingAsync(createdEvent.Id);

        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(createdEvent.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateBooking_ShouldCreateBookingsWithUniqueIds_WhenCalledMultipleTimesForSameEvent()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());

        var first = await _bookingService.CreateBookingAsync(createdEvent.Id);
        var second = await _bookingService.CreateBookingAsync(createdEvent.Id);
        var third = await _bookingService.CreateBookingAsync(createdEvent.Id);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetBookingById_ShouldReturnBooking_WhenBookingExists()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());
        var created = await _bookingService.CreateBookingAsync(createdEvent.Id);

        var found = await _bookingService.GetBookingByIdAsync(created.Id);

        found.Id.Should().Be(created.Id);
        found.EventId.Should().Be(created.EventId);
        found.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task GetBookingById_ShouldReflectStatusChange_AfterBookingIsConfirmed()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());
        var created = await _bookingService.CreateBookingAsync(createdEvent.Id);

        var trackedBooking = await _context.Bookings.FindAsync(created.Id);
        trackedBooking!.Confirm();
        await _context.SaveChangesAsync();

        var found = await _bookingService.GetBookingByIdAsync(created.Id);

        found.Status.Should().Be(BookingStatus.Confirmed);
        found.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNotFoundException_WhenEventDoesNotExist()
    {
        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNotFoundException_WhenEventWasDeleted()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());
        await _eventService.DeleteEventAsync(createdEvent.Id);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(createdEvent.Id))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetBookingById_ShouldThrowNotFoundException_WhenBookingDoesNotExist()
    {
        await FluentActions.Awaiting(() => _bookingService.GetBookingByIdAsync(Guid.NewGuid()))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldDecreaseAvailableSeatsByOne_WhenBookingCreated()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 5));

        await _bookingService.CreateBookingAsync(createdEvent.Id);

        var updatedEvent = await _eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(4);
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceedForAllBookings_UpToSeatLimit()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 3));

        var first = await _bookingService.CreateBookingAsync(createdEvent.Id);
        var second = await _bookingService.CreateBookingAsync(createdEvent.Id);
        var third = await _bookingService.CreateBookingAsync(createdEvent.Id);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();

        var updatedEvent = await _eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNoAvailableSeatsException_AfterSeatsAreExhausted()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 2));

        await _bookingService.CreateBookingAsync(createdEvent.Id);
        await _bookingService.CreateBookingAsync(createdEvent.Id);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(createdEvent.Id))
            .Should().ThrowAsync<NoAvailableSeatsException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNoAvailableSeatsException_WhenNoSeatsAvailable()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 1));
        await _bookingService.CreateBookingAsync(createdEvent.Id);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(createdEvent.Id))
            .Should().ThrowAsync<NoAvailableSeatsException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceed_AfterSeatIsReleasedByRejectedBooking()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 1));
        var firstBooking = await _bookingService.CreateBookingAsync(createdEvent.Id);

        var trackedBooking = await _context.Bookings.FindAsync(firstBooking.Id);
        trackedBooking!.Reject();

        var trackedEvent = await _context.Events.FindAsync(createdEvent.Id);
        trackedEvent!.ReleaseSeats();

        await _context.SaveChangesAsync();

        var eventBeforeSecondBooking = await _eventService.GetEventByIdAsync(createdEvent.Id);
        eventBeforeSecondBooking.AvailableSeats.Should().Be(1);

        var secondBooking = await _bookingService.CreateBookingAsync(createdEvent.Id);

        secondBooking.Id.Should().NotBe(firstBooking.Id);
        var eventAfterSecondBooking = await _eventService.GetEventByIdAsync(createdEvent.Id);
        eventAfterSecondBooking.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_ShouldAllowOnlyExactSeatCount_UnderConcurrentRequests()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 5));

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

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

        using var verificationScope = _serviceProvider.CreateScope();
        var eventService = verificationScope.ServiceProvider.GetRequiredService<IEventService>();
        var updatedEvent = await eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_ShouldProduceUniqueIds_UnderConcurrentRequests()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 10));

        var bookings = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            return await bookingService.CreateBookingAsync(createdEvent.Id);
        })));

        bookings.Should().HaveCount(10);
        bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
    }
}
