using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Conduit.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Users;

public sealed class UsersHandling(
    ConduitContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    ConduitMapper mapper
)
{
    public async Task<UserEnvelope> Register(
        RegisterUserData userData,
        CancellationToken cancellationToken
    )
    {
        if (
            await context
                .Persons.Where(x => x.Username == userData.Username)
                .AnyAsync(cancellationToken)
        )
        {
            throw new RestException(HttpStatusCode.Conflict, "username", Constants.IN_USE);
        }

        if (await context.Persons.Where(x => x.Email == userData.Email).AnyAsync(cancellationToken))
        {
            throw new RestException(HttpStatusCode.Conflict, "email", Constants.IN_USE);
        }

        var person = new Person
        {
            Username = userData.Username,
            Email = userData.Email,
            Hash = await passwordHasher.Hash(
                userData.Password ?? throw new InvalidOperationException(),
                []
            ),
            Salt = [],
        };

        await context.Persons.AddAsync(person, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var user = mapper.PersonToUser(person);
        user.Token = jwtTokenGenerator.CreateToken(person.PersonId);
        return new UserEnvelope(user);
    }

    public async Task<UserEnvelope> Login(
        Login.Command message,
        CancellationToken cancellationToken
    )
    {
        var person = await context
            .Persons.Where(x => x.Email == message.User.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (person == null)
        {
            throw new RestException(HttpStatusCode.Unauthorized, "credentials", "invalid");
        }

        var verification = passwordHasher.Verify(
            message.User.Password ?? throw new InvalidOperationException(),
            person.Hash,
            person.Salt
        );

        if (verification == PasswordVerificationResult.Failed)
        {
            throw new RestException(HttpStatusCode.Unauthorized, "credentials", "invalid");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            person.Hash = await passwordHasher.Hash(message.User.Password, []);
            person.Salt = [];
            await context.SaveChangesAsync(cancellationToken);
        }

        var user = mapper.PersonToUser(person);
        user.Token = jwtTokenGenerator.CreateToken(person.PersonId);
        return new UserEnvelope(user);
    }
}
