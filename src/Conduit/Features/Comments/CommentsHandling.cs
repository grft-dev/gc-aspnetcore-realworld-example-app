using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Errors;
using Microsoft.EntityFrameworkCore;

namespace Conduit.Features.Comments;

public sealed class CommentsHandling(
    ConduitContext context,
    ICurrentUserAccessor currentUserAccessor
)
{
    public async Task<CommentEnvelope> Create(
        CreateCommentData commentData,
        string slug,
        CancellationToken cancellationToken
    )
    {
        var article = await context
            .Articles.Include(x => x.Comments)
            .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);

        if (article == null)
        {
            throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
        }

        var author = await context.Persons.FirstAsync(
            x => x.PersonId == currentUserAccessor.GetCurrentPersonId(),
            cancellationToken
        );

        var comment = new Comment
        {
            Author = author,
            Body = commentData.Body ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await context.Comments.AddAsync(comment, cancellationToken);

        article.Comments.Add(comment);

        await context.SaveChangesAsync(cancellationToken);

        return new CommentEnvelope(comment);
    }

    public async Task<CommentsEnvelope> List(
        List.Query message,
        CancellationToken cancellationToken
    )
    {
        var article = await context
            .Articles.Include(x => x.Comments)
                .ThenInclude(x => x.Author)
            .FirstOrDefaultAsync(x => x.Slug == message.Slug, cancellationToken);

        if (article == null)
        {
            throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);
        }

        return new CommentsEnvelope(article.Comments);
    }

    public async Task Delete(string slug, int id, CancellationToken cancellationToken)
    {
        var article =
            await context
                .Articles.Include(x => x.Comments)
                    .ThenInclude(x => x.Author)
                .FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken)
            ?? throw new RestException(HttpStatusCode.NotFound, "article", Constants.NOT_FOUND);

        var comment =
            article.Comments.FirstOrDefault(x => x.CommentId == id)
            ?? throw new RestException(HttpStatusCode.NotFound, "comment", Constants.NOT_FOUND);

        if (comment.Author?.PersonId != currentUserAccessor.GetCurrentPersonId())
        {
            throw new RestException(HttpStatusCode.Forbidden, "comment", Constants.FORBIDDEN);
        }

        context.Comments.Remove(comment);
        await context.SaveChangesAsync(cancellationToken);
    }
}
