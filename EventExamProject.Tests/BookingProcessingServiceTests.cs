using EventExamProject.Application.Abstractions;
using EventExamProject.Application.DTOs.Event;
using EventExamProject.Application.Services;
using EventExamProject.Application.Services.Interfaces;
using EventExamProject.Domain.Entities;
using EventExamProject.Infrastructure.Persistence;
using EventExamProject.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventExamProject.Tests;

public class BookingProcessingServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly IBookingService _bookingService;
    private readonly IEventService _eventService;
    private readonly BookingProcessingService _processingService;
    private readonly Guid _userId = Guid.NewGuid();

    public BookingProcessingServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _bookingService = _scope.ServiceProvider.GetRequiredService<IBookingService>();
        _eventService = _scope.ServiceProvider.GetRequiredService<IEventService>();
        _processingService = new BookingProcessingService(
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookingProcessingService>.Instance);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }

    private async Task<(Guid EventId, Guid BookingId)> CreatePendingBookingAsync(int totalSeats = 3)
    {
        var createdEvent = await _eventService.CreateEventAsync(new EventDto
        {
            Title = "Event",
            StartAt = DateTime.UtcNow.AddDays(30),
            EndAt = DateTime.UtcNow.AddDays(30).AddHours(1),
            TotalSeats = totalSeats
        });
        var booking = await _bookingService.CreateBookingAsync(createdEvent.Id, _userId);

        return (createdEvent.Id, booking.Id);
    }

    // The processing service works in its own scope/DbContext, so the result is read through a fresh
    // scope too; the test's own context would return its stale tracked entity.
    private async Task<BookingStatus> ReadStatusAsync(Guid bookingId)
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookingService>();

        return (await service.GetBookingByIdAsync(bookingId)).Status;
    }

    [Fact]
    public async Task HandleBooking_ShouldConfirmBooking_WhenBookingIsPending()
    {
        var (_, bookingId) = await CreatePendingBookingAsync();

        await _processingService.HandleBookingAsync(bookingId, CancellationToken.None);

        (await ReadStatusAsync(bookingId)).Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task HandleBooking_ShouldKeepCancelledStatus_WhenBookingWasCancelledBeforeProcessing()
    {
        var (eventId, bookingId) = await CreatePendingBookingAsync(totalSeats: 3);
        await _bookingService.CancelBookingAsync(bookingId, _userId, UserRole.User);

        await _processingService.HandleBookingAsync(bookingId, CancellationToken.None);

        (await ReadStatusAsync(bookingId)).Should().Be(BookingStatus.Cancelled);

        var updatedEvent = await _eventService.GetEventByIdAsync(eventId);
        updatedEvent.AvailableSeats.Should().Be(3, "the seat was returned by the cancellation and must not be taken again");
    }

    [Fact]
    public async Task HandleBooking_ShouldNotChangeAlreadyProcessedBooking()
    {
        var (_, bookingId) = await CreatePendingBookingAsync();
        await _processingService.HandleBookingAsync(bookingId, CancellationToken.None);

        var act = () => _processingService.HandleBookingAsync(bookingId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        (await ReadStatusAsync(bookingId)).Should().Be(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task HandleBooking_ShouldNotThrow_WhenBookingDoesNotExist()
    {
        var act = () => _processingService.HandleBookingAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
