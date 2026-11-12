using Bookings.Application.DTOs;
using Bookings.Domain.Entities;

namespace Bookings.Application.Services.Interfaces;

public interface IBookingService
{
    Task<BookingInfoDto> CreateBookingAsync(Guid eventId, Guid userId);
    Task<BookingInfoDto> GetBookingByIdAsync(Guid id, Guid currentUserId, UserRole userRole);
    Task CancelBookingAsync(Guid bookingId, Guid userId, UserRole userRole);
}
