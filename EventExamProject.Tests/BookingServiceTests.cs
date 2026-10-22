using EventExamProject.Infrastructure.Persistence;
using EventExamProject.Application.Abstractions;
using EventExamProject.Infrastructure.Repositories;
using EventExamProject.Application.DTOs.Event;
using EventExamProject.Domain.Exceptions;
using EventExamProject.Domain.Entities;
using EventExamProject.Application.Services;
using EventExamProject.Application.Services.Interfaces;
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
    private readonly Guid _userId = Guid.NewGuid();

    public BookingServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
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

    private static EventDto CreateValidEventDto(string title = "Event", int totalSeats = 10, int startsInDays = 30) =>
        new()
        {
            Title = title,
            Description = "Description",
            StartAt = DateTime.UtcNow.AddDays(startsInDays),
            EndAt = DateTime.UtcNow.AddDays(startsInDays).AddHours(1),
            TotalSeats = totalSeats,
        };

    private async Task<Guid> CreateEventAsync(int totalSeats = 10) =>
        (await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: totalSeats))).Id;

    [Fact]
    public async Task CreateBooking_ShouldCreatePendingBooking_WhenEventExists()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());

        var booking = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(createdEvent.Id);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();

        var stored = await _context.Bookings.FindAsync(booking.Id);
        stored!.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task CreateBooking_ShouldCreateBookingsWithUniqueIds_WhenCalledMultipleTimesForSameEvent()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());

        var first = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);
        var second = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);
        var third = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetBookingById_ShouldReturnBooking_WhenBookingExists()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());
        var created = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        var found = await _bookingService.GetBookingByIdAsync(created.Id);

        found.Id.Should().Be(created.Id);
        found.EventId.Should().Be(created.EventId);
        found.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task GetBookingById_ShouldReflectStatusChange_AfterBookingIsConfirmed()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());
        var created = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

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
        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(Guid.NewGuid(), _userId))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNotFoundException_WhenEventWasDeleted()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto());
        await _eventService.DeleteEventAsync(createdEvent.Id);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(createdEvent.Id, _userId))
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

        await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        var updatedEvent = await _eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(4);
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceedForAllBookings_UpToSeatLimit()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 3));

        var first = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);
        var second = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);
        var third = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        new[] { first.Id, second.Id, third.Id }.Should().OnlyHaveUniqueItems();

        var updatedEvent = await _eventService.GetEventByIdAsync(createdEvent.Id);
        updatedEvent.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNoAvailableSeatsException_AfterSeatsAreExhausted()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 2));

        await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);
        await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(createdEvent.Id, _userId))
            .Should().ThrowAsync<NoAvailableSeatsException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldThrowNoAvailableSeatsException_WhenNoSeatsAvailable()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 1));
        await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(createdEvent.Id, _userId))
            .Should().ThrowAsync<NoAvailableSeatsException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceed_AfterSeatIsReleasedByRejectedBooking()
    {
        var createdEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 1));
        var firstBooking = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        var trackedBooking = await _context.Bookings.FindAsync(firstBooking.Id);
        trackedBooking!.Reject();

        var trackedEvent = await _context.Events.FindAsync(createdEvent.Id);
        trackedEvent!.ReleaseSeats();

        await _context.SaveChangesAsync();

        var eventBeforeSecondBooking = await _eventService.GetEventByIdAsync(createdEvent.Id);
        eventBeforeSecondBooking.AvailableSeats.Should().Be(1);

        var secondBooking = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

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
                await bookingService.CreateBookingAsync(createdEvent.Id, _userId);
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

            return await bookingService.CreateBookingAsync(createdEvent.Id, Guid.NewGuid());
        })));

        bookings.Should().HaveCount(10);
        bookings.Select(b => b.Id).Should().OnlyHaveUniqueItems();
    }

    // ---- Event already started ----

    [Fact]
    public async Task CreateBooking_ShouldThrowEventAlreadyStartedException_WhenEventIsInThePast()
    {
        var pastEvent = await _eventService.CreateEventAsync(CreateValidEventDto(startsInDays: -1));

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(pastEvent.Id, _userId))
            .Should().ThrowAsync<EventAlreadyStartedException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldNotReserveSeat_WhenEventIsInThePast()
    {
        var pastEvent = await _eventService.CreateEventAsync(CreateValidEventDto(totalSeats: 5, startsInDays: -1));

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(pastEvent.Id, _userId))
            .Should().ThrowAsync<EventAlreadyStartedException>();

        var unchanged = await _eventService.GetEventByIdAsync(pastEvent.Id);
        unchanged.AvailableSeats.Should().Be(5);
        _context.Bookings.Should().BeEmpty();
    }

    // ---- Active bookings limit ----

    [Fact]
    public async Task CreateBooking_ShouldThrowBookingLimitExceededException_WhenUserReachedLimit()
    {
        var eventId = await CreateEventAsync(totalSeats: 50);
        await BookManyAsync(eventId, _userId, BookingService.MaxActiveBookingsPerUser);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(eventId, _userId))
            .Should().ThrowAsync<BookingLimitExceededException>()
            .WithMessage($"*{BookingService.MaxActiveBookingsPerUser}*");
    }

    [Fact]
    public async Task CreateBooking_ShouldNotCreateBookingOrReserveSeat_WhenLimitReached()
    {
        var eventId = await CreateEventAsync(totalSeats: 50);
        await BookManyAsync(eventId, _userId, BookingService.MaxActiveBookingsPerUser);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(eventId, _userId))
            .Should().ThrowAsync<BookingLimitExceededException>();

        _context.Bookings.Count().Should().Be(BookingService.MaxActiveBookingsPerUser);
        var updated = await _eventService.GetEventByIdAsync(eventId);
        updated.AvailableSeats.Should().Be(50 - BookingService.MaxActiveBookingsPerUser);
    }

    [Fact]
    public async Task CreateBooking_ShouldNotAffectOtherUsers_WhenOneUserReachedLimit()
    {
        var eventId = await CreateEventAsync(totalSeats: 50);
        var otherUserId = Guid.NewGuid();
        await BookManyAsync(eventId, _userId, BookingService.MaxActiveBookingsPerUser);

        var booking = await _bookingService.CreateBookingAsync(eventId, otherUserId);

        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceed_WhenSomeOfUserBookingsAreCancelled()
    {
        var eventId = await CreateEventAsync(totalSeats: 50);
        var bookings = await BookManyAsync(eventId, _userId, BookingService.MaxActiveBookingsPerUser);
        await _bookingService.CancelBookingAsync(bookings[0].Id, _userId, UserRole.User);

        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);

        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task CreateBooking_ShouldSucceed_WhenSomeOfUserBookingsAreRejected()
    {
        var eventId = await CreateEventAsync(totalSeats: 50);
        var bookings = await BookManyAsync(eventId, _userId, BookingService.MaxActiveBookingsPerUser);
        var tracked = await _context.Bookings.FindAsync(bookings[0].Id);
        tracked!.Reject();
        await _context.SaveChangesAsync();

        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);

        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task CreateBooking_ShouldCountBookingsAcrossDifferentEvents_TowardsLimit()
    {
        var firstEventId = await CreateEventAsync(totalSeats: 50);
        var secondEventId = await CreateEventAsync(totalSeats: 50);
        await BookManyAsync(firstEventId, _userId, BookingService.MaxActiveBookingsPerUser);

        await FluentActions.Awaiting(() => _bookingService.CreateBookingAsync(secondEventId, _userId))
            .Should().ThrowAsync<BookingLimitExceededException>();
    }

    [Fact]
    public async Task CreateBooking_ShouldNotExceedLimit_UnderConcurrentRequestsFromSameUser()
    {
        var eventId = await CreateEventAsync(totalSeats: 50);

        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            try
            {
                await bookingService.CreateBookingAsync(eventId, _userId);
                return true;
            }
            catch (BookingLimitExceededException)
            {
                return false;
            }
        })));

        results.Count(succeeded => succeeded).Should().Be(BookingService.MaxActiveBookingsPerUser);
    }

    // ---- Cancellation and permissions ----

    [Fact]
    public async Task CancelBooking_ShouldCancelBooking_WhenCalledByOwner()
    {
        var eventId = await CreateEventAsync();
        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);

        await _bookingService.CancelBookingAsync(booking.Id, _userId, UserRole.User);

        var found = await _bookingService.GetBookingByIdAsync(booking.Id);
        found.Status.Should().Be(BookingStatus.Cancelled);
        found.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CancelBooking_ShouldThrowForbiddenException_WhenUserCancelsSomeoneElsesBooking()
    {
        var eventId = await CreateEventAsync();
        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);

        await FluentActions.Awaiting(() =>
                _bookingService.CancelBookingAsync(booking.Id, Guid.NewGuid(), UserRole.User))
            .Should().ThrowAsync<ForbiddenException>();

        var found = await _bookingService.GetBookingByIdAsync(booking.Id);
        found.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task CancelBooking_ShouldCancelBooking_WhenAdminCancelsSomeoneElsesBooking()
    {
        var eventId = await CreateEventAsync();
        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);

        await _bookingService.CancelBookingAsync(booking.Id, Guid.NewGuid(), UserRole.Admin);

        var found = await _bookingService.GetBookingByIdAsync(booking.Id);
        found.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task CancelBooking_ShouldThrowNotFoundException_WhenBookingDoesNotExist()
    {
        await FluentActions.Awaiting(() =>
                _bookingService.CancelBookingAsync(Guid.NewGuid(), _userId, UserRole.User))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CancelBooking_ShouldThrowDomainValidationException_WhenBookingAlreadyCancelled()
    {
        var eventId = await CreateEventAsync();
        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);
        await _bookingService.CancelBookingAsync(booking.Id, _userId, UserRole.User);

        await FluentActions.Awaiting(() =>
                _bookingService.CancelBookingAsync(booking.Id, _userId, UserRole.User))
            .Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task CancelBooking_ShouldReleaseSeat_WhenBookingWasActive()
    {
        var eventId = await CreateEventAsync(totalSeats: 3);
        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);

        await _bookingService.CancelBookingAsync(booking.Id, _userId, UserRole.User);

        var updated = await _eventService.GetEventByIdAsync(eventId);
        updated.AvailableSeats.Should().Be(3);
    }

    [Fact]
    public async Task CancelBooking_ShouldNotReleaseSeat_WhenBookingWasAlreadyRejected()
    {
        var eventId = await CreateEventAsync(totalSeats: 3);
        var booking = await _bookingService.CreateBookingAsync(eventId, _userId);

        var tracked = await _context.Bookings.FindAsync(booking.Id);
        tracked!.Reject();
        await _context.SaveChangesAsync();

        await _bookingService.CancelBookingAsync(booking.Id, _userId, UserRole.User);

        var updated = await _eventService.GetEventByIdAsync(eventId);
        updated.AvailableSeats.Should().Be(2);
    }

    private async Task<List<Application.DTOs.Booking.BookingInfoDto>> BookManyAsync(Guid eventId, Guid userId, int count)
    {
        var bookings = new List<Application.DTOs.Booking.BookingInfoDto>();

        for (var i = 0; i < count; i++)
        {
            bookings.Add(await _bookingService.CreateBookingAsync(eventId, userId));
        }

        return bookings;
    }
}
