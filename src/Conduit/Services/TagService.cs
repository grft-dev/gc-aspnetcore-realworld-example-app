using Conduit.Graftcode;

namespace Conduit.Services;

public static class TagService
{
    public static string[] ListTags() =>
        GraftAsync.Run(() => ConduitGate.Repository().ListTagsAsync());
}
