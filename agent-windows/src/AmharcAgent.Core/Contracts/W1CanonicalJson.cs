using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace AmharcAgent.Core.Contracts;

/// <summary>W1 1.0.0 byte contract. Ordinary serializer output is not a substitute.</summary>
public static class W1CanonicalJson
{
    public static byte[] Canonicalize(string raw, string? excludedTopLevelMember = null)
    {
        using var doc = JsonDocument.Parse(raw);
        // Validate the entire original tree, including excluded members: duplicate
        // names and invalid Unicode cannot be hidden in an attestation.
        _ = Write(doc.RootElement, null);
        return new UTF8Encoding(false, true).GetBytes(Write(doc.RootElement, excludedTopLevelMember));
    }

    public static string Digest(string raw, string? excludedTopLevelMember = null) =>
        Convert.ToHexString(SHA256.HashData(Canonicalize(raw, excludedTopLevelMember))).ToLowerInvariant();

    private static string Quote(string value)
    {
        var b = new StringBuilder("\"");
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                    throw new FormatException("W1_INVALID_UNICODE");
                b.Append(c).Append(value[++i]);
                continue;
            }
            if (char.IsLowSurrogate(c)) throw new FormatException("W1_INVALID_UNICODE");
            b.Append(c switch {
                '"' => "\\\"", '\\' => "\\\\", '\b' => "\\b", '\t' => "\\t",
                '\n' => "\\n", '\f' => "\\f", '\r' => "\\r",
                _ => c < 32 ? "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture) : c.ToString()
            });
        }
        return b.Append('"').ToString();
    }

    private static string Number(string raw)
    {
        var m = Regex.Match(raw, @"^(0|[1-9][0-9]*)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?$");
        if (!m.Success) throw new FormatException("W1_INVALID_NUMBER");
        var digits = (m.Groups[1].Value + m.Groups[2].Value).TrimStart('0');
        if (digits.Length == 0) return "0";
        if (!int.TryParse(m.Groups[3].Success ? m.Groups[3].Value : "0",
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var exponent))
            throw new FormatException("W1_NUMBER_OVERFLOW");
        long power = (long)exponent - m.Groups[2].Value.Length;
        while (power < 0 && digits.EndsWith('0'))
        {
            digits = digits[..^1];
            power++;
        }
        if (power < 0 || power > 10 || digits.Length + power > 10)
            throw new FormatException("W1_INVALID_NUMBER");
        var n = BigInteger.Parse(digits, CultureInfo.InvariantCulture) * BigInteger.Pow(10, (int)power);
        if (n > int.MaxValue) throw new FormatException("W1_NUMBER_OVERFLOW");
        return n.ToString(CultureInfo.InvariantCulture);
    }

    private static string Write(JsonElement v, string? excluded)
    {
        switch (v.ValueKind)
        {
            case JsonValueKind.Object:
                var fields = v.EnumerateObject().ToArray();
                if (fields.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length)
                    throw new FormatException("W1_DUPLICATE_KEYS");
                return "{" + string.Join(",", fields.Where(x => x.Name != excluded)
                    .OrderBy(x => x.Name, StringComparer.Ordinal)
                    .Select(x => Quote(x.Name) + ":" + Write(x.Value, null))) + "}";
            case JsonValueKind.Array:
                return "[" + string.Join(",", v.EnumerateArray().Select(x => Write(x, null))) + "]";
            case JsonValueKind.String: return Quote(v.GetString()!);
            case JsonValueKind.Number: return Number(v.GetRawText());
            case JsonValueKind.True: return "true";
            case JsonValueKind.False: return "false";
            case JsonValueKind.Null: return "null";
            default: throw new FormatException("W1_UNSUPPORTED_JSON");
        }
    }

    private static byte[] SignedBytes(string digest)
    {
        if (!Regex.IsMatch(digest, "^[0-9a-f]{64}$")) throw new FormatException("W1_DIGEST_ENCODING");
        return Encoding.ASCII.GetBytes(digest);
    }

    public static string Sign(string digest, byte[] privateSeed)
    {
        if (privateSeed.Length != 32) throw new ArgumentException("W1_ED25519_SEED_LENGTH");
        var message = SignedBytes(digest);
        var signer = new Ed25519Signer();
        signer.Init(true, new Ed25519PrivateKeyParameters(privateSeed, 0));
        signer.BlockUpdate(message, 0, message.Length);
        return Convert.ToBase64String(signer.GenerateSignature());
    }

    public static bool Verify(string digest, string publicKeyPem, string signatureBase64)
    {
        try
        {
            var der = Convert.FromBase64String(publicKeyPem
                .Replace("-----BEGIN PUBLIC KEY-----", "").Replace("-----END PUBLIC KEY-----", ""));
            var key = PublicKeyFactory.CreateKey(der) as Ed25519PublicKeyParameters;
            if (key is null) return false;
            var message = SignedBytes(digest);
            var signature = Convert.FromBase64String(signatureBase64);
            if (signature.Length != 64 || Convert.ToBase64String(signature) != signatureBase64) return false;
            var signer = new Ed25519Signer();
            signer.Init(false, key);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.VerifySignature(signature);
        }
        catch (Exception e) when (e is FormatException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}