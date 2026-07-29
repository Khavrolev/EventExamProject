using EventExamProject.DataAccess;
using EventExamProject.Exceptions;
using EventExamProject.Models;
using FluentAssertions;

namespace EventExamProject.Tests;

public class InMemoryBookingStoreTests
{
    [Fact]
    public void GetById_ShouldReturnBooking_WhenBookingExists()
    {
        var store = new InMemoryBookingStore();
        var booking = Booking.CreatePending(Guid.NewGuid());
        store.Add(booking);

        var found = store.GetById(booking.Id);

        found.Should().Be(booking);
    }

    [Fact]
    public void GetById_ShouldReturnNull_WhenBookingDoesNotExist()
    {
        var store = new InMemoryBookingStore();

        var found = store.GetById(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public void GetByStatus_ShouldReturnOnlyBookingsWithMatchingStatus()
    {
        var store = new InMemoryBookingStore();
        var pending = Booking.CreatePending(Guid.NewGuid());
        var confirmed = Booking.CreatePending(Guid.NewGuid());
        confirmed.Confirm();
        store.Add(pending);
        store.Add(confirmed);

        var result = store.GetByStatus(BookingStatus.Pending);

        result.Should().ContainSingle().Which.Id.Should().Be(pending.Id);
    }

    [Fact]
    public void Update_ShouldPersistChanges_WhenBookingExists()
    {
        var store = new InMemoryBookingStore();
        var booking = Booking.CreatePending(Guid.NewGuid());
        store.Add(booking);

        booking.Confirm();
        store.Update(booking);

        var found = store.GetById(booking.Id);
        found!.Status.Should().Be(BookingStatus.Confirmed);
        found.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Update_ShouldThrowNotFoundException_WhenBookingDoesNotExist()
    {
        var store = new InMemoryBookingStore();
        var booking = Booking.CreatePending(Guid.NewGuid());

        var act = () => store.Update(booking);

        act.Should().Throw<NotFoundException>();
    }
}
