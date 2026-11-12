using Bookings.Domain.Entities;

namespace Bookings.Application.Abstractions;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Booking newBooking);
    Task UpdateAsync(Booking existingBooking);
}
