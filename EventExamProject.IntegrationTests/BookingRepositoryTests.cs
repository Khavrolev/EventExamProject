using EventExamProject.DataAccess.Repositories;
using EventExamProject.DTOs.Event;
using EventExamProject.IntegrationTests.Infrastructure;
using EventExamProject.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class BookingRepositoryTests(DatabaseFixture fixture) : RepositoryTestBase(fixture)
{
    private static Event CreateEvent(int totalSeats = 10) =>
        Event.Create(new EventDto
        {
            Title = "Event",
            Description = "Description",
            StartAt = new DateTime(2026, 8, 1),
            EndAt = new DateTime(2026, 8, 1).AddHours(1),
            TotalSeats = totalSeats
        });

    private async Task<Event> SeedEventAsync(int totalSeats = 10)
    {
        var newEvent = CreateEvent(totalSeats);
        Context.Events.Add(newEvent);
        await Context.SaveChangesAsync();

        return newEvent;
    }

    [Fact]
    public async Task AddAsync_ShouldPersistBooking_ToDatabase()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id);

        // Act
        await repository.AddAsync(booking);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Bookings.FindAsync(booking.Id);

        stored.Should().NotBeNull();
        stored!.EventId.Should().Be(seededEvent.Id);
        stored.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveBooking_FromDatabase()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id);
        await repository.AddAsync(booking);

        // Act
        await repository.DeleteAsync(booking);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Bookings.FindAsync(booking.Id);

        stored.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnBooking_WhenBookingExists()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id);
        await repository.AddAsync(booking);

        // Act
        var found = await repository.GetByIdAsync(booking.Id);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(booking.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenBookingDoesNotExist()
    {
        // Arrange
        var repository = new BookingRepository(Context);

        // Act
        var found = await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistConfirmedStatus_ToDatabase()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id);
        await repository.AddAsync(booking);

        // Act
        booking.Confirm();
        await repository.UpdateAsync(booking);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Bookings.FindAsync(booking.Id);

        stored!.Status.Should().Be(BookingStatus.Confirmed);
        stored.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistRejectedStatus_ToDatabase()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id);
        await repository.AddAsync(booking);

        // Act
        booking.Reject();
        await repository.UpdateAsync(booking);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Bookings.FindAsync(booking.Id);

        stored!.Status.Should().Be(BookingStatus.Rejected);
        stored.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_ShouldReturnOnlyPendingBookings()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var repository = new BookingRepository(Context);

        var pendingA = Booking.CreatePending(seededEvent.Id);
        var pendingB = Booking.CreatePending(seededEvent.Id);

        var confirmed = Booking.CreatePending(seededEvent.Id);
        confirmed.Confirm();

        var rejected = Booking.CreatePending(seededEvent.Id);
        rejected.Reject();

        await repository.AddAsync(pendingA);
        await repository.AddAsync(pendingB);
        await repository.AddAsync(confirmed);
        await repository.AddAsync(rejected);

        // Act
        var pendingIds = await repository.GetPendingBookingIdsAsync();

        // Assert
        pendingIds.Should().BeEquivalentTo([pendingA.Id, pendingB.Id]);
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_ShouldReturnEmpty_WhenNoPendingBookingsExist()
    {
        // Arrange
        var repository = new BookingRepository(Context);

        // Act
        var pendingIds = await repository.GetPendingBookingIdsAsync();

        // Assert
        pendingIds.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_ShouldThrow_WhenEventDoesNotExist()
    {
        // Arrange
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(Guid.NewGuid());

        // Act
        var act = () => repository.AddAsync(booking);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>("the foreign key to events should reject an unknown EventId");
    }
}
