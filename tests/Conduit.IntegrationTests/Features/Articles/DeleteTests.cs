using System;
using System.Linq;
using System.Threading.Tasks;
using Conduit.Features.Articles;
using Conduit.IntegrationTests.Features.Comments;
using Conduit.IntegrationTests.Features.Users;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Conduit.IntegrationTests.Features.Articles;

public class DeleteTests : SliceFixture
{
    [Fact]
    public async Task Expect_Delete_Article()
    {
        var createCmd = new CreateArticleData
        {
            Title = "Test article dsergiu77",
            Description = "Description of the test article",
            Body = "Body of the test article",
        };

        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var slug = article.Slug ?? throw new InvalidOperationException();

        var deleteSlug = slug;

        var dbContext = GetDbContext();

        var articlesHandling = new ArticlesHandling(
            dbContext,
            new StubCurrentUserAccessor(UserHelpers.DefaultUserName)
        );
        await articlesHandling.Delete(deleteSlug, new System.Threading.CancellationToken());

        var dbArticle = await ExecuteDbContextAsync(db =>
            db.Articles.Where(d => d.Slug == deleteSlug).SingleOrDefaultAsync()
        );

        Assert.Null(dbArticle);
    }

    [Fact]
    public async Task Expect_Delete_Article_With_Tags()
    {
        var createCmd = new CreateArticleData
        {
            Title = "Test article dsergiu77",
            Description = "Description of the test article",
            Body = "Body of the test article",
            TagList = ["tag1", "tag2"],
        };

        var article = await ArticleHelpers.CreateArticle(this, createCmd);
        var dbArticleWithTags = await ExecuteDbContextAsync(db =>
            db.Articles.Include(a => a.ArticleTags)
                .Where(d => d.Slug == article.Slug)
                .SingleOrDefaultAsync()
        );

        var deleteSlug = article.Slug ?? throw new InvalidOperationException();

        var dbContext = GetDbContext();

        var articlesHandling = new ArticlesHandling(
            dbContext,
            new StubCurrentUserAccessor(UserHelpers.DefaultUserName)
        );
        await articlesHandling.Delete(deleteSlug, new System.Threading.CancellationToken());

        var dbArticle = await ExecuteDbContextAsync(db =>
            db.Articles.Where(d => d.Slug == deleteSlug).SingleOrDefaultAsync()
        );
        Assert.Null(dbArticle);
    }

    [Fact]
    public async Task Expect_Delete_Article_With_Comments()
    {
        var createArticleCmd = new CreateArticleData
        {
            Title = "Test article dsergiu77",
            Description = "Description of the test article",
            Body = "Body of the test article",
        };

        var article = await ArticleHelpers.CreateArticle(this, createArticleCmd);
        var dbArticle =
            await ExecuteDbContextAsync(db =>
                db.Articles.Include(a => a.ArticleTags)
                    .Where(d => d.Slug == article.Slug)
                    .SingleOrDefaultAsync()
            ) ?? throw new InvalidOperationException();

        var articleId = dbArticle.ArticleId;
        var slug = dbArticle.Slug;

        // create article comment
        var createCommentData = new Conduit.Features.Comments.CreateCommentData("article comment");

        var comment = await CommentHelpers.CreateComment(
            this,
            createCommentData,
            slug ?? throw new InvalidOperationException(),
            UserHelpers.DefaultUserName
        );

        // delete article with comment
        var deleteSlug = slug ?? throw new InvalidOperationException();

        var dbContext = GetDbContext();

        var articlesHandling = new ArticlesHandling(
            dbContext,
            new StubCurrentUserAccessor(UserHelpers.DefaultUserName)
        );
        await articlesHandling.Delete(deleteSlug, new System.Threading.CancellationToken());

        var deleted = await ExecuteDbContextAsync(db =>
            db.Articles.Where(d => d.Slug == deleteSlug).SingleOrDefaultAsync()
        );
        Assert.Null(deleted);
    }
}
