using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Features.Profiles;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Articles;

public sealed class ArticlesHandling(
    ConduitContext context,
    ICurrentUserAccessor currentUserAccessor
)
{
    public async Task<ArticlesEnvelope> List(
        List.Query message,
        CancellationToken cancellationToken
    )
    {
        var currentPersonId = currentUserAccessor.GetCurrentPersonId();
        var queryable = context.Articles.AsNoTracking();

        if (message.IsFeed)
        {
            if (currentPersonId == null)
            {
                throw new RestException(HttpStatusCode.Unauthorized, "token", "is missing");
            }
            queryable = queryable.Where(x =>
                x.Author != null
                && context.FollowedPeople.Any(y =>
                    y.ObserverId == currentPersonId && y.TargetId == x.Author.PersonId
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(message.Tag))
        {
            queryable = queryable.Where(x => x.ArticleTags.Any(y => y.TagId == message.Tag));
        }

        if (!string.IsNullOrWhiteSpace(message.Author))
        {
            queryable = queryable.Where(x =>
                x.Author != null && x.Author.Username == message.Author
            );
        }

        if (!string.IsNullOrWhiteSpace(message.FavoritedUsername))
        {
            queryable = queryable.Where(x =>
                x.ArticleFavorites.Any(y =>
                    y.Person != null && y.Person.Username == message.FavoritedUsername
                )
            );
        }

        var count = await queryable.CountAsync(cancellationToken);
        var articles = await queryable
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.ArticleId)
            .Skip(message.Offset ?? 0)
            .Take(message.Limit ?? 20)
            .Select(x => new ArticleSummary
            {
                Slug = x.Slug,
                Title = x.Title,
                Description = x.Description,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                TagList = x.ArticleTags.Select(y => y.TagId!).ToList(),
                FavoritesCount = x.ArticleFavorites.Count,
                Favorited =
                    currentPersonId != null
                    && x.ArticleFavorites.Any(y => y.PersonId == currentPersonId),
                Author =
                    x.Author == null
                        ? null
                        : new Profile
                        {
                            Username = x.Author.Username,
                            Bio = x.Author.Bio,
                            Image = x.Author.Image,
                            IsFollowed =
                                currentPersonId != null
                                && context.FollowedPeople.Any(y =>
                                    y.ObserverId == currentPersonId
                                    && y.TargetId == x.Author.PersonId
                                ),
                        },
            })
            .ToListAsync(cancellationToken);
        return new ArticlesEnvelope { Articles = articles, ArticlesCount = count };
    }

    public async Task<ArticleEnvelope> Details(
        Details.Query message,
        CancellationToken cancellationToken
    )
    {
        var article = await context
            .Articles.GetAllData()
            .FirstOrDefaultAsync(x => x.Slug == message.Slug, cancellationToken);

        if (article == null)
        {
            throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
        }
        new[] { article }.PopulateFavorited(currentUserAccessor.GetCurrentPersonId());
        return new ArticleEnvelope(article);
    }

    public async Task<ArticleEnvelope> Create(
        Create.ArticleData articleData,
        CancellationToken cancellationToken
    )
    {
        var author = await context.Persons.FirstAsync(
            x => x.PersonId == currentUserAccessor.GetCurrentPersonId(),
            cancellationToken
        );
        var tags = new List<Tag>();
        foreach (var tag in (articleData.TagList ?? []).Distinct(StringComparer.Ordinal))
        {
            var t = await context.Tags.FindAsync([tag], cancellationToken);
            if (t == null)
            {
                t = new Tag { TagId = tag };
                await context.Tags.AddAsync(t, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }
            tags.Add(t);
        }

        var slug = articleData.Title.GenerateSlug();
        var uniqueSlug = slug;
        for (
            var i = 1;
            await context.Articles.AnyAsync(x => x.Slug == uniqueSlug, cancellationToken);
            i++
        )
        {
            uniqueSlug = $"{slug}-{i}";
        }

        var article = new Article
        {
            Author = author,
            Body = articleData.Body,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Description = articleData.Description,
            Title = articleData.Title,
            Slug = uniqueSlug,
        };
        await context.Articles.AddAsync(article, cancellationToken);

        await context.ArticleTags.AddRangeAsync(
            tags.Select(x => new ArticleTag { Article = article, Tag = x }),
            cancellationToken
        );

        await context.SaveChangesAsync(cancellationToken);

        return new ArticleEnvelope(article);
    }

    public async Task<ArticleEnvelope> Edit(
        Edit.ArticleData articleData,
        string articleSlug,
        CancellationToken cancellationToken
    )
    {
        var article = await context
            .Articles.Include(x => x.ArticleTags)
            .Include(x => x.Author)
            .Where(x => x.Slug == articleSlug)
            .FirstOrDefaultAsync(cancellationToken);

        if (article == null)
        {
            throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
        }

        if (article.Author?.PersonId != currentUserAccessor.GetCurrentPersonId())
        {
            throw new RestException(HttpStatusCode.Forbidden, "article", Constants.FORBIDDEN);
        }

        article.Description = articleData.Description ?? article.Description;
        article.Body = articleData.Body ?? article.Body;
        if (!string.IsNullOrEmpty(articleData.Title) && articleData.Title != article.Title)
        {
            article.Title = articleData.Title;
            var slug = article.Title.GenerateSlug();
            var uniqueSlug = slug;
            for (
                var i = 1;
                await context.Articles.AnyAsync(
                    x => x.Slug == uniqueSlug && x.ArticleId != article.ArticleId,
                    cancellationToken
                );
                i++
            )
            {
                uniqueSlug = $"{slug}-{i}";
            }
            article.Slug = uniqueSlug;
        }

        var articleTagList = (
            articleData.TagListSet
                ? (articleData.TagList ?? [])
                : article.ArticleTags.Where(x => x.TagId is not null).Select(x => x.TagId!)
        )
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var articleTagsToCreate = GetArticleTagsToCreate(article, articleTagList);
        var articleTagsToDelete = GetArticleTagsToDelete(article, articleTagList);

        if (
            context.ChangeTracker.Entries().First(x => x.Entity == article).State
                == EntityState.Modified
            || articleTagsToCreate.Count != 0
            || articleTagsToDelete.Count != 0
        )
        {
            article.UpdatedAt = DateTime.UtcNow;
        }

        var tagIds = articleTagsToCreate.Select(x => x.TagId).OfType<string>().Distinct().ToArray();
        var existingTagIds = await context
            .Tags.Where(x => x.TagId != null && tagIds.Contains(x.TagId))
            .Select(x => x.TagId!)
            .ToHashSetAsync(cancellationToken);
        await context.Tags.AddRangeAsync(
            tagIds.Where(x => !existingTagIds.Contains(x)).Select(x => new Tag { TagId = x }),
            cancellationToken
        );

        await context.ArticleTags.AddRangeAsync(articleTagsToCreate, cancellationToken);
        context.ArticleTags.RemoveRange(articleTagsToDelete);

        await context.SaveChangesAsync(cancellationToken);

        article = await context
            .Articles.GetAllData()
            .Where(x => x.Slug == article.Slug)
            .FirstOrDefaultAsync(x => x.ArticleId == article.ArticleId, cancellationToken);
        if (article is null)
        {
            throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
        }

        new[] { article }.PopulateFavorited(currentUserAccessor.GetCurrentPersonId());
        return new ArticleEnvelope(article);
    }

    public async Task Delete(string slug, CancellationToken cancellationToken)
    {
        var article =
            await context
                .Articles.Include(x => x.Author)
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken)
            ?? throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);

        if (article.Author?.PersonId != currentUserAccessor.GetCurrentPersonId())
        {
            throw new RestException(HttpStatusCode.Forbidden, "article", Constants.FORBIDDEN);
        }

        context.Articles.Remove(article);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static List<ArticleTag> GetArticleTagsToCreate(
        Article article,
        IEnumerable<string> articleTagList
    )
    {
        var articleTagsToCreate = new List<ArticleTag>();
        foreach (var tag in articleTagList.Distinct())
        {
            var at = article.ArticleTags?.FirstOrDefault(t => t.TagId == tag);
            if (at == null)
            {
                at = new ArticleTag
                {
                    Article = article,
                    ArticleId = article.ArticleId,
                    TagId = tag,
                };
                articleTagsToCreate.Add(at);
            }
        }

        return articleTagsToCreate;
    }

    private static List<ArticleTag> GetArticleTagsToDelete(
        Article article,
        IEnumerable<string> articleTagList
    )
    {
        var articleTagsToDelete = new List<ArticleTag>();
        foreach (var tag in article.ArticleTags)
        {
            var at = articleTagList.FirstOrDefault(t => t == tag.TagId);
            if (at == null)
            {
                articleTagsToDelete.Add(tag);
            }
        }

        return articleTagsToDelete;
    }
}
