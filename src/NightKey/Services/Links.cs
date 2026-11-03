using System;
using System.Diagnostics;

namespace NightKey.Services;

/// <summary>Night Tech Solutions' web pages and contact details, opened in the person's own browser.</summary>
public static class Links
{
    public const string Company = "Night Tech Solutions";
    public const string Website = "https://nighttech.solutions/";
    public const string FreeApps = "https://nighttech.solutions/security-apps/";
    public const string Help = "https://nighttech.solutions/security-apps/#nightkey";
    public const string Privacy = "https://nighttech.solutions/privacy/#apps";
    public const string Contact = "https://nighttech.solutions/contact/";
    public const string Source = "https://github.com/Zetes-78/NightKey";
    public const string Releases = "https://github.com/Zetes-78/NightKey/releases/latest";
    public const string Phone = "(864) 381-8510";
    public const string PhoneLink = "tel:+18643818510";

    /// <summary>True in the Microsoft Store build: the Store handles updates, so the app doesn't offer its own.</summary>
#if STORE
    public const bool StoreBuild = true;
#else
    public const bool StoreBuild = false;
#endif

    /// <summary>Opens one of the links above. Only these fixed addresses are ever opened.</summary>
    public static void Open(string url)
    {
        if (!url.StartsWith("https://nighttech.solutions/", StringComparison.Ordinal) &&
            !url.StartsWith("https://github.com/Zetes-78/", StringComparison.Ordinal) &&
            url != PhoneLink) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception) { /* no browser or phone app registered: nothing to do */ }
    }
}
