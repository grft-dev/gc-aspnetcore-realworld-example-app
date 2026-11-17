namespace Conduit;

internal static class ConduitConfig
{
    public static string ConnectionString() => Required("CONDUIT_CN");

    public static string TokenSecret() => Required("CONDUIT_TOKEN_SECRET");

    private static string Required(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing {key} environment variable.");
        }

        return value;
    }
}
