using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Tags;

public sealed class TagsHandling(ConduitContext context)
{
    public async Task<List<string>> List(List.Query _, CancellationToken cancellationToken)
    {
        var tags = await context
            .Tags.OrderBy(x => x.TagId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return tags?.Select(x => x.TagId ?? string.Empty).ToList() ?? [];
    }
}
