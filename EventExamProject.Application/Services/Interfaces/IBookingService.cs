using EventExamProject.Application.DTOs.Booking;
using EventExamProject.Domain.Entities;

namespace EventExamProject.Application.Services.Interfaces;

public interface IBookingService
{
    Task<BookingInfoDto> CreateBookingAsync(Guid eventId, Guid userId);
    Task<BookingInfoDto> GetBookingByIdAsync(Guid id);
    Task CancelBookingAsync(Guid bookingId, Guid userId, UserRole userRole);
}
