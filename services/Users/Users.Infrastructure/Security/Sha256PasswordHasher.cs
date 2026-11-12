using System.Security.Cryptography;
using System.Text;
using Users.Application.Abstractions;

namespace Users.Infrastructure.Security;

internal class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool Verify(string password, string passwordHash)
    {
        return string.Equals(Hash(password), passwordHash, StringComparison.OrdinalIgnoreCase);
    }
}
