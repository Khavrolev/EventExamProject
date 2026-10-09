using EventExamProject.Domain.Entities;

namespace EventExamProject.Services.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId);
    Task<Booking> GetBookingByIdAsync(Guid id);
}
