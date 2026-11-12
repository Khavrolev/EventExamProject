using Bookings.Domain.Entities;

namespace Bookings.Application.Abstractions;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}
