using EventExamProject.Models;
using FluentAssertions;

namespace EventExamProject.Tests;

public class BookingTests
{
    [Fact]
    public void CreatePending_ShouldInitializeBooking_WithPendingStatus()
    {
        var eventId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var booking = Booking.CreatePending(eventId);

        var after = DateTime.UtcNow;
        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(eventId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void CreatePending_ShouldGenerateUniqueIds_WhenCalledMultipleTimes()
    {
        var eventId = Guid.NewGuid();

        var first = Booking.CreatePending(eventId);
        var second = Booking.CreatePending(eventId);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Confirm_ShouldSetStatusToConfirmed_AndFillProcessedAt()
    {
        var booking = Booking.CreatePending(Guid.NewGuid());
        var before = DateTime.UtcNow;

        booking.Confirm();

        var after = DateTime.UtcNow;
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
