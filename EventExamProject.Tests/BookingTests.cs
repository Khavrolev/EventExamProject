using EventExamProject.Domain.Entities;
using EventExamProject.Domain.Exceptions;
using FluentAssertions;

namespace EventExamProject.Tests;

public class BookingTests
{
    [Fact]
    public void CreatePending_ShouldInitializeBooking_WithPendingStatus()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var booking = Booking.CreatePending(eventId, userId);

        var after = DateTime.UtcNow;
        booking.Id.Should().NotBe(Guid.Empty);
        booking.EventId.Should().Be(eventId);
        booking.UserId.Should().Be(userId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        booking.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public void CreatePending_ShouldGenerateUniqueIds_WhenCalledMultipleTimes()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = Booking.CreatePending(eventId, userId);
        var second = Booking.CreatePending(eventId, userId);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Confirm_ShouldSetStatusToConfirmed_AndFillProcessedAt()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        var before = DateTime.UtcNow;

        booking.Confirm();

        var after = DateTime.UtcNow;
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Reject_ShouldSetStatusToRejected_AndFillProcessedAt()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        var before = DateTime.UtcNow;

        booking.Reject();

        var after = DateTime.UtcNow;
        booking.Status.Should().Be(BookingStatus.Rejected);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Cancel_ShouldSetStatusToCancelled_AndFillProcessedAt()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        var before = DateTime.UtcNow;

        booking.Cancel();

        booking.Status.Should().Be(BookingStatus.Cancelled);
        booking.ProcessedAt.Should().NotBeNull();
        booking.ProcessedAt!.Value.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Cancel_ShouldWork_ForConfirmedBooking()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Confirm();

        booking.Cancel();

        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public void Cancel_ShouldThrowDomainValidationException_WhenAlreadyCancelled()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Cancel();

        var act = () => booking.Cancel();

        act.Should().Throw<DomainValidationException>();
    }
}
