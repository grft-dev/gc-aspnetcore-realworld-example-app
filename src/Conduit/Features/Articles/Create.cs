namespace Conduit.Features.Articles;

public class Create
{
    public class ArticleData
    {
        public string? Title { get; init; }

        public string? Description { get; init; }

        public string? Body { get; init; }

        public string[]? TagList { get; init; }
    }
}
