using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Conduit.Repositories;

internal sealed class SqlServerConduitRepository(string connectionString)
{
    private const string ArticleColumns = """
        SELECT
            a.ArticleId,
            a.Slug,
            a.Title,
            a.Description,
            a.Body,
            a.CreatedAt,
            a.UpdatedAt,
            a.AuthorId,
            p.Username AS AuthorUsername,
            p.Bio AS AuthorBio,
            p.Image AS AuthorImage,
            (SELECT COUNT(*) FROM ArticleFavorites af WHERE af.ArticleId = a.ArticleId) AS FavoritesCount,
            CAST(CASE
                WHEN @ViewerId IS NOT NULL AND EXISTS (
                    SELECT 1 FROM ArticleFavorites af
                    WHERE af.ArticleId = a.ArticleId AND af.PersonId = @ViewerId)
                THEN 1 ELSE 0 END AS bit) AS Favorited,
            CAST(CASE
                WHEN @ViewerId IS NOT NULL AND EXISTS (
                    SELECT 1 FROM FollowedPeople fp
                    WHERE fp.ObserverId = @ViewerId AND fp.TargetId = a.AuthorId)
                THEN 1 ELSE 0 END AS bit) AS AuthorFollowing
        FROM Articles a
        INNER JOIN Persons p ON p.PersonId = a.AuthorId
        """;

    public Task<PersonRow?> GetPersonByIdAsync(int personId) =>
        QueryAsync(connection =>
            connection.QueryFirstOrDefaultAsync<PersonRow>(
                """
                SELECT PersonId, Username, Email, Bio, Image, PasswordHash, PasswordSalt
                FROM Persons
                WHERE PersonId = @PersonId
                """,
                new { PersonId = personId }
            )
        );

    public Task<PersonRow?> GetPersonByEmailAsync(string email) =>
        QueryAsync(connection =>
            connection.QueryFirstOrDefaultAsync<PersonRow>(
                """
                SELECT PersonId, Username, Email, Bio, Image, PasswordHash, PasswordSalt
                FROM Persons
                WHERE Email = @Email
                """,
                new { Email = email }
            )
        );

    public Task<PersonRow?> GetPersonByUsernameAsync(string username) =>
        QueryAsync(connection =>
            connection.QueryFirstOrDefaultAsync<PersonRow>(
                """
                SELECT PersonId, Username, Email, Bio, Image, PasswordHash, PasswordSalt
                FROM Persons
                WHERE Username = @Username
                """,
                new { Username = username }
            )
        );

    public async Task<bool> UsernameTakenAsync(string username, int? exceptPersonId)
    {
        var count = await QueryAsync(connection =>
                connection.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*)
                    FROM Persons
                    WHERE Username = @Username
                      AND (@ExceptId IS NULL OR PersonId <> @ExceptId)
                    """,
                    new { Username = username, ExceptId = exceptPersonId }
                )
            )
            .ConfigureAwait(false);
        return count > 0;
    }

    public async Task<bool> EmailTakenAsync(string email, int? exceptPersonId)
    {
        var count = await QueryAsync(connection =>
                connection.ExecuteScalarAsync<int>(
                    """
                    SELECT COUNT(*)
                    FROM Persons
                    WHERE Email = @Email
                      AND (@ExceptId IS NULL OR PersonId <> @ExceptId)
                    """,
                    new { Email = email, ExceptId = exceptPersonId }
                )
            )
            .ConfigureAwait(false);
        return count > 0;
    }

    public Task<int> InsertPersonAsync(string username, string email, byte[] hash, byte[] salt) =>
        WriteAsync(connection =>
            connection.QuerySingleAsync<int>(
                """
                INSERT INTO Persons (Username, Email, Bio, Image, PasswordHash, PasswordSalt)
                OUTPUT INSERTED.PersonId
                VALUES (@Username, @Email, NULL, NULL, @PasswordHash, @PasswordSalt)
                """,
                new
                {
                    Username = username,
                    Email = email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                }
            )
        );

    public Task UpdatePersonAsync(PersonRow person) =>
        WriteAsync(connection =>
            connection.ExecuteAsync(
                """
                UPDATE Persons
                SET Username = @Username,
                    Email = @Email,
                    Bio = @Bio,
                    Image = @Image,
                    PasswordHash = @PasswordHash,
                    PasswordSalt = @PasswordSalt
                WHERE PersonId = @PersonId
                """,
                person
            )
        );

    public Task<bool> IsFollowingAsync(int observerId, int targetId) =>
        QueryAsync(async connection =>
        {
            var found = await connection
                .ExecuteScalarAsync<int>(
                    """
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM FollowedPeople
                        WHERE ObserverId = @ObserverId AND TargetId = @TargetId
                    ) THEN 1 ELSE 0 END
                    """,
                    new { ObserverId = observerId, TargetId = targetId }
                )
                .ConfigureAwait(false);
            return found == 1;
        });

    public Task FollowAsync(int observerId, int targetId) =>
        WriteAsync(connection =>
            connection.ExecuteAsync(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM FollowedPeople
                    WHERE ObserverId = @ObserverId AND TargetId = @TargetId)
                INSERT INTO FollowedPeople (ObserverId, TargetId)
                VALUES (@ObserverId, @TargetId)
                """,
                new { ObserverId = observerId, TargetId = targetId }
            )
        );

    public Task UnfollowAsync(int observerId, int targetId) =>
        WriteAsync(connection =>
            connection.ExecuteAsync(
                """
                DELETE FROM FollowedPeople
                WHERE ObserverId = @ObserverId AND TargetId = @TargetId
                """,
                new { ObserverId = observerId, TargetId = targetId }
            )
        );

    public async Task<ArticleRow?> GetArticleBySlugAsync(string slug, int? viewerId)
    {
        var article = await QueryAsync(connection =>
                connection.QueryFirstOrDefaultAsync<ArticleRow>(
                    ArticleColumns
                        + """

                        WHERE a.Slug = @Slug
                        """,
                    new { Slug = slug, ViewerId = viewerId }
                )
            )
            .ConfigureAwait(false);
        if (article == null)
        {
            return null;
        }

        await AttachTagsAsync([article]).ConfigureAwait(false);
        return article;
    }

    public async Task<ArticleRow[]> ListArticlesAsync(
        int? viewerId,
        string tag,
        string author,
        string favoritedUsername,
        int limit,
        int offset,
        bool feed
    )
    {
        ArticleRow[] articles =
        [
            .. await QueryAsync(connection =>
                    connection.QueryAsync<ArticleRow>(
                        ArticleColumns
                            + """

                            WHERE (@Feed = 0 OR EXISTS (
                                    SELECT 1 FROM FollowedPeople fp
                                    WHERE fp.ObserverId = @ViewerId AND fp.TargetId = a.AuthorId))
                              AND (@Tag = N'' OR EXISTS (
                                    SELECT 1 FROM ArticleTags atg
                                    WHERE atg.ArticleId = a.ArticleId AND atg.TagId = @Tag))
                              AND (@Author = N'' OR p.Username = @Author)
                              AND (@FavoritedUsername = N'' OR EXISTS (
                                    SELECT 1
                                    FROM ArticleFavorites fav
                                    INNER JOIN Persons favPerson ON favPerson.PersonId = fav.PersonId
                                    WHERE fav.ArticleId = a.ArticleId AND favPerson.Username = @FavoritedUsername))
                            ORDER BY a.CreatedAt DESC, a.ArticleId DESC
                            OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY
                            """,
                        new
                        {
                            ViewerId = viewerId,
                            Tag = tag,
                            Author = author,
                            FavoritedUsername = favoritedUsername,
                            Feed = feed,
                            Offset = offset,
                            Limit = limit,
                        }
                    )
                )
                .ConfigureAwait(false),
        ];
        await AttachTagsAsync(articles).ConfigureAwait(false);
        return articles;
    }

    public async Task<ArticleRow> CreateArticleAsync(
        int authorId,
        string title,
        string description,
        string body,
        string[] tags
    )
    {
        var articleId = await InTransactionAsync(
                async (connection, transaction) =>
                {
                    var slug = await AllocateSlugAsync(connection, transaction, title, null)
                        .ConfigureAwait(false);
                    var id = await connection
                        .QuerySingleAsync<int>(
                            """
                            INSERT INTO Articles (AuthorId, Slug, Title, Description, Body, CreatedAt, UpdatedAt)
                            OUTPUT INSERTED.ArticleId
                            VALUES (@AuthorId, @Slug, @Title, @Description, @Body, SYSUTCDATETIME(), SYSUTCDATETIME())
                            """,
                            new
                            {
                                AuthorId = authorId,
                                Slug = slug,
                                Title = title,
                                Description = description,
                                Body = body,
                            },
                            transaction
                        )
                        .ConfigureAwait(false);
                    await ReplaceTagsAsync(connection, transaction, id, tags).ConfigureAwait(false);
                    return id;
                }
            )
            .ConfigureAwait(false);

        return await GetArticleByIdAsync(articleId, authorId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
    }

    public async Task<ArticleRow> UpdateArticleAsync(
        ArticleRow article,
        string title,
        string description,
        string body,
        string[] tags,
        bool titleChanged
    )
    {
        await InTransactionAsync(
                async (connection, transaction) =>
                {
                    var slug = article.Slug;
                    if (titleChanged)
                    {
                        slug = await AllocateSlugAsync(
                                connection,
                                transaction,
                                title,
                                article.ArticleId
                            )
                            .ConfigureAwait(false);
                    }

                    await connection
                        .ExecuteAsync(
                            """
                            UPDATE Articles
                            SET Slug = @Slug,
                                Title = @Title,
                                Description = @Description,
                                Body = @Body,
                                UpdatedAt = SYSUTCDATETIME()
                            WHERE ArticleId = @ArticleId
                            """,
                            new
                            {
                                article.ArticleId,
                                Slug = slug,
                                Title = title,
                                Description = description,
                                Body = body,
                            },
                            transaction
                        )
                        .ConfigureAwait(false);
                    await ReplaceTagsAsync(connection, transaction, article.ArticleId, tags)
                        .ConfigureAwait(false);
                    return 0;
                }
            )
            .ConfigureAwait(false);

        return await GetArticleByIdAsync(article.ArticleId, article.AuthorId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("article not found");
    }

    public Task DeleteArticleAsync(int articleId) =>
        WriteAsync(connection =>
            connection.ExecuteAsync(
                "DELETE FROM Articles WHERE ArticleId = @ArticleId",
                new { ArticleId = articleId }
            )
        );

    public Task FavoriteAsync(int articleId, int personId) =>
        WriteAsync(connection =>
            connection.ExecuteAsync(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM ArticleFavorites
                    WHERE ArticleId = @ArticleId AND PersonId = @PersonId)
                INSERT INTO ArticleFavorites (ArticleId, PersonId)
                VALUES (@ArticleId, @PersonId)
                """,
                new { ArticleId = articleId, PersonId = personId }
            )
        );

    public Task UnfavoriteAsync(int articleId, int personId) =>
        WriteAsync(connection =>
            connection.ExecuteAsync(
                """
                DELETE FROM ArticleFavorites
                WHERE ArticleId = @ArticleId AND PersonId = @PersonId
                """,
                new { ArticleId = articleId, PersonId = personId }
            )
        );

    public async Task<CommentRow> CreateCommentAsync(int articleId, int authorId, string body)
    {
        var commentId = await WriteAsync(connection =>
                connection.QuerySingleAsync<int>(
                    """
                    INSERT INTO Comments (ArticleId, AuthorId, Body, CreatedAt, UpdatedAt)
                    OUTPUT INSERTED.CommentId
                    VALUES (@ArticleId, @AuthorId, @Body, SYSUTCDATETIME(), SYSUTCDATETIME())
                    """,
                    new
                    {
                        ArticleId = articleId,
                        AuthorId = authorId,
                        Body = body,
                    }
                )
            )
            .ConfigureAwait(false);
        return await GetCommentAsync(commentId, authorId).ConfigureAwait(false)
            ?? throw new InvalidOperationException("comment not found");
    }

    public async Task<CommentRow[]> ListCommentsAsync(int articleId, int? viewerId)
    {
        var comments = await QueryAsync(connection =>
                connection.QueryAsync<CommentRow>(
                    """
                    SELECT
                        c.CommentId,
                        c.ArticleId,
                        c.AuthorId,
                        c.Body,
                        c.CreatedAt,
                        c.UpdatedAt,
                        p.Username AS AuthorUsername,
                        p.Bio AS AuthorBio,
                        p.Image AS AuthorImage,
                        CAST(CASE
                            WHEN @ViewerId IS NOT NULL AND EXISTS (
                                SELECT 1 FROM FollowedPeople fp
                                WHERE fp.ObserverId = @ViewerId AND fp.TargetId = c.AuthorId)
                            THEN 1 ELSE 0 END AS bit) AS AuthorFollowing
                    FROM Comments c
                    INNER JOIN Persons p ON p.PersonId = c.AuthorId
                    WHERE c.ArticleId = @ArticleId
                    ORDER BY c.CreatedAt ASC, c.CommentId ASC
                    """,
                    new { ArticleId = articleId, ViewerId = viewerId }
                )
            )
            .ConfigureAwait(false);
        return [.. comments];
    }

    public Task<CommentRow?> GetCommentAsync(int commentId, int? viewerId) =>
        QueryAsync(connection =>
            connection.QueryFirstOrDefaultAsync<CommentRow>(
                """
                SELECT
                    c.CommentId,
                    c.ArticleId,
                    c.AuthorId,
                    c.Body,
                    c.CreatedAt,
                    c.UpdatedAt,
                    p.Username AS AuthorUsername,
                    p.Bio AS AuthorBio,
                    p.Image AS AuthorImage,
                    CAST(CASE
                        WHEN @ViewerId IS NOT NULL AND EXISTS (
                            SELECT 1 FROM FollowedPeople fp
                            WHERE fp.ObserverId = @ViewerId AND fp.TargetId = c.AuthorId)
                        THEN 1 ELSE 0 END AS bit) AS AuthorFollowing
                FROM Comments c
                INNER JOIN Persons p ON p.PersonId = c.AuthorId
                WHERE c.CommentId = @CommentId
                """,
                new { CommentId = commentId, ViewerId = viewerId }
            )
        );

    public Task DeleteCommentAsync(int commentId) =>
        WriteAsync(connection =>
            connection.ExecuteAsync(
                "DELETE FROM Comments WHERE CommentId = @CommentId",
                new { CommentId = commentId }
            )
        );

    public async Task<string[]> ListTagsAsync()
    {
        var tags = await QueryAsync(connection =>
                connection.QueryAsync<string>("SELECT TagId FROM Tags ORDER BY TagId")
            )
            .ConfigureAwait(false);
        return [.. tags];
    }

    private async Task<ArticleRow?> GetArticleByIdAsync(int articleId, int? viewerId)
    {
        var article = await QueryAsync(connection =>
                connection.QueryFirstOrDefaultAsync<ArticleRow>(
                    ArticleColumns
                        + """

                        WHERE a.ArticleId = @ArticleId
                        """,
                    new { ArticleId = articleId, ViewerId = viewerId }
                )
            )
            .ConfigureAwait(false);
        if (article == null)
        {
            return null;
        }

        await AttachTagsAsync([article]).ConfigureAwait(false);
        return article;
    }

    private async Task AttachTagsAsync(ArticleRow[] articles)
    {
        if (articles.Length == 0)
        {
            return;
        }

        var ids = articles.Select(article => article.ArticleId).ToArray();
        var links = await QueryAsync(connection =>
                connection.QueryAsync<ArticleTagRow>(
                    """
                    SELECT ArticleId, TagId
                    FROM ArticleTags
                    WHERE ArticleId IN @Ids
                    ORDER BY TagId
                    """,
                    new { Ids = ids }
                )
            )
            .ConfigureAwait(false);
        var lookup = links
            .GroupBy(link => link.ArticleId)
            .ToDictionary(group => group.Key, group => group.Select(link => link.TagId).ToList());
        foreach (var article in articles)
        {
            if (lookup.TryGetValue(article.ArticleId, out var tags))
            {
                article.Tags = tags;
            }
        }
    }

    private static async Task<string> AllocateSlugAsync(
        SqlConnection connection,
        DbTransaction transaction,
        string title,
        int? exceptArticleId
    )
    {
        var slug = Slug.Generate(title);
        var unique = slug;
        for (var i = 1; ; i++)
        {
            var taken = await connection
                .ExecuteScalarAsync<int>(
                    """
                    SELECT CASE WHEN EXISTS (
                        SELECT 1 FROM Articles WITH (UPDLOCK, HOLDLOCK)
                        WHERE Slug = @Slug
                          AND (@ExceptId IS NULL OR ArticleId <> @ExceptId)
                    ) THEN 1 ELSE 0 END
                    """,
                    new { Slug = unique, ExceptId = exceptArticleId },
                    transaction
                )
                .ConfigureAwait(false);
            if (taken == 0)
            {
                return unique;
            }

            unique = $"{slug}-{i}";
        }
    }

    private static async Task ReplaceTagsAsync(
        SqlConnection connection,
        DbTransaction transaction,
        int articleId,
        string[] tags
    )
    {
        await connection
            .ExecuteAsync(
                "DELETE FROM ArticleTags WHERE ArticleId = @ArticleId",
                new { ArticleId = articleId },
                transaction
            )
            .ConfigureAwait(false);
        foreach (var tag in tags)
        {
            await connection
                .ExecuteAsync(
                    """
                    MERGE Tags AS target
                    USING (SELECT @TagId AS TagId) AS source
                    ON target.TagId = source.TagId
                    WHEN NOT MATCHED THEN INSERT (TagId) VALUES (source.TagId);
                    """,
                    new { TagId = tag },
                    transaction
                )
                .ConfigureAwait(false);
            await connection
                .ExecuteAsync(
                    """
                    INSERT INTO ArticleTags (ArticleId, TagId)
                    VALUES (@ArticleId, @TagId)
                    """,
                    new { ArticleId = articleId, TagId = tag },
                    transaction
                )
                .ConfigureAwait(false);
        }
    }

    private async Task<T> QueryAsync<T>(Func<SqlConnection, Task<T>> query)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        return await query(connection).ConfigureAwait(false);
    }

    private async Task<T> WriteAsync<T>(Func<SqlConnection, Task<T>> write)
    {
        try
        {
            return await QueryAsync(write).ConfigureAwait(false);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw MapDuplicate(exception);
        }
    }

    private async Task<T> InTransactionAsync<T>(Func<SqlConnection, DbTransaction, Task<T>> action)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync()
            .ConfigureAwait(false);
        try
        {
            var result = await action(connection, transaction).ConfigureAwait(false);
            await transaction.CommitAsync().ConfigureAwait(false);
            return result;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw MapDuplicate(exception);
        }
    }

    private static InvalidOperationException MapDuplicate(SqlException exception)
    {
        var detail = exception.Message;
        if (detail.Contains("UX_Persons_Username", StringComparison.Ordinal))
        {
            return new InvalidOperationException("username has already been taken", exception);
        }

        if (detail.Contains("UX_Persons_Email", StringComparison.Ordinal))
        {
            return new InvalidOperationException("email has already been taken", exception);
        }

        return new InvalidOperationException("slug has already been taken", exception);
    }
}
