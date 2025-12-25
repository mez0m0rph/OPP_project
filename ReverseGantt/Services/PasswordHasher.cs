using System.Security.Cryptography;

namespace ReverseGantt.Services;

public static class PasswordHasher
{
    public static string Hash(string password, int iterations = 120_000)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);

        return $"pbkdf2${iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return false;

        var parts = stored.Split('$');
        if (parts.Length != 4) return false;
        if (parts[0] != "pbkdf2") return false;

        if (!int.TryParse(parts[1], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var actual = pbkdf2.GetBytes(expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
