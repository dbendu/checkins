using System.Security.Cryptography;
using System.Text;

namespace Logic;

public static class PasswordHasher
{
    private const int SaltBytes = 16;

    public static (string Hash, string Salt) Hash(string password)
    {
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(SaltBytes));

        return (Compute(password, salt), salt);
    }

    public static bool Verify(string password, string hash, string salt) =>
        Compute(password, salt) == hash;

    private static string Compute(string password, string salt) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
}
