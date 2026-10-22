using EventExamProject.Domain.Exceptions;

namespace EventExamProject.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string Login { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    private User()
    {
        Login = null!;
        PasswordHash = null!;
    }

    public static User Create(string login, string passwordHash, UserRole role = UserRole.User)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            throw new DomainValidationException("Login must not be empty");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainValidationException("Password hash must not be empty");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Login = login.Trim(),
            PasswordHash = passwordHash,
            Role = role
        };
    }
}
