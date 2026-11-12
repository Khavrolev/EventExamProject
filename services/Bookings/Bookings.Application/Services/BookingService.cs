using System.Collections.Concurrent;
using Bookings.Application.Abstractions;
using Bookings.Application.DTOs;
using Bookings.Application.Services.Interfaces;
using Bookings.Domain.Entities;
using Bookings.Domain.Exceptions;

namespace Bookings.Application.Services;

public class BookingService(IBookingRepository bookingRepository) : IBookingService
{
    public const int MaxActiveBookingsPerUser = 10;

    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> UserLocks = new();

    private static SemaphoreSlim GetUserLockFor(Guid userId) =>
        UserLocks.GetOrAdd(userId, static _ => new SemaphoreSlim(1, 1));

    public async Task<BookingInfoDto> CreateBookingAsync(Guid eventId, Guid userId)
    {
        var userLock = GetUserLockFor(userId);
        await userLock.WaitAsync();

        try
        {
            var activeBookings = await bookingRepository.CountActiveByUserAsync(userId);

            if (activeBookings >= MaxActiveBookingsPerUser)
            {
                throw new BookingLimitExceededException(
                    $"Active bookings limit ({MaxActiveBookingsPerUser}) exceeded");
            }

            var newBookingEntity = Booking.CreatePending(eventId, userId);
            await bookingRepository.AddAsync(newBookingEntity);

            return BookingInfoDto.FromBooking(newBookingEntity);
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

        foundBooking.Cancel();
        await bookingRepository.UpdateAsync(foundBooking);
    }
}
