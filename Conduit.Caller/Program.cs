using graft.nuget.Conduit.Graft;

var host = Environment.GetEnvironmentVariable("GRAFT_HOST") ?? "ws://localhost:8102/ws";
GraftConfig.Host = host;

try
{
    var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    var email = $"caller{suffix}@example.com";
    var username = $"caller{suffix}";
    const string password = "Password1";
    var title = $"Caller article {suffix}";
    var description = $"Caller description {suffix}";
    var body = $"Caller body {suffix}";

    Console.WriteLine($"Caller E2E start (host: {host})");
    Console.WriteLine($"Registering user '{username}'...");
    var registerToken = await UsersService.Register(username, email, password);
    if (string.IsNullOrWhiteSpace(registerToken))
    {
        throw new InvalidOperationException("Register did not return a token.");
    }

    Console.WriteLine($"Logging in user '{email}'...");
    var loginToken = await UsersService.Login(email, password);
    if (string.IsNullOrWhiteSpace(loginToken))
    {
        throw new InvalidOperationException("Login did not return a token.");
    }

    Console.WriteLine("Creating article...");
    var slug = await ArticlesService.Create(loginToken, title, description, body);
    if (string.IsNullOrWhiteSpace(slug))
    {
        throw new InvalidOperationException("Create did not return a slug.");
    }

    Console.WriteLine($"Reading article '{slug}'...");
    var readTitle = await ArticlesService.Get(loginToken, slug);
    if (!string.Equals(readTitle, title, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Article title was '{readTitle}', expected '{title}'."
        );
    }

    Console.WriteLine("Caller E2E smoke passed.");
}
catch (Exception ex)
{
    Console.Error.WriteLine("Caller E2E smoke failed.");
    Console.Error.WriteLine(ex.ToString());
    Environment.ExitCode = 1;
}
