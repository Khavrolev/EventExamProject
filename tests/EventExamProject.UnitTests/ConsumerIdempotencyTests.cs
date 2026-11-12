using Events.Domain.Entities;
using Events.Domain.ValueObjects;
using Events.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventExamProject.UnitTests;

public class ConsumerIdempotencyTests
{
    [Fact]
    public void DuplicateBooking_DoesNotReserveSeatsTwice()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var context = new EventsDbContext(options))
        {
            var details = new EventDetails(
                "Test Event",
                "Description",
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(2),
                10);

            var @event = Event.Create(details);
            @event.Id = eventId;

            context.Events.Add(@event);
            context.SaveChanges();
        }

        using (var context = new EventsDbContext(options))
        {
            var eventEntity = context.Events.Find(eventId)!;
            eventEntity.TryReserveSeats(1);
            context.ProcessedBookings.Add(new ProcessedBooking
            {
                BookingId = bookingId,
                ProcessedAt = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        using (var context = new EventsDbContext(options))
        {
            var isAlreadyProcessed = context.ProcessedBookings.Any(p => p.BookingId == bookingId);
            Assert.True(isAlreadyProcessed);

            var eventEntity = context.Events.Find(eventId)!;
            Assert.Equal(9, eventEntity.AvailableSeats);

            if (!isAlreadyProcessed)
            {
                eventEntity.TryReserveSeats(1);
                context.SaveChanges();
            }

            Assert.Equal(9, eventEntity.AvailableSeats);
        }
    }
}
