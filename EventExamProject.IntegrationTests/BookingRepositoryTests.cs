using EventExamProject.Infrastructure.Repositories;
using EventExamProject.IntegrationTests.Infrastructure;
using EventExamProject.Domain.Entities;
using EventExamProject.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class BookingRepositoryTests(DatabaseFixture fixture) : RepositoryTestBase(fixture)
{
    private static Event CreateEvent(int totalSeats = 10) =>
        Event.Create(new EventDetails(
            "Event",
            "Description",
            new DateTime(2026, 8, 1),
            new DateTime(2026, 8, 1).AddHours(1),
            totalSeats));

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
        var userId = (await SeedUserAsync()).Id;
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id, userId);

        // Act
        await repository.AddAsync(booking);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Bookings.FindAsync(booking.Id);

        stored.Should().NotBeNull();
        stored!.EventId.Should().Be(seededEvent.Id);
        stored.UserId.Should().Be(userId);
        stored.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveBooking_FromDatabase()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var userId = (await SeedUserAsync()).Id;
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id, userId);
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
        var userId = (await SeedUserAsync()).Id;
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id, userId);
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
        var userId = (await SeedUserAsync()).Id;
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id, userId);
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
        var userId = (await SeedUserAsync()).Id;
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id, userId);
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
        var userId = (await SeedUserAsync()).Id;
        var repository = new BookingRepository(Context);

        var pendingA = Booking.CreatePending(seededEvent.Id, userId);
        var pendingB = Booking.CreatePending(seededEvent.Id, userId);

        var confirmed = Booking.CreatePending(seededEvent.Id, userId);
        confirmed.Confirm();

        var rejected = Booking.CreatePending(seededEvent.Id, userId);
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
        var booking = Booking.CreatePending(Guid.NewGuid(), (await SeedUserAsync()).Id);

        // Act
        var act = () => repository.AddAsync(booking);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>("the foreign key to events should reject an unknown EventId");
    }

    [Fact]
    public async Task AddAsync_ShouldThrow_WhenUserDoesNotExist()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id, Guid.NewGuid());

        // Act
        var act = () => repository.AddAsync(booking);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>("the foreign key to users should reject an unknown UserId");
    }

    [Fact]
    public async Task CountActiveByUserAsync_ShouldCountOnlyPendingAndConfirmedBookingsOfThatUser()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var user = await SeedUserAsync();
        var otherUser = await SeedUserAsync();
        var repository = new BookingRepository(Context);

        var pending = Booking.CreatePending(seededEvent.Id, user.Id);
        var confirmed = Booking.CreatePending(seededEvent.Id, user.Id);
        confirmed.Confirm();
        var rejected = Booking.CreatePending(seededEvent.Id, user.Id);
        rejected.Reject();
        var cancelled = Booking.CreatePending(seededEvent.Id, user.Id);
        cancelled.Cancel();
        var foreign = Booking.CreatePending(seededEvent.Id, otherUser.Id);

        foreach (var booking in new[] { pending, confirmed, rejected, cancelled, foreign })
        {
            await repository.AddAsync(booking);
        }

        // Act
        var count = await repository.CountActiveByUserAsync(user.Id);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistCancelledStatus_ToDatabase()
    {
        // Arrange
        var seededEvent = await SeedEventAsync();
        var userId = (await SeedUserAsync()).Id;
        var repository = new BookingRepository(Context);
        var booking = Booking.CreatePending(seededEvent.Id, userId);
        await repository.AddAsync(booking);

        // Act
        booking.Cancel();
        await repository.UpdateAsync(booking);

        // Assert
        await using var verificationContext = CreateContext();
        var stored = await verificationContext.Bookings.FindAsync(booking.Id);

        stored!.Status.Should().Be(BookingStatus.Cancelled);
        stored.ProcessedAt.Should().NotBeNull();
    }
}
