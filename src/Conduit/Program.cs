using System;
using Conduit;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal static class Program
{
    public static void Main(string[] args)
    {
        GraftHost.MarkEntryPoint();

        var contentRoot = GraftHost.ContentRoot();
        var builder = GraftHost.GatewayOwnsProcess
            ? WebApplication.CreateBuilder(
                new WebApplicationOptions
                {
                    Args = [],
                    ApplicationName = typeof(GraftHost).Assembly.GetName().Name,
                    ContentRootPath = contentRoot,
                }
            )
            : WebApplication.CreateBuilder(args);

        builder.Host.UseContentRoot(contentRoot);

        builder.Services.AddConduitDatabase(builder.Configuration);

        builder.Services.AddConduit();

        builder.Services.AddJwt(builder.Configuration);

        var app = builder.Build();
        GraftHost.Use(app.Services);

        app.Services.GetRequiredService<ILoggerFactory>().AddSerilogLogging();

        app.UseMiddleware<ErrorHandlingMiddleware>();

        if (builder.Configuration.GetValue("Database:ApplyMigrations", true))
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ConduitContext>();
            DatabaseInitializer.InitializeAsync(dbContext).GetAwaiter().GetResult();
        }

        // gg --runApp calls Main on its startup path. Start the host and return
        // so Gateway can bind ports 80 and 81. dotnet run waits here instead.
        if (GraftHost.GatewayOwnsProcess)
        {
            app.StartAsync().GetAwaiter().GetResult();
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                IHost host = app;
                host.StopAsync().GetAwaiter().GetResult();
                host.Dispose();
            };
            return;
        }

        app.Run();
    }
}
