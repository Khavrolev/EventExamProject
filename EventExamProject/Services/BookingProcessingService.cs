using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Models;

namespace EventExamProject.Services;

public class BookingProcessingService(IBookingStore bookingStore, ILogger<BookingProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Booking processing service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var pendingBookings = bookingStore.GetByStatus(BookingStatus.Pending).ToList();

            foreach (var booking in pendingBookings)
            {
                try
                {
                    await Task.Delay(2000, stoppingToken);

                    booking.Confirm();
                    bookingStore.Update(booking);

                    logger.LogInformation("Booking {BookingId} confirmed", booking.Id);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process booking {BookingId}", booking.Id);
                }
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}