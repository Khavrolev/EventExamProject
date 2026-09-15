using EventExamProject.DataAccess.Interfaces;
using EventExamProject.DTOs.Event;
using EventExamProject.DTOs.Pagination;
using EventExamProject.Models;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.DataAccess.Repositories;

internal sealed class EventRepository(AppDbContext context) : IEventRepository
{
    public async Task<PaginatedResultDto<Event>> GetAllAsync(EventFilterDto filter, PaginationParamsDto paginationParams)
    {
        var filtered = context.Events.AsQueryable();

        if (!string.IsNullOrEmpty(filter.Title))
        {
            var title = filter.Title.ToLower();
            filtered = filtered.Where(e => e.Title.ToLower().Contains(title));
        }

        if (filter.From.HasValue)
        {
            filtered = filtered.Where(e => e.StartAt >= filter.From);
        }

        if (filter.To.HasValue)
        {
            filtered = filtered.Where(e => e.EndAt <= filter.To);
        }

        var totalCount = await filtered.CountAsync();

        var paginated = await filtered
            .OrderBy(e => e.StartAt)
            .ThenBy(e => e.Id)
            .Skip((paginationParams.Page - 1) * paginationParams.PageSize)
            .Take(paginationParams.PageSize)
            .ToListAsync();

        return new PaginatedResultDto<Event>
        {
            Data = paginated, TotalCount = totalCount, Page = paginationParams.Page, PageSize = paginationParams.PageSize
        };
    }

    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await context.Events.FindAsync([id], cancellationToken);

    public async Task AddAsync(Event newEvent)
    {
        context.Events.Add(newEvent);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Event existingEvent)
    {
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Event eventToDelete)
    {
        context.Events.Remove(eventToDelete);
        await context.SaveChangesAsync();
    }
}
