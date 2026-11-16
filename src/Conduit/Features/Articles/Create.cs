namespace Conduit.Features.Articles;

public class CreateArticleData
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Body { get; init; }

    public string[]? TagList { get; init; }
}
