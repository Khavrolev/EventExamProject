using System.ComponentModel.DataAnnotations;
using EventExamProject.DTOs.Event;
using EventExamProject.Models;
using FluentAssertions;

namespace EventExamProject.Tests;

public class EventTests
{
    private static Event CreateEvent(int totalSeats) =>
        Event.Create(CreateDto(totalSeats));

    private static EventDto CreateDto(int totalSeats, string title = "Event") =>
        new()
        {
            Title = title,
            StartAt = new DateTime(2026, 8, 1),
            EndAt = new DateTime(2026, 8, 1).AddHours(1),
            TotalSeats = totalSeats,
        };

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

    [Fact]
    public void Update_ShouldIncreaseAvailableSeats_ByTheSameAmount_WhenNoSeatsAreBooked()
    {
        var @event = CreateEvent(totalSeats: 5);

        @event.Update(CreateDto(totalSeats: 8));

        @event.TotalSeats.Should().Be(8);
        @event.AvailableSeats.Should().Be(8);
    }

    [Fact]
    public void Update_ShouldPreserveAlreadyBookedSeats_WhenTotalSeatsIsIncreased()
    {
        var @event = CreateEvent(totalSeats: 5);
        @event.TryReserveSeats(3);

        @event.Update(CreateDto(totalSeats: 10));

        @event.TotalSeats.Should().Be(10);
        @event.AvailableSeats.Should().Be(7);
    }

    [Fact]
    public void Update_ShouldNotChangeAvailableSeats_WhenTotalSeatsIsUnchanged()
    {
        var @event = CreateEvent(totalSeats: 5);
        @event.TryReserveSeats(2);

        @event.Update(CreateDto(totalSeats: 5, title: "New title"));

        @event.TotalSeats.Should().Be(5);
        @event.AvailableSeats.Should().Be(3);
    }

    [Fact]
    public void Update_ShouldThrow_AndNotChangeTotalSeats_WhenNewTotalSeatsIsLessThanAlreadyBookedSeats()
    {
        var @event = CreateEvent(totalSeats: 5);
        @event.TryReserveSeats(3);

        var act = () => @event.Update(CreateDto(totalSeats: 2));

        act.Should().Throw<ValidationException>();
        @event.TotalSeats.Should().Be(5);
        @event.AvailableSeats.Should().Be(2);
    }

    [Fact]
    public void Create_ShouldKeepStartAtAndEndAt_WithUtcKind_WhenGivenUtcDates()
    {
        var utcStart = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

        var @event = Event.Create(new EventDto
        {
            Title = "Event",
            StartAt = utcStart,
            EndAt = utcStart.AddHours(1),
            TotalSeats = 5
        });

        @event.StartAt.Kind.Should().Be(DateTimeKind.Utc);
        @event.StartAt.Should().Be(utcStart);
    }

    [Fact]
    public void Create_ShouldConvertLocalDates_ToUtcConvention_WithUtcKind()
    {
        var localStart = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Local);
        var expected = localStart.ToUniversalTime();

        var @event = Event.Create(new EventDto
        {
            Title = "Event",
            StartAt = localStart,
            EndAt = localStart.AddHours(1),
            TotalSeats = 5
        });

        @event.StartAt.Kind.Should().Be(DateTimeKind.Utc);
        @event.StartAt.Should().Be(expected);
    }

    [Fact]
    public void Create_ShouldTreatUnspecifiedDates_AsAlreadyUtc()
    {
        var unspecifiedStart = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Unspecified);

        var @event = Event.Create(new EventDto
        {
            Title = "Event",
            StartAt = unspecifiedStart,
            EndAt = unspecifiedStart.AddHours(1),
            TotalSeats = 5
        });

        @event.StartAt.Kind.Should().Be(DateTimeKind.Utc);
        @event.StartAt.Should().Be(DateTime.SpecifyKind(unspecifiedStart, DateTimeKind.Utc));
    }
}
