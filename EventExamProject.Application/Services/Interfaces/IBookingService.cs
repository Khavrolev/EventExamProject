using EventExamProject.Application.DTOs.Booking;

namespace EventExamProject.Application.Services.Interfaces;

public interface IBookingService
{
    Task<BookingInfoDto> CreateBookingAsync(Guid eventId);
    Task<BookingInfoDto> GetBookingByIdAsync(Guid id);
}
