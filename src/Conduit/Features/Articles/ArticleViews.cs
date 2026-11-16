using Conduit.Domain;
using Conduit.Features.Profiles;

namespace Conduit.Features.Articles;

internal static class ArticleViews
{
    public static ArticleSummary From(Article article) =>
        new()
        {
            Slug = article.Slug,
            Title = article.Title,
            Description = article.Description,
            TagList = article.TagList,
            CreatedAt = article.CreatedAt,
            UpdatedAt = article.UpdatedAt,
            Favorited = article.Favorited,
            FavoritesCount = article.FavoritesCount,
            Author = article.Author is null
                ? null
                : new Profile
                {
                    Username = article.Author.Username,
                    Bio = article.Author.Bio,
                    Image = article.Author.Image,
                    IsFollowed = article.Author.IsFollowedByCurrentUser,
                },
        };
}
