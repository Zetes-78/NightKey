using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NightKey.Core;

/// <summary>
/// Password-protected backup files (.nkbackup): PBKDF2-SHA256 (600,000 rounds) derives a key,
/// AES-256-GCM encrypts the account list. Works on any PC, unlike the Windows-bound vault.
/// </summary>
public static class Backup
{
    public const int Iterations = 600_000;
    private const string FormatName = "nightkey-backup";
    private static readonly byte[] AssociatedData = Encoding.UTF8.GetBytes("nightkey-backup-v1");

    public static string Export(IReadOnlyList<Account> accounts, string password)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(accounts, JsonDefaults.Options);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] key = DeriveKey(password, salt, Iterations);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, cipher, tag, AssociatedData);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }

        var envelope = new Envelope
        {
            Format = FormatName,
            Version = 1,
            Kdf = "pbkdf2-sha256",
            Iterations = Iterations,
            Salt = Convert.ToBase64String(salt),
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag),
            Data = Convert.ToBase64String(cipher),
            Created = DateTimeOffset.UtcNow,
            Count = accounts.Count,
        };
        return JsonSerializer.Serialize(envelope, JsonDefaults.Indented);
    }

    public static List<Account> Import(string fileText, string password)
    {
        Envelope? env;
        try { env = JsonSerializer.Deserialize<Envelope>(fileText, JsonDefaults.Options); }
        catch (JsonException) { env = null; }
        if (env is null || env.Format != FormatName)
            throw new InvalidDataException("This isn't a NightKey backup file.");
        if (env.Version != 1 || env.Kdf != "pbkdf2-sha256" || env.Iterations < 100_000)
            throw new InvalidDataException("This backup was made by a newer version of NightKey. Update NightKey, then try again.");

        byte[] salt, nonce, tag, cipher;
        try
        {
            salt = Convert.FromBase64String(env.Salt);
            nonce = Convert.FromBase64String(env.Nonce);
            tag = Convert.FromBase64String(env.Tag);
            cipher = Convert.FromBase64String(env.Data);
        }
        catch (FormatException)
        {
            throw new InvalidDataException("This backup file is damaged and can't be read.");
        }
        if (nonce.Length != 12 || tag.Length != 16)
            throw new InvalidDataException("This backup file is damaged and can't be read.");
        byte[] plain = new byte[cipher.Length];
        byte[] key = DeriveKey(password, salt, env.Iterations);
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain, AssociatedData);
            var accounts = JsonSerializer.Deserialize<List<Account>>(plain, JsonDefaults.Options) ?? new List<Account>();
            foreach (var a in accounts) a.Validate();
            return accounts;
        }
        catch (CryptographicException)
        {
            throw new InvalidDataException("The password is wrong, or the backup file has been changed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static byte[] DeriveKey(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    private sealed class Envelope
    {
        public string Format { get; set; } = "";
        public int Version { get; set; }
        public string Kdf { get; set; } = "";
        public int Iterations { get; set; }
        public string Salt { get; set; } = "";
        public string Nonce { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Data { get; set; } = "";
        public DateTimeOffset Created { get; set; }
        public int Count { get; set; }
    }
}
