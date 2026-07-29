using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using EventExamProject.Services.Interfaces;

namespace EventExamProject.Services;

public class BookingService(IBookingStore bookingStore, IEventService eventService) : IBookingService
{
    public async Task<Booking> CreateBooking(Guid eventId)
    {
        await eventService.GetEventById(eventId);
        var newBookingEntity = Booking.CreatePending(eventId);
        bookingStore.Add(newBookingEntity);

        return newBookingEntity;
    }
    
    public Task<Booking> GetBookingById(Guid id)
    {
        var foundBooking = bookingStore.GetById(id);

        return foundBooking == null ? throw new NotFoundException($"Booking with id {id} was not found") : Task.FromResult(foundBooking);
    }
}