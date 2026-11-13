namespace Conduit.Features.Users;

public class Create
{
    public record UserData(string? Username, string? Email, string? Password);
}
