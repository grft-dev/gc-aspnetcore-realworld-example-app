using graft.nuget.Conduit.Graft;

GraftConfig.Host = "ws://localhost:8102/ws";

var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
var email = $"caller{suffix}@example.com";
var username = $"caller{suffix}";
const string password = "Password1";
var title = $"Caller article {suffix}";

await UsersService.Register(username, email, password);
var token = await UsersService.Login(email, password);
if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException("Login did not return a token.");
}

var slug = await ArticlesService.Create(token, title, "From the graft caller", "Body");
var read = await ArticlesService.Get(token, slug);
Console.WriteLine($"{slug} {read}");
if (!string.Equals(read, title, StringComparison.Ordinal))
{
    throw new InvalidOperationException($"Article title was '{read}', expected '{title}'.");
}
