using Bookings.Application.Abstractions;
using Bookings.Domain.Entities;
using Bookings.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bookings.Infrastructure.Repositories;

public class OutboxRepository(BookingsDbContext context) : IOutboxRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await context.OutboxMessages.AddAsync(message, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<OutboxMessage>> GetUnprocessedMessagesAsync(int batchSize = 20, CancellationToken cancellationToken = default)
    {
        return await context.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.ProcessedAt = DateTime.UtcNow;
            message.Error = null;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await context.OutboxMessages.FindAsync([messageId], cancellationToken);
        if (message != null)
        {
            message.Error = error;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
