IF DB_ID(N'Conduit') IS NULL
    CREATE DATABASE Conduit;
GO

USE Conduit;
GO

IF OBJECT_ID(N'Persons', N'U') IS NULL
    CREATE TABLE Persons (
        PersonId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Username nvarchar(256) NOT NULL,
        Email nvarchar(320) NOT NULL,
        Bio nvarchar(max) NULL,
        Image nvarchar(max) NULL,
        PasswordHash varbinary(32) NOT NULL,
        PasswordSalt varbinary(16) NOT NULL,
        CONSTRAINT UX_Persons_Username UNIQUE (Username),
        CONSTRAINT UX_Persons_Email UNIQUE (Email)
    );
GO

IF OBJECT_ID(N'Articles', N'U') IS NULL
    CREATE TABLE Articles (
        ArticleId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        AuthorId int NOT NULL,
        Slug nvarchar(450) NOT NULL,
        Title nvarchar(max) NOT NULL,
        Description nvarchar(max) NOT NULL,
        Body nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT UX_Articles_Slug UNIQUE (Slug),
        CONSTRAINT FK_Articles_Persons FOREIGN KEY (AuthorId) REFERENCES Persons(PersonId)
    );
GO

IF OBJECT_ID(N'Tags', N'U') IS NULL
    CREATE TABLE Tags (
        TagId nvarchar(256) NOT NULL PRIMARY KEY
    );
GO

IF OBJECT_ID(N'ArticleTags', N'U') IS NULL
    CREATE TABLE ArticleTags (
        ArticleId int NOT NULL,
        TagId nvarchar(256) NOT NULL,
        CONSTRAINT PK_ArticleTags PRIMARY KEY (ArticleId, TagId),
        CONSTRAINT FK_ArticleTags_Articles FOREIGN KEY (ArticleId) REFERENCES Articles(ArticleId) ON DELETE CASCADE,
        CONSTRAINT FK_ArticleTags_Tags FOREIGN KEY (TagId) REFERENCES Tags(TagId)
    );
GO

IF OBJECT_ID(N'ArticleFavorites', N'U') IS NULL
    CREATE TABLE ArticleFavorites (
        ArticleId int NOT NULL,
        PersonId int NOT NULL,
        CONSTRAINT PK_ArticleFavorites PRIMARY KEY (ArticleId, PersonId),
        CONSTRAINT FK_ArticleFavorites_Articles FOREIGN KEY (ArticleId) REFERENCES Articles(ArticleId) ON DELETE CASCADE,
        CONSTRAINT FK_ArticleFavorites_Persons FOREIGN KEY (PersonId) REFERENCES Persons(PersonId)
    );
GO

IF OBJECT_ID(N'Comments', N'U') IS NULL
    CREATE TABLE Comments (
        CommentId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ArticleId int NOT NULL,
        AuthorId int NOT NULL,
        Body nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL,
        UpdatedAt datetime2 NOT NULL,
        CONSTRAINT FK_Comments_Articles FOREIGN KEY (ArticleId) REFERENCES Articles(ArticleId) ON DELETE CASCADE,
        CONSTRAINT FK_Comments_Persons FOREIGN KEY (AuthorId) REFERENCES Persons(PersonId)
    );
GO

IF OBJECT_ID(N'FollowedPeople', N'U') IS NULL
    CREATE TABLE FollowedPeople (
        ObserverId int NOT NULL,
        TargetId int NOT NULL,
        CONSTRAINT PK_FollowedPeople PRIMARY KEY (ObserverId, TargetId),
        CONSTRAINT FK_FollowedPeople_Observer FOREIGN KEY (ObserverId) REFERENCES Persons(PersonId),
        CONSTRAINT FK_FollowedPeople_Target FOREIGN KEY (TargetId) REFERENCES Persons(PersonId)
    );
GO

IF NOT EXISTS (SELECT 1 FROM Persons WHERE Username = N'jacob')
    INSERT INTO Persons (Username, Email, Bio, Image, PasswordHash, PasswordSalt)
    VALUES (
        N'jacob',
        N'jacob@example.com',
        N'I work at State Farm.',
        N'https://i.imgur.com/Qr71crq.jpg',
        0x7D7851B12E779A200F059ADDB3A60B0380BEAB7286812AAB39406A144F0EFB4B,
        0x102233445566778899AABBCCDDEEFF01);
GO

IF NOT EXISTS (SELECT 1 FROM Tags WHERE TagId = N'dragons')
    INSERT INTO Tags (TagId) VALUES (N'dragons');
GO

IF NOT EXISTS (SELECT 1 FROM Articles WHERE Slug = N'how-to-train-your-dragon')
    INSERT INTO Articles (AuthorId, Slug, Title, Description, Body, CreatedAt, UpdatedAt)
    SELECT
        p.PersonId,
        N'how-to-train-your-dragon',
        N'How to train your dragon',
        N'Ever wonder how?',
        N'It takes a Jacobian',
        '2024-01-15T12:00:00.0000000',
        '2024-01-15T12:00:00.0000000'
    FROM Persons p
    WHERE p.Username = N'jacob';
GO

IF NOT EXISTS (
    SELECT 1
    FROM ArticleTags atg
    INNER JOIN Articles a ON a.ArticleId = atg.ArticleId
    WHERE a.Slug = N'how-to-train-your-dragon' AND atg.TagId = N'dragons')
    INSERT INTO ArticleTags (ArticleId, TagId)
    SELECT a.ArticleId, N'dragons'
    FROM Articles a
    WHERE a.Slug = N'how-to-train-your-dragon';
GO
