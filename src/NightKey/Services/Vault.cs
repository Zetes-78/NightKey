using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NightKey.Core;

namespace NightKey.Services;

/// <summary>
/// The on-disk account store, encrypted with Windows DPAPI for the current user.
/// Only this Windows account on this PC can decrypt it. Use Backup for moving between PCs.
/// </summary>
public static class Vault
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Night Tech Solutions/NightKey/vault/v1");

    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Night Tech Solutions", "NightKey");

    private static string VaultPath => Path.Combine(Folder, "vault.dat");

    public static List<Account> Load()
    {
        if (!File.Exists(VaultPath)) return new List<Account>();
        byte[] protectedBytes = File.ReadAllBytes(VaultPath);
        byte[] plain = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
        try
        {
            return JsonSerializer.Deserialize<List<Account>>(plain, JsonDefaults.Options) ?? new List<Account>();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public static void Save(IEnumerable<Account> accounts)
    {
        Directory.CreateDirectory(Folder);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(accounts.ToList(), JsonDefaults.Options);
        byte[] protectedBytes;
        try
        {
            protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        // Write-then-swap so a crash mid-save never leaves a half-written vault.
        // The previous vault is kept as vault.dat.bak.
        string temp = VaultPath + ".tmp";
        File.WriteAllBytes(temp, protectedBytes);
        if (File.Exists(VaultPath)) File.Replace(temp, VaultPath, VaultPath + ".bak");
        else File.Move(temp, VaultPath);
    }
}
