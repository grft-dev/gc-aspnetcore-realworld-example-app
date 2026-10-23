using Conduit.Graftcode;
using Conduit.Security;

namespace Conduit.Services;

public static class UserService
{
    public static string Register(string username, string email, string password) =>
        GraftAsync.Run(() => RegisterAsync(username, email, password));

    public static string Login(string email, string password) =>
        GraftAsync.Run(() => LoginAsync(email, password));

    public static string GetCurrentUser(string token) =>
        GraftAsync.Run(() => GetCurrentUserAsync(token));

    public static string UpdateUser(
        string token,
        string username,
        string email,
        string password,
        string bio,
        string image
    ) => GraftAsync.Run(() => UpdateUserAsync(token, username, email, password, bio, image));

    private static async Task<string> RegisterAsync(string username, string email, string password)
    {
        var name = Rules.Required(username, "username");
        Rules.MaxLength(name, 256, "username");
        var address = Rules.Required(email, "email");
        Rules.MaxLength(address, 320, "email");
        Rules.Password(password);

        var repository = ConduitGate.Repository();
        if (await repository.UsernameTakenAsync(name, null).ConfigureAwait(false))
        {
            throw new InvalidOperationException("username has already been taken");
        }

        if (await repository.EmailTakenAsync(address, null).ConfigureAwait(false))
        {
            throw new InvalidOperationException("email has already been taken");
        }

        var hash = PasswordHasher.Hash(password, out var salt);
        var personId = await repository
            .InsertPersonAsync(name, address, hash, salt)
            .ConfigureAwait(false);
        var person =
            await repository.GetPersonByIdAsync(personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("user not found");
        return RecordFormat.User(person, TokenIssuer.Create(person.PersonId));
    }

    private static async Task<string> LoginAsync(string email, string password)
    {
        var address = Rules.Required(email, "email");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("password can't be blank");
        }

        var repository = ConduitGate.Repository();
        var person = await repository.GetPersonByEmailAsync(address).ConfigureAwait(false);
        if (
            person == null
            || !PasswordHasher.Verify(password, person.PasswordHash, person.PasswordSalt)
        )
        {
            throw new InvalidOperationException("credentials invalid");
        }

        return RecordFormat.User(person, TokenIssuer.Create(person.PersonId));
    }

    private static async Task<string> GetCurrentUserAsync(string token)
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var person =
            await ConduitGate.Repository().GetPersonByIdAsync(personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("user not found");
        return RecordFormat.User(person, TokenIssuer.Create(person.PersonId));
    }

    private static async Task<string> UpdateUserAsync(
        string token,
        string username,
        string email,
        string password,
        string bio,
        string image
    )
    {
        var personId = TokenIssuer.RequirePersonId(token);
        var repository = ConduitGate.Repository();
        var person =
            await repository.GetPersonByIdAsync(personId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("user not found");

        var nextUsername = Rules.KeepOrReplace(username, "username", 256);
        if (
            nextUsername != null
            && !string.Equals(nextUsername, person.Username, StringComparison.Ordinal)
        )
        {
            if (
                await repository
                    .UsernameTakenAsync(nextUsername, person.PersonId)
                    .ConfigureAwait(false)
            )
            {
                throw new InvalidOperationException("username has already been taken");
            }

            person.Username = nextUsername;
        }

        var nextEmail = Rules.KeepOrReplace(email, "email", 320);
        if (nextEmail != null && !string.Equals(nextEmail, person.Email, StringComparison.Ordinal))
        {
            if (await repository.EmailTakenAsync(nextEmail, person.PersonId).ConfigureAwait(false))
            {
                throw new InvalidOperationException("email has already been taken");
            }

            person.Email = nextEmail;
        }

        var nextBio = Rules.KeepOrText(bio);
        if (nextBio != null)
        {
            person.Bio = nextBio;
        }

        var nextImage = Rules.KeepOrText(image);
        if (nextImage != null)
        {
            person.Image = nextImage;
        }

        if (!string.IsNullOrEmpty(password))
        {
            Rules.Password(password);
            person.PasswordHash = PasswordHasher.Hash(password, out var salt);
            person.PasswordSalt = salt;
        }

        await repository.UpdatePersonAsync(person).ConfigureAwait(false);
        return RecordFormat.User(person, TokenIssuer.Create(person.PersonId));
    }
}
