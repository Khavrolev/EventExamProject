using System.Collections.Concurrent;
using System.Text.Json;
using Bookings.Application.Abstractions;
using Bookings.Application.DTOs;
using Bookings.Application.Services.Interfaces;
using Bookings.Domain.Entities;
using Bookings.Domain.Exceptions;
using Contracts;

namespace Bookings.Application.Services;

public class BookingService(
    IBookingRepository bookingRepository,
    IOutboxRepository outboxRepository,
    IBookingEventProducer eventProducer) : IBookingService
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

    public async Task<BookingInfoDto> GetBookingByIdAsync(Guid id, Guid currentUserId, UserRole userRole)
    {
        var foundBooking = await bookingRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Booking with id {id} was not found");

        if (userRole != UserRole.Admin && foundBooking.UserId != currentUserId)
        {
            throw new ForbiddenException("You can view only your own bookings");
        }

        return BookingInfoDto.FromBooking(foundBooking);
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

        var cancelledEvent = new BookingCancelled(
            foundBooking.Id,
            foundBooking.EventId,
            foundBooking.UserId,
            1,
            foundBooking.ProcessedAt ?? DateTime.UtcNow);

        var outboxMessage = OutboxMessage.Create(
            KafkaTopics.BookingCancelled,
            foundBooking.EventId.ToString(),
            JsonSerializer.Serialize(cancelledEvent));

        await outboxRepository.AddAsync(outboxMessage);

        try
        {
            await eventProducer.PublishBookingCancelledAsync(cancelledEvent);
            await outboxRepository.MarkAsProcessedAsync(outboxMessage.Id);
        }
        catch
        {
        }
    }
}
