using EventExamProject.DataAccess;
using EventExamProject.Models;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.Services;

internal class BookingProcessingService(IServiceScopeFactory scopeFactory, ILogger<BookingProcessingService> logger) : BackgroundService
{
    private const int PollingInterval = 5000;
    private const int ProcessingDelay = 2000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Booking processing service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            List<Guid> pendingBookingIds;

            using (var scope = scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                pendingBookingIds = await context.Bookings
                    .Where(b => b.Status == BookingStatus.Pending)
                    .Select(b => b.Id)
                    .ToListAsync(stoppingToken);
            }

            var tasks = pendingBookingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
            await Task.WhenAll(tasks);

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        await Task.Delay(ProcessingDelay, stoppingToken);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var booking = await context.Bookings.FindAsync([bookingId], stoppingToken);

            if (booking == null)
            {
                return;
            }

            var eventEntity = await context.Events.FindAsync([booking.EventId], stoppingToken);

            if (eventEntity == null)
            {
                booking.Reject();
                logger.LogWarning("Booking {BookingId} rejected", booking.Id);
            }
            else
            {
                booking.Confirm();
                logger.LogInformation("Booking {BookingId} confirmed", booking.Id);
            }

            await context.SaveChangesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process booking {BookingId}", bookingId);

            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var booking = await context.Bookings.FindAsync([bookingId], stoppingToken);

            if (booking == null)
            {
                return;
            }

            booking.Reject();

            var eventEntity = await context.Events.FindAsync([booking.EventId], stoppingToken);
            eventEntity?.ReleaseSeats();

            await context.SaveChangesAsync(stoppingToken);
        }
    }
}
