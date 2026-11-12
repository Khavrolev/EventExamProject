using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Users.Application.Abstractions;
using Users.Domain.Entities;
using Users.Infrastructure.Persistence;

namespace Users.Infrastructure;

public static class DatabaseMigrationExtensions
{
    public static void ApplyMigrations(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        context.Database.Migrate();

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var adminUser = context.Users.FirstOrDefault(u => u.Login == "admin");
        if (adminUser == null)
        {
            context.Users.Add(User.Create("admin", passwordHasher.Hash("AdminPassword123!"), UserRole.Admin));
            context.SaveChanges();
        }
        else if (adminUser.Role != UserRole.Admin || !passwordHasher.Verify("AdminPassword123!", adminUser.PasswordHash))
        {
            adminUser.Role = UserRole.Admin;
            adminUser.PasswordHash = passwordHasher.Hash("AdminPassword123!");
            context.SaveChanges();
        }
    }
}
