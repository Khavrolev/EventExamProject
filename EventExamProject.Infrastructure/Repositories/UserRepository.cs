using EventExamProject.Application.Abstractions;
using EventExamProject.Domain.Entities;
using EventExamProject.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventExamProject.Infrastructure.Repositories;

internal sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default) =>
        await context.Users.FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

    public async Task AddAsync(User newUser)
    {
        context.Users.Add(newUser);
        await context.SaveChangesAsync();
    }
}
