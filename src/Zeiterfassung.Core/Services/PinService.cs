using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace Zeiterfassung.Core.Services;

public class PinService
{
    public const int SaltLength = 16;
    public const int TimeCost = 3;
    public const int MemoryCost = 65536;
    public const int Parallelism = 4;

    public (string Hash, string Salt) HashPin(string pin)
    {
        var salt = new byte[SaltLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        var hash = HashPinWithSalt(pin, salt);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool VerifyPin(string pin, string storedHash, string storedSalt)
    {
        try
        {
            var saltBytes = Convert.FromBase64String(storedSalt);
            var computedHash = HashPinWithSalt(pin, saltBytes);
            var hashBytes = Convert.FromBase64String(storedHash);

            return TimingSafeCompare(hashBytes, computedHash);
        }
        catch
        {
            return false;
        }
    }

    private byte[] HashPinWithSalt(string pin, byte[] salt)
    {
        var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(pin))
        {
            Salt = salt,
            DegreeOfParallelism = Parallelism,
            Iterations = TimeCost,
            MemorySize = MemoryCost
        };
        return argon2.GetBytes(32);
    }

    private bool TimingSafeCompare(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }
        return result == 0;
    }

    public string GenerateRandomPin()
    {
        var bytes = new byte[3];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        var pin = BitConverter.ToUInt32(bytes.Concat(new byte[] { 0 }).ToArray(), 0) % 1000000;
        return pin.ToString("D6");
    }
}
