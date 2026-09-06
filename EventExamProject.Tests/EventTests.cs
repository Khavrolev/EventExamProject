using System.ComponentModel.DataAnnotations;
using EventExamProject.DTOs.Event;
using EventExamProject.Models;
using FluentAssertions;

namespace EventExamProject.Tests;

public class EventTests
{
    private static Event CreateEvent(int totalSeats) =>
        Event.Create(new EventDto
        {
            Title = "Event",
            StartAt = new DateTime(2026, 8, 1),
            EndAt = new DateTime(2026, 8, 1).AddHours(1),
            TotalSeats = totalSeats,
        });

    [Fact]
    public void TryReserveSeats_ShouldReturnTrue_AndDecreaseAvailableSeats_WhenSeatsAreAvailable()
    {
        var @event = CreateEvent(totalSeats: 5);

        var result = @event.TryReserveSeats();

        result.Should().BeTrue();
        @event.AvailableSeats.Should().Be(4);
    }

    [Fact]
    public void TryReserveSeats_ShouldReturnTrue_WhenReservingExactlyTheLastSeat()
    {
        var @event = CreateEvent(totalSeats: 1);

        var result = @event.TryReserveSeats();

        result.Should().BeTrue();
        @event.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public void TryReserveSeats_ShouldReturnFalse_AndNotChangeAvailableSeats_WhenNoSeatsAreAvailable()
    {
        var @event = CreateEvent(totalSeats: 1);
        @event.TryReserveSeats();

        var result = @event.TryReserveSeats();

        result.Should().BeFalse();
        @event.AvailableSeats.Should().Be(0);
    }

    [Fact]
    public void TryReserveSeats_ShouldReserveRequestedCount_WhenCountIsGreaterThanOne()
    {
        var @event = CreateEvent(totalSeats: 5);

        var result = @event.TryReserveSeats(3);

        result.Should().BeTrue();
        @event.AvailableSeats.Should().Be(2);
    }

    [Fact]
    public void ReleaseSeats_ShouldIncreaseAvailableSeats_AfterSeatsWereReserved()
    {
        var @event = CreateEvent(totalSeats: 5);
        @event.TryReserveSeats();

        @event.ReleaseSeats();

        @event.AvailableSeats.Should().Be(5);
    }

    [Fact]
    public void ReleaseSeats_ShouldThrow_WhenReleasingMoreSeatsThanReserved()
    {
        var @event = CreateEvent(totalSeats: 5);

        var act = () => @event.ReleaseSeats();

        act.Should().Throw<ValidationException>();
    }
}
