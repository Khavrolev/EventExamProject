using EventExamProject.Application.Abstractions;
using EventExamProject.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventExamProject.Application.Services;

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

    // The booking is re-read here, after the delay, so it may have been cancelled in the meantime.
    // Only bookings that are still Pending may be processed (and may give their seat back).
    internal async Task HandleBookingAsync(Guid bookingId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

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

            var eventEntity = await eventRepository.GetByIdAsync(booking.EventId, stoppingToken);

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

            await bookingRepository.UpdateAsync(booking);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process booking {BookingId}", bookingId);

            using var scope = scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

            var booking = await bookingRepository.GetByIdAsync(bookingId, stoppingToken);

            if (booking == null || booking.Status != BookingStatus.Pending)
            {
                return;
            }

            booking.Reject();

            var eventEntity = await eventRepository.GetByIdAsync(booking.EventId, stoppingToken);
            eventEntity?.ReleaseSeats();

            await bookingRepository.UpdateAsync(booking);

            if (eventEntity != null)
            {
                await eventRepository.UpdateAsync(eventEntity);
            }
        }
    }
}
