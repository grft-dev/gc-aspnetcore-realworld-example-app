namespace Conduit.Repositories;

internal sealed class PersonRow
{
    public int PersonId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Bio { get; set; }

    public string? Image { get; set; }

    public byte[] PasswordHash { get; set; } = [];

    public byte[] PasswordSalt { get; set; } = [];
}

internal sealed class ArticleRow
{
    public int ArticleId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int AuthorId { get; set; }

    public string AuthorUsername { get; set; } = string.Empty;

    public string? AuthorBio { get; set; }

    public string? AuthorImage { get; set; }

    public int FavoritesCount { get; set; }

    public bool Favorited { get; set; }

    public bool AuthorFollowing { get; set; }

    public List<string> Tags { get; set; } = [];
}

internal sealed class CommentRow
{
    public int CommentId { get; set; }

    public int ArticleId { get; set; }

    public int AuthorId { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string AuthorUsername { get; set; } = string.Empty;

    public string? AuthorBio { get; set; }

    public string? AuthorImage { get; set; }

    public bool AuthorFollowing { get; set; }
}

internal sealed class ArticleTagRow
{
    public int ArticleId { get; set; }

    public string TagId { get; set; } = string.Empty;
}
