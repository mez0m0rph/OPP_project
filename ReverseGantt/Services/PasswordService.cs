using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace ReverseGantt.Services;

public class PasswordService
{
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var subkey = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100000, 32);
        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 2);
        if (parts.Length != 2) return false;
        var salt = Convert.FromBase64String(parts[0]);
        var expected = Convert.FromBase64String(parts[1]);
        var actual = KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, 100000, 32);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
