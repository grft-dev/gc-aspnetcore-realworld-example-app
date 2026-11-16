using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Infrastructure.Security;

public static class JwtPersonIdentity
{
    public static bool TryGetPersonId(ClaimsPrincipal? principal, out int personId)
    {
        var identity = principal?.Identity as ClaimsIdentity;
        return TryGetPersonId(identity, out personId);
    }

    public static bool TryGetPersonId(ClaimsIdentity? identity, out int personId)
    {
        personId = 0;

        var subject = identity?.FindFirst("sub");
        if (
            subject is null
            || identity!.FindAll("sub").Count() != 1
            || identity.FindAll("conduit_token_version").Count() != 1
            || identity.FindFirst("conduit_token_version")?.Value != "2"
            || !subject.Value.StartsWith("user:", StringComparison.Ordinal)
            || !int.TryParse(subject.Value[5..], NumberStyles.None, CultureInfo.InvariantCulture, out personId)
            || personId <= 0
        )
        {
            personId = 0;
            return false;
        }

        return true;
    }

    public static Task<bool> PersonExistsAsync(
        ConduitContext database,
        int personId,
        CancellationToken cancellationToken = default
    ) => database.Persons.AnyAsync(person => person.PersonId == personId, cancellationToken);
}
