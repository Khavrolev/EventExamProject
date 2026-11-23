using System.Text.Json;
using Confluent.Kafka;
using Contracts;
using Events.Application.Abstractions;
using Events.Application.Common;
using Events.Domain.Entities;
using Events.Domain.ValueObjects;
using Events.Infrastructure.Messaging;
using Events.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EventExamProject.UnitTests;

public class ConsumerIdempotencyTests
{
    [Fact]
    public async Task DuplicateBookingMessage_ProcessedByConsumer_DoesNotReserveSeatsTwice()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<EventsDbContext>(options => options.UseInMemoryDatabase(dbName));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var producerMock = new Mock<IProducer<string, string>>();
        var cacheMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<BookingConfirmedConsumerService>>();
        var kafkaOptions = Options.Create(new KafkaConsumerOptions());
        var redisOptions = Options.Create(new RedisOptions());

        var consumer = new BookingConfirmedConsumerService(
            scopeFactory,
            kafkaOptions,
            producerMock.Object,
            cacheMock.Object,
            redisOptions,
            loggerMock.Object);

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var details = new EventDetails(
                "Test Event",
                "Description",
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(2),
                10);

            var @event = Event.Create(details);
            @event.Id = eventId;

            context.Events.Add(@event);
            await context.SaveChangesAsync();
        }

        var userId = Guid.NewGuid();
        var message = new BookingConfirmed(bookingId, eventId, userId, 1, DateTime.UtcNow);
        var messageJson = JsonSerializer.Serialize(message);

        // Act - отправляем одно и то же сообщение дважды непосредственно в консьюмер
        await consumer.HandleBookingConfirmedMessageAsync(messageJson);
        await consumer.HandleBookingConfirmedMessageAsync(messageJson);

        // Assert - места должны быть зарезервированы ровно один раз (10 - 1 = 9)
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var eventEntity = await context.Events.FindAsync(eventId);
            Assert.NotNull(eventEntity);
            Assert.Equal(9, eventEntity.AvailableSeats);

            var processedCount = await context.ProcessedBookings.CountAsync(p => p.BookingId == bookingId);
            Assert.Equal(1, processedCount);
        }
    }

    [Fact]
    public async Task DuplicateCancellationMessage_ProcessedByConsumer_DoesNotReleaseSeatsTwice()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<EventsDbContext>(options => options.UseInMemoryDatabase(dbName));
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var producerMock = new Mock<IProducer<string, string>>();
        var cacheMock = new Mock<ICacheService>();
        var loggerMock = new Mock<ILogger<BookingConfirmedConsumerService>>();
        var kafkaOptions = Options.Create(new KafkaConsumerOptions());
        var redisOptions = Options.Create(new RedisOptions());

        var consumer = new BookingConfirmedConsumerService(
            scopeFactory,
            kafkaOptions,
            producerMock.Object,
            cacheMock.Object,
            redisOptions,
            loggerMock.Object);

        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        // Сначала подтверждаем бронирование
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var details = new EventDetails(
                "Test Event",
                "Description",
                DateTime.UtcNow.AddDays(1),
                DateTime.UtcNow.AddDays(2),
                10);

            var @event = Event.Create(details);
            @event.Id = eventId;

            context.Events.Add(@event);
            await context.SaveChangesAsync();
        }

        var userId = Guid.NewGuid();
        var confirmedJson = JsonSerializer.Serialize(new BookingConfirmed(bookingId, eventId, userId, 1, DateTime.UtcNow));
        await consumer.HandleBookingConfirmedMessageAsync(confirmedJson);

        var cancelledJson = JsonSerializer.Serialize(new BookingCancelled(bookingId, eventId, userId, 1, DateTime.UtcNow));

        // Act - отправляем отмену дважды
        await consumer.HandleBookingCancelledMessageAsync(cancelledJson);
        await consumer.HandleBookingCancelledMessageAsync(cancelledJson);

        // Assert - места должны вернуться ровно один раз (9 + 1 = 10, а не 11)
        using (var scope = scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var eventEntity = await context.Events.FindAsync(eventId);
            Assert.NotNull(eventEntity);
            Assert.Equal(10, eventEntity.AvailableSeats);

            var processedCount = await context.ProcessedBookings.CountAsync(p => p.BookingId == bookingId);
            Assert.Equal(0, processedCount);
        }
    }
}
