using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Conduit;

public static class GraftHost
{
    private static readonly object Gate = new();
    private static IServiceProvider? _services;
    private static int _entryPointStarted;

    public static bool GatewayOwnsProcess
    {
        get
        {
            var entry = Assembly.GetEntryAssembly();
            if (entry is null)
            {
                return true;
            }

            return !string.Equals(
                entry.GetName().Name,
                typeof(GraftHost).Assembly.GetName().Name,
                StringComparison.Ordinal
            );
        }
    }

    public static string ContentRoot()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("ASPNETCORE_CONTENTROOT"),
            Path.GetDirectoryName(typeof(GraftHost).Assembly.Location),
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            return Path.GetFullPath(candidate);
        }

        return Path.GetFullPath(".");
    }

    public static void MarkEntryPoint()
    {
        lock (Gate)
        {
            _entryPointStarted = 1;
        }
    }

    public static void Use(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        lock (Gate)
        {
            _services ??= services;
            _entryPointStarted = 0;
            Monitor.PulseAll(Gate);
        }
    }

    public static IServiceProvider Services
    {
        get
        {
            lock (Gate)
            {
                var deadline = Environment.TickCount64 + 30_000;
                while (
                    _services is null
                    && _entryPointStarted == 1
                    && Environment.TickCount64 < deadline
                )
                {
                    var remaining = deadline - Environment.TickCount64;
                    if (remaining <= 0)
                    {
                        break;
                    }

                    Monitor.Wait(Gate, TimeSpan.FromMilliseconds(remaining));
                }

                if (_services is not null)
                {
                    return _services;
                }

                if (_entryPointStarted == 1)
                {
                    throw new InvalidOperationException(
                        "Program.Main did not publish the Graft host."
                    );
                }

                var builder = Host.CreateApplicationBuilder(
                    new HostApplicationBuilderSettings
                    {
                        ApplicationName = typeof(GraftHost).Assembly.GetName().Name,
                        ContentRootPath = ContentRoot(),
                        Args = [],
                    }
                );
                builder.Services.AddConduitDatabase(builder.Configuration);
                builder.Services.AddConduit();
                builder.Services.AddJwt(builder.Configuration);
                var host = builder.Build();
                using (var scope = host.Services.CreateScope())
                {
                    var database =
                        scope.ServiceProvider.GetRequiredService<Infrastructure.ConduitContext>();
                    Infrastructure
                        .DatabaseInitializer.InitializeAsync(database)
                        .GetAwaiter()
                        .GetResult();
                }

                host.StartAsync().GetAwaiter().GetResult();
                AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                {
                    host.StopAsync().GetAwaiter().GetResult();
                    host.Dispose();
                };
                _services = host.Services;
                return _services;
            }
        }
    }
}
