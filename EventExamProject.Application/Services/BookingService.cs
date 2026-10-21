using System.Collections.Concurrent;
using EventExamProject.Application.Abstractions;
using EventExamProject.Application.DTOs.Booking;
using EventExamProject.Domain.Exceptions;
using EventExamProject.Domain.Entities;
using EventExamProject.Application.Services.Interfaces;

namespace EventExamProject.Application.Services;

internal class BookingService(IEventRepository eventRepository, IBookingRepository bookingRepository) : IBookingService
{
    public const int MaxActiveBookingsPerUser = 10;

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> EventLocks = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> UserLocks = new();

    private static SemaphoreSlim GetLockFor(Guid eventId) =>
        EventLocks.GetOrAdd(eventId, static _ => new SemaphoreSlim(1, 1));

    private static SemaphoreSlim GetUserLockFor(Guid userId) =>
        UserLocks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));

    public async Task<BookingInfoDto> CreateBookingAsync(Guid eventId, Guid userId)
    {
        // The user lock makes "count active bookings + add a new one" atomic per user,
        // so parallel requests cannot slip past the limit. Always taken before the event lock.
        var userLock = GetUserLockFor(userId);
        await userLock.WaitAsync();

        try
        {
            var eventLock = GetLockFor(eventId);
            await eventLock.WaitAsync();

            try
            {
                var foundEvent = await eventRepository.GetByIdAsync(eventId)
                    ?? throw new NotFoundException($"Event with id {eventId} was not found");

                if (foundEvent.StartAt <= DateTime.UtcNow)
                {
                    throw new EventAlreadyStartedException("Cannot book an event that has already started");
                }

                var activeBookings = await bookingRepository.CountActiveByUserAsync(userId);

                if (activeBookings >= MaxActiveBookingsPerUser)
                {
                    throw new BookingLimitExceededException(
                        $"Active bookings limit ({MaxActiveBookingsPerUser}) exceeded");
                }

                if (!foundEvent.TryReserveSeats())
                {
                    throw new NoAvailableSeatsException("No available seats for this event");
                }

                await eventRepository.UpdateAsync(foundEvent);

                var newBookingEntity = Booking.CreatePending(eventId, userId);
                await bookingRepository.AddAsync(newBookingEntity);

                return BookingInfoDto.FromBooking(newBookingEntity);
            }
            finally
            {
                eventLock.Release();
            }
        }
        finally
        {
            userLock.Release();
        }
    }

    public async Task<BookingInfoDto> GetBookingByIdAsync(Guid id)
    {
        var foundBooking = await bookingRepository.GetByIdAsync(id);

        return foundBooking is null
            ? throw new NotFoundException($"Booking with id {id} was not found")
            : BookingInfoDto.FromBooking(foundBooking);
    }

    public async Task CancelBookingAsync(Guid bookingId, Guid userId, UserRole userRole)
    {
        var foundBooking = await bookingRepository.GetByIdAsync(bookingId)
            ?? throw new NotFoundException($"Booking with id {bookingId} was not found");

        if (userRole != UserRole.Admin && foundBooking.UserId != userId)
        {
            throw new ForbiddenException("You can cancel only your own bookings");
        }

        var eventLock = GetLockFor(foundBooking.EventId);
        await eventLock.WaitAsync();

        try
        {
            // Rejected bookings have already given their seat back (or never held one).
            var holdsSeat = foundBooking.Status is BookingStatus.Pending or BookingStatus.Confirmed;

            foundBooking.Cancel();
            await bookingRepository.UpdateAsync(foundBooking);

            if (holdsSeat)
            {
                var foundEvent = await eventRepository.GetByIdAsync(foundBooking.EventId);

                if (foundEvent is not null)
                {
                    foundEvent.ReleaseSeats();
                    await eventRepository.UpdateAsync(foundEvent);
                }
            }
        }
        finally
        {
            eventLock.Release();
        }
    }
}
