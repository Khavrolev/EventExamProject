using System.Security.Cryptography;
using System.Text;
using EventExamProject.Application.Abstractions;

namespace EventExamProject.Infrastructure.Security;

internal class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));

        return Convert.ToHexString(bytes);
    }

    public bool Verify(string password, string passwordHash)
    {
        var actual = Encoding.UTF8.GetBytes(Hash(password));
        var expected = Encoding.UTF8.GetBytes(passwordHash.ToUpperInvariant());

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
