using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Conduit.Security;

internal static class TokenIssuer
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static string Create(int personId)
    {
        var expiry = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        var payload = string.Create(CultureInfo.InvariantCulture, $"{personId}.{expiry}");
        var signature = Sign(payload);
        return payload + "." + Convert.ToHexString(signature);
    }

    public static int RequirePersonId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("token is missing");
        }

        var parts = token.Split('.');
        if (
            parts.Length != 3
            || !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var personId
            )
            || personId <= 0
            || !long.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expiry
            )
        )
        {
            throw new InvalidOperationException("token is missing");
        }

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(parts[2]);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("token is missing");
        }

        var payload = parts[0] + "." + parts[1];
        var actual = Sign(payload);
        if (
            provided.Length != actual.Length
            || !CryptographicOperations.FixedTimeEquals(actual, provided)
        )
        {
            throw new InvalidOperationException("token is missing");
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
        {
            throw new InvalidOperationException("token is missing");
        }

        return personId;
    }

    public static int? ReadOptional(string? token) =>
        string.IsNullOrWhiteSpace(token) ? null : RequirePersonId(token);

    private static byte[] Sign(string payload)
    {
        var key = Encoding.UTF8.GetBytes(Conduit.ConduitConfig.TokenSecret());
        var data = Encoding.UTF8.GetBytes(payload);
        return HMACSHA256.HashData(key, data);
    }
}
