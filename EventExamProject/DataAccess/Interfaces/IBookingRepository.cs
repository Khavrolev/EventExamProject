using EventExamProject.Models;

namespace EventExamProject.DataAccess.Interfaces;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Booking newBooking);
    Task UpdateAsync(Booking booking);
}
