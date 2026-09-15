using System.Collections.Concurrent;
using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

internal class BookingService(IEventRepository eventRepository, IBookingRepository bookingRepository) : IBookingService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> EventLocks = new();

    private static SemaphoreSlim GetLockFor(Guid eventId) =>
        EventLocks.GetOrAdd(eventId, static _ => new SemaphoreSlim(1, 1));

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        var eventLock = GetLockFor(eventId);
        await eventLock.WaitAsync();

        try
        {
            var foundEvent = await eventRepository.GetByIdAsync(eventId)
                ?? throw new NotFoundException($"Event with id {eventId} was not found");

            if (!foundEvent.TryReserveSeats())
            {
                throw new NoAvailableSeatsException("No available seats for this event");
            }

            await eventRepository.UpdateAsync(foundEvent);

            var newBookingEntity = Booking.CreatePending(eventId);
            await bookingRepository.AddAsync(newBookingEntity);

            return newBookingEntity;
        }
        finally
        {
            eventLock.Release();
        }
    }

    public async Task<Booking> GetBookingByIdAsync(Guid id)
    {
        var foundBooking = await bookingRepository.GetByIdAsync(id);

        return foundBooking ?? throw new NotFoundException($"Booking with id {id} was not found");
    }
}
