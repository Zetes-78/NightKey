using System;
using System.Text.Json.Serialization;

namespace NightKey.Core;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Issuer { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Base32 secret, normalized (uppercase, no spaces).</summary>
    public string Secret { get; set; } = "";
    public OtpAlgorithm Algorithm { get; set; } = OtpAlgorithm.Sha1;
    public int Digits { get; set; } = 6;
    public int Period { get; set; } = 30;
    public DateTimeOffset Added { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore] public string DisplayTitle => string.IsNullOrWhiteSpace(Issuer) ? Name : Issuer;
    [JsonIgnore] public string DisplaySubtitle => string.IsNullOrWhiteSpace(Issuer) ? "" : Name;

    public void Validate()
    {
        Base32.Decode(Secret);
        if (Digits is < 6 or > 8)
            throw new FormatException("Codes must be 6, 7 or 8 digits long.");
        if (Period is < 1 or > 300)
            throw new FormatException("The code period must be between 1 and 300 seconds. Most sites use 30.");
    }

    /// <summary>Two entries are the same account if they share a secret.</summary>
    public bool SameSecretAs(Account other) =>
        string.Equals(Base32.Normalize(Secret), Base32.Normalize(other.Secret), StringComparison.Ordinal);
}
