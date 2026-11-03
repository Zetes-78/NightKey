using System;
using System.Collections.Generic;
using System.Text;

namespace NightKey.Core;

public sealed record MigrationResult(List<Account> Accounts, int Skipped);

/// <summary>
/// Reads Google Authenticator's "Transfer accounts" QR codes (otpauth-migration://offline?data=...).
/// The payload is a small protobuf; this is a minimal reader for just the fields we need.
/// </summary>
public static class MigrationImport
{
    private const string Prefix = "otpauth-migration://";

    public static bool IsMigrationUri(string text) =>
        text.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static MigrationResult Parse(string uri)
    {
        int q = uri.IndexOf('?');
        string? data = null;
        if (q >= 0)
        {
            foreach (string pair in uri[(q + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair[..eq].Equals("data", StringComparison.OrdinalIgnoreCase))
                    data = Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }
        if (string.IsNullOrEmpty(data))
            throw new FormatException("This transfer code doesn't contain any accounts.");

        string b64 = data.Trim().Replace(' ', '+').Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');

        byte[] payload;
        try { payload = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new FormatException("This transfer code is damaged. Try scanning it again."); }

        var accounts = new List<Account>();
        int skipped = 0;
        var reader = new ProtoReader(payload);
        while (!reader.End)
        {
            var (field, wire) = reader.ReadTag();
            if (field == 1 && wire == 2)
            {
                var account = ReadOtpParameters(reader.ReadBytes());
                if (account is null) skipped++;
                else accounts.Add(account);
            }
            else reader.Skip(wire);
        }
        return new MigrationResult(accounts, skipped);
    }

    private static Account? ReadOtpParameters(byte[] buffer)
    {
        byte[]? secret = null;
        string name = "", issuer = "";
        ulong algorithm = 0, digits = 0, type = 0;

        var r = new ProtoReader(buffer);
        while (!r.End)
        {
            var (field, wire) = r.ReadTag();
            switch (field)
            {
                case 1 when wire == 2: secret = r.ReadBytes(); break;
                case 2 when wire == 2: name = Encoding.UTF8.GetString(r.ReadBytes()); break;
                case 3 when wire == 2: issuer = Encoding.UTF8.GetString(r.ReadBytes()); break;
                case 4 when wire == 0: algorithm = r.ReadVarint(); break;
                case 5 when wire == 0: digits = r.ReadVarint(); break;
                case 6 when wire == 0: type = r.ReadVarint(); break;
                default: r.Skip(wire); break;
            }
        }

        // type: 1 = HOTP (unsupported), 2 = TOTP. algorithm 4 = MD5 (unsupported).
        if (secret is null || secret.Length == 0 || type == 1 || algorithm == 4) return null;

        if (!string.IsNullOrEmpty(issuer) && name.StartsWith(issuer + ":", StringComparison.Ordinal))
            name = name[(issuer.Length + 1)..].Trim();

        return new Account
        {
            Issuer = issuer.Trim(),
            Name = name.Trim(),
            Secret = Base32.Encode(secret),
            Algorithm = algorithm switch { 2 => OtpAlgorithm.Sha256, 3 => OtpAlgorithm.Sha512, _ => OtpAlgorithm.Sha1 },
            Digits = digits == 2 ? 8 : 6,
            Period = 30,
        };
    }

    private sealed class ProtoReader
    {
        private readonly byte[] _buf;
        private int _pos;
        public ProtoReader(byte[] buf) => _buf = buf;
        public bool End => _pos >= _buf.Length;

        public ulong ReadVarint()
        {
            ulong result = 0;
            for (int shift = 0; shift < 64; shift += 7)
            {
                if (_pos >= _buf.Length) throw new FormatException("This transfer code is damaged.");
                byte b = _buf[_pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
            }
            throw new FormatException("This transfer code is damaged.");
        }

        public (int Field, int Wire) ReadTag()
        {
            ulong tag = ReadVarint();
            return ((int)(tag >> 3), (int)(tag & 7));
        }

        public byte[] ReadBytes()
        {
            int len = checked((int)ReadVarint());
            if (len < 0 || _pos + len > _buf.Length) throw new FormatException("This transfer code is damaged.");
            byte[] result = _buf.AsSpan(_pos, len).ToArray();
            _pos += len;
            return result;
        }

        public void Skip(int wire)
        {
            switch (wire)
            {
                case 0: ReadVarint(); break;
                case 1: _pos += 8; break;
                case 2: ReadBytes(); break;
                case 5: _pos += 4; break;
                default: throw new FormatException("This transfer code is damaged.");
            }
        }
    }
}
