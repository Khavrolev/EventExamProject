using Users.Domain.Entities;

namespace Users.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string login, UserRole role);
}
