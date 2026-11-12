using Microsoft.EntityFrameworkCore;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Infrastructure.Persistence;

namespace Users.Infrastructure.Repositories;

internal class UserRepository(UsersDbContext context) : IUserRepository
{
    public async Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .SingleOrDefaultAsync(u => u.Login == login, cancellationToken);
    }

    public async Task AddAsync(User newUser)
    {
        await context.Users.AddAsync(newUser);
        await context.SaveChangesAsync();
    }
}
