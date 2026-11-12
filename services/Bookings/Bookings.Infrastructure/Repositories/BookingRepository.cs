using Bookings.Application.Abstractions;
using Bookings.Domain.Entities;
using Bookings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bookings.Infrastructure.Repositories;

internal sealed class BookingRepository(BookingsDbContext context) : IBookingRepository
{
    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Bookings.FindAsync([id], cancellationToken);

    public async Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken cancellationToken = default)
    {
        return await context.Bookings
            .Where(b => b.Status == BookingStatus.Pending)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Bookings
            .CountAsync(b => b.UserId == userId
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed),
                cancellationToken);
    }

    public async Task AddAsync(Booking newBooking)
    {
        context.Bookings.Add(newBooking);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Booking booking)
    {
        context.Bookings.Update(booking);
        await context.SaveChangesAsync();
    }
}
