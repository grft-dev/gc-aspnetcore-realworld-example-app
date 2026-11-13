using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Security;
using Graftcode.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Conduit;

internal static class GraftUser
{
    public static IDisposable Begin(ServiceContext context, ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return Nop.Instance;
        }

        var accessor = context.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = principal };
        return new Reset(accessor);
    }

    public static async Task<ClaimsPrincipal?> Authenticate(ServiceContext context, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var options = context.GetRequiredService<IOptions<JwtIssuerOptions>>().Value;
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                RequireSignedTokens = true,
                RequireExpirationTime = true,
                IssuerSigningKey = options.SigningCredentials!.Key,
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            },
            out _
        );

        var identity = principal.Identity as ClaimsIdentity;
        var subject = identity?.FindFirst("sub");
        if (
            subject is null
            || identity!.FindAll("sub").Count() != 1
            || identity.FindAll("conduit_token_version").Count() != 1
            || identity.FindFirst("conduit_token_version")?.Value != "2"
            || !subject.Value.StartsWith("user:", StringComparison.Ordinal)
            || !int.TryParse(
                subject.Value[5..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var personId
            )
            || personId <= 0
        )
        {
            throw new SecurityTokenException("Invalid user identity");
        }

        var database = context.GetRequiredService<ConduitContext>();
        if (!await database.Persons.AnyAsync(person => person.PersonId == personId))
        {
            throw new SecurityTokenException("User no longer exists");
        }

        return principal;
    }

    private sealed class Reset(IHttpContextAccessor accessor) : IDisposable
    {
        public void Dispose() => accessor.HttpContext = null;
    }

    private sealed class Nop : IDisposable
    {
        public static readonly Nop Instance = new();

        public void Dispose() { }
    }
}
