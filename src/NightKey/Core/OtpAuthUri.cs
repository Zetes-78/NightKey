using System;
using System.Collections.Generic;

namespace NightKey.Core;

/// <summary>Parses otpauth://totp/... setup links (the text inside a site's QR code).</summary>
public static class OtpAuthUri
{
    private const string Prefix = "otpauth://";

    public static bool IsSetupUri(string text) =>
        text.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static Account Parse(string uri)
    {
        uri = uri.Trim();
        if (!IsSetupUri(uri))
            throw new FormatException("That isn't an authenticator setup code. Setup links start with otpauth://");

        string rest = uri[Prefix.Length..];
        int slash = rest.IndexOf('/');
        if (slash < 0) throw new FormatException("The setup code is missing its account name.");

        string type = rest[..slash].ToLowerInvariant();
        if (type == "hotp")
            throw new NotSupportedException("This is a counter-based (HOTP) code. NightKey supports time-based codes, which almost every site uses.");
        if (type != "totp")
            throw new FormatException($"NightKey doesn't recognize the code type \"{type}\".");

        string afterType = rest[(slash + 1)..];
        string labelPart = afterType, query = "";
        int q = afterType.IndexOf('?');
        if (q >= 0) { labelPart = afterType[..q]; query = afterType[(q + 1)..]; }

        string label = Uri.UnescapeDataString(labelPart);
        string? labelIssuer = null;
        string name = label.Trim();
        int colon = label.IndexOf(':');
        if (colon >= 0)
        {
            labelIssuer = label[..colon].Trim();
            name = label[(colon + 1)..].Trim();
        }

        var p = ParseQuery(query);
        if (!p.TryGetValue("secret", out var secret) || string.IsNullOrWhiteSpace(secret))
            throw new FormatException("The setup code doesn't include a secret key.");

        var account = new Account
        {
            Issuer = p.TryGetValue("issuer", out var issuer) && !string.IsNullOrWhiteSpace(issuer)
                ? issuer.Trim() : labelIssuer ?? "",
            Name = name,
            Secret = Base32.Normalize(secret),
        };

        if (p.TryGetValue("algorithm", out var alg)) account.Algorithm = ParseAlgorithm(alg);
        if (p.TryGetValue("digits", out var d))
            account.Digits = int.TryParse(d, out var digits) ? digits : throw new FormatException("The setup code has an invalid digit count.");
        if (p.TryGetValue("period", out var per))
            account.Period = int.TryParse(per, out var period) ? period : throw new FormatException("The setup code has an invalid period.");

        account.Validate();
        return account;
    }

    public static OtpAlgorithm ParseAlgorithm(string value) =>
        value.Replace("-", "").Trim().ToUpperInvariant() switch
        {
            "SHA1" => OtpAlgorithm.Sha1,
            "SHA256" => OtpAlgorithm.Sha256,
            "SHA512" => OtpAlgorithm.Sha512,
            _ => throw new FormatException($"NightKey doesn't support the \"{value}\" algorithm."),
        };

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
            string value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
            result.TryAdd(key, value);
        }
        return result;
    }
}
