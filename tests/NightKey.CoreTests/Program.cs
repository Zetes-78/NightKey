using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NightKey.Core;

int failures = 0, passes = 0;
void Check(bool ok, string name)
{
    if (ok) passes++; else { failures++; Console.WriteLine("FAIL: " + name); }
}
void Throws<T>(Action a, string name) where T : Exception
{
    try { a(); Check(false, name + " (no exception)"); }
    catch (T) { Check(true, name); }
    catch (Exception ex) { Check(false, $"{name} (threw {ex.GetType().Name})"); }
}

// RFC 6238 Appendix B test vectors (8 digits).
var sha1Key = Encoding.ASCII.GetBytes("12345678901234567890");
var sha256Key = Encoding.ASCII.GetBytes("12345678901234567890123456789012");
var sha512Key = Encoding.ASCII.GetBytes("1234567890123456789012345678901234567890123456789012345678901234");
long[] times = { 59, 1111111109, 1111111111, 1234567890, 2000000000, 20000000000 };
string[] e1 = { "94287082", "07081804", "14050471", "89005924", "69279037", "65353130" };
string[] e256 = { "46119246", "68084774", "67062674", "91819424", "90698825", "77737706" };
string[] e512 = { "90693936", "25091201", "99943326", "93441116", "38618901", "47863826" };
for (int i = 0; i < times.Length; i++)
{
    var t = DateTimeOffset.FromUnixTimeSeconds(times[i]);
    Check(Totp.Compute(sha1Key, t, 30, 8, OtpAlgorithm.Sha1) == e1[i], $"RFC6238 SHA1 t={times[i]}");
    Check(Totp.Compute(sha256Key, t, 30, 8, OtpAlgorithm.Sha256) == e256[i], $"RFC6238 SHA256 t={times[i]}");
    Check(Totp.Compute(sha512Key, t, 30, 8, OtpAlgorithm.Sha512) == e512[i], $"RFC6238 SHA512 t={times[i]}");
}

// RFC 4226 Appendix D (HOTP, 6 digits).
string[] hotp = { "755224", "287082", "359152", "969429", "338314", "254676", "287922", "162583", "399871", "520489" };
for (int c = 0; c < hotp.Length; c++)
    Check(Totp.Hotp(sha1Key, c, 6, OtpAlgorithm.Sha1) == hotp[c], $"RFC4226 counter={c}");

// Base32 round trip + RFC 4648 vectors.
Check(Base32.Encode(Encoding.ASCII.GetBytes("foobar")) == "MZXW6YTBOI", "base32 encode foobar");
Check(Encoding.ASCII.GetString(Base32.Decode("MZXW6YTBOI======")) == "foobar", "base32 decode foobar");
Check(Base32.Normalize("jbsw y3dp-ehpk 3pxp") == "JBSWY3DPEHPK3PXP", "base32 normalize grouped key");
Throws<FormatException>(() => Base32.Decode("ABC1"), "base32 rejects 1");
Throws<FormatException>(() => Base32.Decode("   "), "base32 rejects empty");
var rnd = new Random(7);
for (int n = 1; n < 70; n++)
{
    var bytes = new byte[n]; rnd.NextBytes(bytes);
    Check(Convert.ToHexString(Base32.Decode(Base32.Encode(bytes))) == Convert.ToHexString(bytes), $"base32 roundtrip len={n}");
}

// otpauth:// parsing.
var a = OtpAuthUri.Parse("otpauth://totp/ACME%20Co:john.doe@email.com?secret=HXDMVJECJJWSRB3HWIZR4IFUGFTMXBOZ&issuer=ACME%20Co&algorithm=SHA1&digits=6&period=30");
Check(a.Issuer == "ACME Co" && a.Name == "john.doe@email.com" && a.Digits == 6 && a.Period == 30, "uri: Google example");
var b = OtpAuthUri.Parse("otpauth://totp/GitHub:zetes?secret=jbswy3dpehpk3pxp");
Check(b.Issuer == "GitHub" && b.Name == "zetes" && b.Secret == "JBSWY3DPEHPK3PXP", "uri: issuer from label, lowercase secret");
var c2 = OtpAuthUri.Parse("otpauth://totp/jason+test@nighttech.solutions?secret=JBSWY3DPEHPK3PXP&algorithm=SHA256&digits=8&period=60");
Check(c2.Issuer == "" && c2.Name == "jason+test@nighttech.solutions" && c2.Algorithm == OtpAlgorithm.Sha256 && c2.Digits == 8 && c2.Period == 60, "uri: no issuer, plus sign, sha256/8/60");
Throws<NotSupportedException>(() => OtpAuthUri.Parse("otpauth://hotp/x?secret=JBSWY3DPEHPK3PXP&counter=0"), "uri: rejects hotp");
Throws<FormatException>(() => OtpAuthUri.Parse("otpauth://totp/x?issuer=y"), "uri: requires secret");
Throws<FormatException>(() => OtpAuthUri.Parse("https://example.com"), "uri: rejects non-otpauth");
Throws<FormatException>(() => OtpAuthUri.Parse("otpauth://totp/x?secret=JBSWY3DPEHPK3PXP&digits=9"), "uri: rejects 9 digits");

// Google Authenticator migration payload (built by hand to match its protobuf layout).
byte[] Varint(ulong v) { var l = new List<byte>(); do { byte x = (byte)(v & 0x7F); v >>= 7; if (v != 0) x |= 0x80; l.Add(x); } while (v != 0); return l.ToArray(); }
byte[] Field(int f, int wire, byte[] body)
{
    var l = new List<byte>(Varint((ulong)((f << 3) | wire)));
    if (wire == 2) l.AddRange(Varint((ulong)body.Length));
    l.AddRange(body); return l.ToArray();
}
byte[] Otp(byte[] secret, string name, string issuer, ulong alg, ulong digits, ulong type)
{
    var l = new List<byte>();
    l.AddRange(Field(1, 2, secret)); l.AddRange(Field(2, 2, Encoding.UTF8.GetBytes(name)));
    l.AddRange(Field(3, 2, Encoding.UTF8.GetBytes(issuer))); l.AddRange(Field(4, 0, Varint(alg)));
    l.AddRange(Field(5, 0, Varint(digits))); l.AddRange(Field(6, 0, Varint(type)));
    return l.ToArray();
}
var payload = new List<byte>();
payload.AddRange(Field(1, 2, Otp(Encoding.ASCII.GetBytes("Hello!\xDE\xAD\xBE\xEF"), "Square:jason@nighttech.solutions", "Square", 1, 1, 2)));
payload.AddRange(Field(1, 2, Otp(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, "counter-acct", "Old", 1, 1, 1))); // HOTP -> skipped
payload.AddRange(Field(2, 0, Varint(1))); payload.AddRange(Field(3, 0, Varint(1)));
string migration = "otpauth-migration://offline?data=" + Uri.EscapeDataString(Convert.ToBase64String(payload.ToArray()));
var mig = MigrationImport.Parse(migration);
Check(mig.Accounts.Count == 1 && mig.Skipped == 1, "migration: one TOTP imported, HOTP skipped");
Check(mig.Accounts[0].Issuer == "Square" && mig.Accounts[0].Name == "jason@nighttech.solutions", "migration: issuer prefix stripped");
Check(mig.Accounts[0].Secret == Base32.Encode(Encoding.ASCII.GetBytes("Hello!\xDE\xAD\xBE\xEF")), "migration: secret preserved");
Throws<FormatException>(() => MigrationImport.Parse("otpauth-migration://offline?data=%%%"), "migration: rejects garbage");

// Backup round trip.
var list = new List<Account> { a, b, c2 };
string file = Backup.Export(list, "correct horse battery");
var restored = Backup.Import(file, "correct horse battery");
Check(restored.Count == 3 && restored[2].Algorithm == OtpAlgorithm.Sha256 && restored[0].SameSecretAs(a), "backup: round trip");
Check(!file.Contains("JBSWY3DPEHPK3PXP") && !file.Contains("john.doe"), "backup: no plaintext in file");
Throws<InvalidDataException>(() => Backup.Import(file, "wrong password"), "backup: wrong password");
Throws<InvalidDataException>(() => Backup.Import(file.Replace("\"data\": \"", "\"data\": \"AA"), "correct horse battery"), "backup: tamper detected");
Throws<InvalidDataException>(() => Backup.Import("{\"hello\":1}", "x"), "backup: rejects other json");
{
    // Flip one character inside valid base64 ciphertext: must fail authentication, not decrypt garbage.
    int i = file.IndexOf("\"data\": \"") + 10;
    char ch = file[i] == 'A' ? 'B' : 'A';
    string flipped = file[..i] + ch + file[(i + 1)..];
    Throws<InvalidDataException>(() => Backup.Import(flipped, "correct horse battery"), "backup: bit flip detected");
}

Console.WriteLine($"{passes} passed, {failures} failed");
return failures == 0 ? 0 : 1;
