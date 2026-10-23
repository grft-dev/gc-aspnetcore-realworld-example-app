using System.Security.Cryptography;

namespace Conduit.Security;

internal static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static byte[] Hash(string password, out byte[] salt)
    {
        salt = RandomNumberGenerator.GetBytes(SaltSize);
        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize
        );
    }

    public static bool Verify(string password, byte[] hash, byte[] salt)
    {
        if (hash.Length == 0 || salt.Length == 0)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            hash.Length
        );
        return CryptographicOperations.FixedTimeEquals(actual, hash);
    }
}
