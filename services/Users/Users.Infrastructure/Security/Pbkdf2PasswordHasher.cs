using System.Security.Cryptography;
using System.Text;
using Users.Application.Abstractions;

namespace Users.Infrastructure.Security;

internal class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        if (passwordHash.Contains(':'))
        {
            var parts = passwordHash.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[0]);
            var expectedHash = Convert.FromBase64String(parts[1]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }

        var legacyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var legacyHex = Convert.ToHexString(legacyBytes).ToLowerInvariant();
        return string.Equals(legacyHex, passwordHash, StringComparison.OrdinalIgnoreCase);
    }
}
