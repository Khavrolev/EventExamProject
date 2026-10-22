using EventExamProject.Application.Abstractions;
using EventExamProject.Domain.Entities;
using EventExamProject.Domain.Exceptions;
using EventExamProject.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventExamProject.Infrastructure.Repositories;

internal sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default) =>
        await context.Users.FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

    public async Task AddAsync(User newUser)
    {
        context.Users.Add(newUser);

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsLoginUniqueViolation(ex))
        {
            // Two parallel registrations with the same login both pass the service-level check;
            // the unique index rejects the second one. Report it like any other taken login.
            context.Entry(newUser).State = EntityState.Detached;

            throw new DomainValidationException($"User with login '{newUser.Login}' already exists");
        }
    }

    private static bool IsLoginUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_users_Login"
        };
}
