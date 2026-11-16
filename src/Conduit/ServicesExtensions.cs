using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.Features.Comments;
using Conduit.Features.Favorites;
using Conduit.Features.Followers;
using Conduit.Features.Profiles;
using Conduit.Features.Tags;
using Conduit.Features.Users;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Conduit;

public static class ServicesExtensions
{
    public static void AddConduitDatabase(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var provider =
            configuration["Database:Provider"]
            ?? configuration["Conduit_DatabaseProvider"]
            ?? "sqlite";
        var connection =
            configuration.GetConnectionString("Conduit")
            ?? configuration["Conduit_ConnectionString"];
        switch (provider.Trim().ToLowerInvariant())
        {
            case "sqlite":
                services.AddDbContext<SqliteConduitContext>(options =>
                    options.UseSqlite(connection ?? "Data Source=realworld.db")
                );
                services.AddScoped<ConduitContext>(sp =>
                    sp.GetRequiredService<SqliteConduitContext>()
                );
                break;
            case "sqlserver":
                if (string.IsNullOrWhiteSpace(connection))
                {
                    throw new InvalidOperationException(
                        "ConnectionStrings:Conduit is required for SQL Server."
                    );
                }
                services.AddDbContext<SqlServerConduitContext>(options =>
                    options.UseSqlServer(connection)
                );
                services.AddScoped<ConduitContext>(sp =>
                    sp.GetRequiredService<SqlServerConduitContext>()
                );
                break;
            default:
                throw new InvalidOperationException(
                    "Database:Provider must be sqlite or sqlserver."
                );
        }
    }

    public static void AddConduit(this IServiceCollection services)
    {
        services.AddSingleton<Features.ConduitMapper>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<IProfileReader, ProfileReader>();
        services.AddScoped<UsersHandling>();
        services.AddScoped<UserHandling>();
        services.AddScoped<ProfilesHandling>();
        services.AddScoped<FollowersHandling>();
        services.AddScoped<ArticlesHandling>();
        services.AddScoped<FavoritesHandling>();
        services.AddScoped<CommentsHandling>();
        services.AddScoped<TagsHandling>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    }

    public static void AddJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions();

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configuration["Jwt:SigningKey"] ?? "");
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a base64-encoded random key of at least 32 bytes."
            );
        }
        if (
            key.Length < 32
            || key.Distinct().Count() < 16
            || Encoding
                .UTF8.GetString(key)
                .Contains("somethinglongerforthisdumbalgorithmisrequired", StringComparison.Ordinal)
            || HasRepeatedPattern(key)
        )
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a base64-encoded random key of at least 32 bytes; default and placeholder keys are not allowed."
            );
        }
        var signingKey = new SymmetricSecurityKey(key);
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var issuer = "issuer";
        var audience = "audience";

        services.Configure<JwtIssuerOptions>(options =>
        {
            options.Issuer = issuer;
            options.Audience = audience;
            options.SigningCredentials = signingCredentials;
        });

        var tokenValidationParameters = new TokenValidationParameters
        {
            // The signing key must match!
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            IssuerSigningKey = signingCredentials.Key,
            // Validate the JWT Issuer (iss) claim
            ValidateIssuer = true,
            ValidIssuer = issuer,
            // Validate the JWT Audience (aud) claim
            ValidateAudience = true,
            ValidAudience = audience,
            // Validate the token expiry
            ValidateLifetime = true,
            // If you want to allow a certain amount of clock drift, set that here:
            ClockSkew = TimeSpan.Zero,
        };

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = tokenValidationParameters;
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (!JwtPersonIdentity.TryGetPersonId(context.Principal, out var personId))
                        {
                            context.Fail("Invalid user identity");
                            return;
                        }
                        var db =
                            context.HttpContext.RequestServices.GetRequiredService<ConduitContext>();
                        if (
                            !await JwtPersonIdentity.PersonExistsAsync(
                                db,
                                personId,
                                context.HttpContext.RequestAborted
                            )
                        )
                        {
                            context.Fail("User no longer exists");
                            return;
                        }
                    },
                    OnMessageReceived = (context) =>
                    {
                        var token = context.HttpContext.Request.Headers.Authorization.ToString();
                        if (token.StartsWith("Token ", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = token["Token ".Length..].Trim();
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = (context) =>
                    {
                        // the RealWorld spec expects a JSON body on 401 responses
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        return context.Response.WriteAsync(
                            """{"errors":{"token":["is missing"]}}"""
                        );
                    },
                };
            });
    }

    private static bool HasRepeatedPattern(byte[] key)
    {
        for (var period = 1; period <= key.Length / 2; period++)
        {
            var isRepeated = true;
            for (var index = period; index < key.Length; index++)
            {
                if (key[index] == key[index % period])
                {
                    continue;
                }

                isRepeated = false;
                break;
            }

            if (isRepeated)
            {
                return true;
            }
        }

        return false;
    }

    public static void AddSerilogLogging(this ILoggerFactory loggerFactory)
    {
        // Attach the sink to the logger configuration
        var log = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            //just for local debug
            .WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] {SourceContext} {Message}{NewLine}{Exception}",
                theme: AnsiConsoleTheme.Code
            )
            .CreateLogger();

        loggerFactory.AddSerilog(log, dispose: true);
    }
}
