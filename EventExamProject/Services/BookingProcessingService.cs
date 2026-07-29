using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Models;

namespace EventExamProject.Services;

public class BookingProcessingService (IBookingStore bookingStore) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pendingBookings = bookingStore.GetByStatus(BookingStatus.Pending).ToList();

            foreach (var booking in pendingBookings)
            {
                await Task.Delay(2000, stoppingToken);
                booking.Confirm();
                bookingStore.Update(booking);
            }
            
            await Task.Delay(5000, stoppingToken);
        }
    }
}