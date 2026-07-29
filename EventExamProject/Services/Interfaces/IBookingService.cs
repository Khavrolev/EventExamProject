using EventExamProject.Models;

namespace EventExamProject.Services.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBooking(Guid eventId);
    Task<Booking> GetBookingById(Guid id);
}