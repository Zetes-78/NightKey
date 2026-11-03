using System;
using System.Collections.Generic;
using System.Text;

namespace NightKey.Core;

/// <summary>RFC 4648 Base32, the encoding sites use for authenticator secret keys.</summary>
public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Uppercases and strips spaces, dashes and padding so keys typed in groups still work.</summary>
    public static string Normalize(string input)
    {
        var sb = new StringBuilder(input?.Length ?? 0);
        foreach (char c in input ?? "")
        {
            if (char.IsWhiteSpace(c) || c == '-' || c == '=') continue;
            sb.Append(char.ToUpperInvariant(c));
        }
        string clean = sb.ToString();
        Decode(clean); // throws a readable FormatException if invalid
        return clean;
    }

    public static byte[] Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Enter the secret key from the site.");

        var output = new List<byte>(input.Length * 5 / 8);
        int buffer = 0, bits = 0;
        foreach (char raw in input)
        {
            if (char.IsWhiteSpace(raw) || raw == '-' || raw == '=') continue;
            char c = char.ToUpperInvariant(raw);
            int value = Alphabet.IndexOf(c);
            if (value < 0)
                throw new FormatException($"\"{raw}\" can't appear in a secret key. Keys use the letters A to Z and the digits 2 to 7.");
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xFF));
                buffer &= (1 << bits) - 1;
            }
        }
        if (output.Count == 0)
            throw new FormatException("That secret key is too short.");
        return output.ToArray();
    }

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bits = 0;
        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                sb.Append(Alphabet[(buffer >> bits) & 31]);
            }
            buffer &= (1 << bits) - 1;
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }
}
