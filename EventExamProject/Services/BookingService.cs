using System.Collections.Concurrent;
using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

public class BookingService(IBookingStore bookingStore, IEventStore eventStore, IEventService eventService) : IBookingService
{
    private readonly ConcurrentDictionary<Guid, object> _eventLocks = new();

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        var foundEvent = await eventService.GetEventByIdAsync(eventId);
        var eventLock = _eventLocks.GetOrAdd(eventId, _ => new object());

        lock(eventLock){
            if (!foundEvent.TryReserveSeats())
            {
                throw new NoAvailableSeatsException("No available seats for this event");
            }

            eventStore.Update(foundEvent);

            var newBookingEntity = Booking.CreatePending(eventId);
            bookingStore.Add(newBookingEntity);

            return newBookingEntity;
        }
    }

    public Task<Booking> GetBookingByIdAsync(Guid id)
    {
        var foundBooking = bookingStore.GetById(id);

        return foundBooking == null ? throw new NotFoundException($"Booking with id {id} was not found") : Task.FromResult(foundBooking);
    }
}