using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Conduit.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Users;

public sealed class UserHandling(
    ConduitContext context,
    IPasswordHasher passwordHasher,
    ICurrentUserAccessor currentUserAccessor,
    IJwtTokenGenerator jwtTokenGenerator,
    ConduitMapper mapper
)
{
    public async Task<User> Get(Details.Query message, CancellationToken cancellationToken)
    {
        var person = await context
            .Persons.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PersonId == message.PersonId, cancellationToken);

        if (person == null)
        {
            throw new RestException(HttpStatusCode.NotFound, "user", Constants.NOT_FOUND);
        }

        var user = mapper.PersonToUser(person);
        user.Token = jwtTokenGenerator.CreateToken(person.PersonId);
        return user;
    }

    public async Task<User> Update(EditUserData userData, CancellationToken cancellationToken)
    {
        var currentPersonId = currentUserAccessor.GetCurrentPersonId();
        var person = await context
            .Persons.Where(x => x.PersonId == currentPersonId)
            .FirstOrDefaultAsync(cancellationToken);
        if (person is null)
        {
            throw new RestException(HttpStatusCode.NotFound, "user", Constants.NOT_FOUND);
        }

        if (userData.UsernameSet && userData.Username != person.Username)
        {
            if (
                await context
                    .Persons.Where(x => x.Username == userData.Username)
                    .AnyAsync(cancellationToken)
            )
            {
                throw new RestException(HttpStatusCode.Conflict, "username", Constants.IN_USE);
            }

            person.Username = userData.Username;
        }

        if (userData.EmailSet && userData.Email != person.Email)
        {
            if (
                await context
                    .Persons.Where(x => x.Email == userData.Email)
                    .AnyAsync(cancellationToken)
            )
            {
                throw new RestException(HttpStatusCode.Conflict, "email", Constants.IN_USE);
            }

            person.Email = userData.Email;
        }

        if (userData.BioSet)
        {
            person.Bio = string.IsNullOrEmpty(userData.Bio) ? null : userData.Bio;
        }

        if (userData.ImageSet)
        {
            person.Image = string.IsNullOrEmpty(userData.Image) ? null : userData.Image;
        }

        if (userData.PasswordSet && !string.IsNullOrWhiteSpace(userData.Password))
        {
            person.Hash = await passwordHasher.Hash(userData.Password, []);
            person.Salt = [];
        }

        await context.SaveChangesAsync(cancellationToken);

        var user = mapper.PersonToUser(person);
        user.Token = jwtTokenGenerator.CreateToken(person.PersonId);
        return user;
    }
}
