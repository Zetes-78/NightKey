using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace NightKey.Core;

public enum OtpAlgorithm { Sha1, Sha256, Sha512 }

/// <summary>Time-based one-time passwords (RFC 6238) built on HOTP (RFC 4226).</summary>
public static class Totp
{
    public static string Compute(byte[] key, DateTimeOffset time, int period = 30, int digits = 6,
        OtpAlgorithm algorithm = OtpAlgorithm.Sha1)
        => Hotp(key, time.ToUnixTimeSeconds() / period, digits, algorithm);

    public static string Hotp(byte[] key, long counter, int digits, OtpAlgorithm algorithm)
    {
        if (digits is < 6 or > 8) throw new ArgumentOutOfRangeException(nameof(digits));

        byte[] message = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, counter);

        byte[] hash = algorithm switch
        {
            OtpAlgorithm.Sha256 => HMACSHA256.HashData(key, message),
            OtpAlgorithm.Sha512 => HMACSHA512.HashData(key, message),
            _ => HMACSHA1.HashData(key, message),
        };

        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24)
                   | (hash[offset + 1] << 16)
                   | (hash[offset + 2] << 8)
                   | hash[offset + 3];

        int modulus = digits switch { 6 => 1_000_000, 7 => 10_000_000, _ => 100_000_000 };
        CryptographicOperations.ZeroMemory(hash);
        return (binary % modulus).ToString().PadLeft(digits, '0');
    }
}
