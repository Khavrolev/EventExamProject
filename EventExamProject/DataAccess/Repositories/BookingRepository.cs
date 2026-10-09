using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.DataAccess.Repositories;

internal sealed class BookingRepository(AppDbContext context) : IBookingRepository
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

    public async Task DeleteAsync(Booking booking)
    {
        context.Bookings.Remove(booking);
        await context.SaveChangesAsync();
    }
}
