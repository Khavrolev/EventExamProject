using EventExamProject.DataAccess;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

internal class BookingService(AppDbContext context) : IBookingService
{
    private static readonly SemaphoreSlim BookingLock = new(1, 1);

    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await BookingLock.WaitAsync();

        try
        {
            var foundEvent = await context.Events.FindAsync(eventId)
                ?? throw new NotFoundException($"Event with id {eventId} was not found");

            if (!foundEvent.TryReserveSeats())
            {
                throw new NoAvailableSeatsException("No available seats for this event");
            }

            var newBookingEntity = Booking.CreatePending(eventId);
            context.Bookings.Add(newBookingEntity);

            await context.SaveChangesAsync();

            return newBookingEntity;
        }
        finally
        {
            BookingLock.Release();
        }
    }

    public async Task<Booking> GetBookingByIdAsync(Guid id)
    {
        var foundBooking = await context.Bookings.FindAsync(id);

        return foundBooking ?? throw new NotFoundException($"Booking with id {id} was not found");
    }
}
