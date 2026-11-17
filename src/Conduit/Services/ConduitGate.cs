using Conduit.Repositories;

namespace Conduit.Services;

internal static class ConduitGate
{
    public static SqlServerConduitRepository Repository() =>
        new(Conduit.ConduitConfig.ConnectionString());
}
