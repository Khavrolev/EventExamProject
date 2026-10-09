using EventExamProject.Domain.Entities;

namespace EventExamProject.Application.Abstractions;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Booking newBooking);
    Task UpdateAsync(Booking booking);
    Task DeleteAsync(Booking booking);
}
