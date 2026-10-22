using EventExamProject.Domain.Entities;

namespace EventExamProject.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);
    Task AddAsync(User newUser);
}
