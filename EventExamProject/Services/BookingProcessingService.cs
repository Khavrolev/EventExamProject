using EventExamProject.DataAccess.Interfaces;
using EventExamProject.Models;

namespace EventExamProject.Services;

public class BookingProcessingService(IBookingStore bookingStore, IEventStore eventStore, ILogger<BookingProcessingService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Booking processing service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var pendingBookings = bookingStore.GetByStatus(BookingStatus.Pending).ToList();
            
            var tasks = pendingBookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));
            await Task.WhenAll(tasks); 

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
    {
        Event? eventEntity = null;
        var semaphoreTaken = false;
        
        try
        {
            await Task.Delay(2000, stoppingToken);
            await _processingSemaphore.WaitAsync(stoppingToken);
            semaphoreTaken = true;
            
            eventEntity = eventStore.GetById(booking.EventId);

            if (eventEntity == null)
            {
                booking.Reject();
                bookingStore.Update(booking);
                logger.LogWarning("Booking {BookingId} rejected", booking.Id);
            }
            else
            {
                booking.Confirm();
                bookingStore.Update(booking);
                logger.LogInformation("Booking {BookingId} confirmed", booking.Id);

            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            booking.Reject();
            bookingStore.Update(booking);

            if (eventEntity != null)
            {
                eventEntity.ReleaseSeats();
                eventStore.Update(eventEntity);
            }
            
            logger.LogError(ex, "Failed to process booking {BookingId}", booking.Id);
        }
        finally
        {
            if (semaphoreTaken) {
                _processingSemaphore.Release();
            }
        }
    }
}