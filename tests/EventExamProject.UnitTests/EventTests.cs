using Events.Domain.Entities;
using Events.Domain.Exceptions;
using Events.Domain.ValueObjects;
using Xunit;

namespace EventExamProject.UnitTests;

public class EventTests
{
    [Fact]
    public void TryReserveSeats_DecrementsAvailableSeats_WhenEnoughSeats()
    {
        var details = new EventDetails(
            "Test Conference",
            "Description",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            5);

        var @event = Event.Create(details);

        var success = @event.TryReserveSeats(2);

        Assert.True(success);
        Assert.Equal(3, @event.AvailableSeats);
    }

    [Fact]
    public void TryReserveSeats_ReturnsFalse_WhenNotEnoughSeats()
    {
        var details = new EventDetails(
            "Test Conference",
            "Description",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            2);

        var @event = Event.Create(details);

        var success = @event.TryReserveSeats(3);

        Assert.False(success);
        Assert.Equal(2, @event.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_IncrementsAvailableSeats()
    {
        var details = new EventDetails(
            "Test Conference",
            "Description",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            5);

        var @event = Event.Create(details);

        @event.TryReserveSeats(2);
        @event.ReleaseSeats(1);

        Assert.Equal(4, @event.AvailableSeats);
    }

    [Fact]
    public void ReleaseSeats_ThrowsIfReleasingMoreThanTotal()
    {
        var details = new EventDetails(
            "Test Conference",
            "Description",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2),
            5);

        var @event = Event.Create(details);

        Assert.Throws<DomainValidationException>(() => @event.ReleaseSeats(1));
    }
}
