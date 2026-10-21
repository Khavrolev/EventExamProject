using EventExamProject.Domain.Entities;

namespace EventExamProject.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, string login, UserRole role);
}
