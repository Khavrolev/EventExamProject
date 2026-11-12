using Bookings.Application.Abstractions;
using Bookings.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bookings.Application.Services;

public class BookingProcessingService(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingProcessingService> logger) : BackgroundService
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
                var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                pendingBookingIds = await bookingRepository.GetPendingBookingIdsAsync(stoppingToken);
            }

            var tasks = pendingBookingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
            await Task.WhenAll(tasks);

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        await Task.Delay(ProcessingDelay, stoppingToken);
        await HandleBookingAsync(bookingId, stoppingToken);
    }

    public virtual async Task HandleBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();

            var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);

            if (booking == null)
            {
                return;
            }

            if (booking.Status != BookingStatus.Pending)
            {
                logger.LogInformation("Booking {BookingId} is already {Status}, skipping", booking.Id, booking.Status);
                return;
            }

            booking.Confirm();
            await bookingRepository.UpdateAsync(booking);
            logger.LogInformation("Booking {BookingId} confirmed in database", booking.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process booking {BookingId}", bookingId);
        }
    }
}
