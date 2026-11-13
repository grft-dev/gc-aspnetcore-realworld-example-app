namespace Conduit.Features.Articles;

public class List
{
    public record Query(
        string? Tag,
        string? Author,
        string? FavoritedUsername,
        int? Limit,
        int? Offset,
        bool IsFeed = false
    );
}
