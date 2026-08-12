using EventExamProject.Models;

namespace EventExamProject.Services.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId);
    Task<Booking> GetBookingByIdAsync(Guid id);
}
