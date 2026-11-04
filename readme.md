# Conduit on Graftcode

This repository is a fork of the [RealWorld Conduit](https://github.com/gothinkster/aspnetcore-realworld-example-app) sample. It exists to show Graftcode in use: user, profile, article, comment, and tag operations are hosted as a graft on Graftcode Gateway and checked with `gc-realworld-graft-e2e`.

The graft stack does not serve HTTP. The previous ASP.NET Core host, Mediator pipeline, Entity Framework model, and JWT middleware are gone. The same operations and rules run as static methods over SQL Server.

## Exposed public surface

Module:

- `Conduit.dll`

Types and methods (names have no `Async` suffix):

- `Conduit.Services.UserService`
  - `Register`, `Login`, `GetCurrentUser`, `UpdateUser`
- `Conduit.Services.ProfileService`
  - `GetProfile`, `Follow`, `Unfollow`
- `Conduit.Services.ArticleService`
  - `ListArticles`, `Feed`, `GetArticle`, `CreateArticle`, `EditArticle`, `DeleteArticle`, `FavoriteArticle`, `UnfavoriteArticle`
- `Conduit.Services.CommentService`
  - `CreateComment`, `ListComments`, `DeleteComment`
- `Conduit.Services.TagService`
  - `ListTags`

Repositories, row types, the password hasher, the token issuer, and `GraftAsync` stay off the gateway allowlist. The runtime image is `mcr.microsoft.com/dotnet/runtime:10.0`. The host replaces the platform stub of `Microsoft.Data.SqlClient` with the unix implementation, because GG loads the flat DLL.

Protected methods take the token as an ordinary argument. An empty token is anonymous on `ListArticles`, `GetArticle`, `GetProfile`, `ListComments`, and `ListTags`. A missing token on a method that needs a user fails with `token is missing`.

This demo starts the stack without `GG_PROJECT_KEY`.

## Record format

Fields are joined with `|`. `\` and `|` inside a field are escaped as `\\` and `\|`. Timestamps use the round-trip `o` pattern in UTC. A missing row is `string.Empty`. An empty list is an empty array. `DeleteArticle` and `DeleteComment` return `string.Empty`.

- user: `email|token|username|bio|image`
- profile: `username|bio|image|following`
- article: `slug|title|description|body|createdAt|updatedAt|favorited|favoritesCount|tags|authorUsername|authorBio|authorImage|authorFollowing`
- comment: `id|createdAt|updatedAt|body|authorUsername|authorBio|authorImage|authorFollowing`
- tags: `string[]`

`body` is empty on `ListArticles` and `Feed`. Tags inside an article are comma-separated and sorted by name; `\` and `,` in a tag are escaped before the field is escaped. `favorited` and `following` are `true` or `false`.

An empty string on `UpdateUser` or on the title, description, and body of `EditArticle` keeps the current value. `EditArticle` always replaces the tag list. `ListArticles` and `Feed` ignore a filter when its string is empty. A limit of `0` or less uses 20. A negative offset uses 0.

Broken rules throw `InvalidOperationException` with a stable message: `username can't be blank`, `email can't be blank`, `password can't be blank`, `password is too short (minimum is 8 characters)`, `username has already been taken`, `email has already been taken`, `credentials invalid`, `token is missing`, `user not found`, `profile not found`, `article not found`, `comment not found`, `article forbidden`, `comment forbidden`, `title can't be blank`, `description can't be blank`, `body can't be blank`.

The token is an opaque HMAC of the person id and an expiry. Clients pass it back. They do not parse it.

## 1) Start Docker stack with GG

From `ports/gc-aspnetcore-realworld-example-app/src`:

```powershell
docker compose -f docker-compose.graft.yml up --build -d
```

Ports:

- `8094` Gateway (`/nuget`, WebSocket)
- `8095` Graft Vision
- `1436` SQL Server

WebSocket endpoint for clients:

- `ws://localhost:8094/ws`

Graft Vision:

- `http://localhost:8095`

The seed user the verifier reads:

- email `jacob@example.com`
- username `jacob`
- password `password1234`
- article slug `how-to-train-your-dragon`
- tag `dragons`

## 2) Install generated clients

After the container logs `dotnet add package`, open `http://localhost:8094/nuget` and use the package id printed there.

The id below is a placeholder shape. Without a `GG_PROJECT_KEY` linked to your Graftcode account, every generation gets a new suffix. With that key the id stays stable.

- `graft.nuget.conduit_euvmqa` 1.0.0

NuGet will not replace an existing `1.0.0` when the package id stays the same. When the hash changes, delete the old package folder and restore from the feed printed in the container log. When the hash stays and the host surface changed, delete the cached `1.0.0` and restore from that same feed.

Runtime config:

```powershell
$env:GRAFTCODE_CONFIG_NUGET_CONDUIT = "name=graft.nuget.Conduit; runtime=netcore; modules=Conduit.dll; host=ws://localhost:8094/ws; stateless=true"
```

The client reads `GRAFTCODE_CONFIG_NUGET_CONDUIT`. The package id still carries the generation suffix.

## 3) Run the e2e verifier

```powershell
dotnet run --project C:\Coding\Marketing\gc-realworld-graft-e2e\gc-realworld-graft-e2e.csproj -c Release -- `
  --host "ws://localhost:8094/ws" `
  --stateless true `
  --report "C:\Coding\Marketing\gc-realworld-graft-e2e\reports\realworld-e2e.json"
```

`--assembly` is ignored. The runner uses the PackageReference checked into its project file. That id is one generation; replace it when `/nuget` prints a new suffix.

Exit codes: `0` all checks passed, `1` an assertion failed, `2` the run was blocked before assertions.

## 4) Stop

```powershell
docker compose -f docker-compose.graft.yml down
```

## Troubleshooting

- `dotnet add package` takes its source from the container log. There is no fixed feed URL. Without a project key, each start prints a new `https://grft.dev/<guid>__free`. If restore fails, restart the host and use the URL from that log.
- If port `8094` is occupied, change the published ports in `src/docker-compose.graft.yml` and point the client host at the new WebSocket URL.
- Restore under `src` uses the repo-local `NuGet.Config` (nuget.org only).

## Migration notes

The exposed slice keeps the RealWorld operations: registration and login, the current user, profiles and follows, article list, feed, create, edit, delete, favorite, comments, and tags. Slugs still come from the title, with a numeric suffix when the slug is already taken. Only the author can edit or delete an article, and only the author can delete a comment.

What the graft replaces, and what left the exposed project: HTTP controllers, Swagger, the Mediator pipeline, FluentValidation, Entity Framework, SQLite, and request-context authentication. `GraftAsync` is the temporary bridge from the synchronous graft methods to the asynchronous SQL calls. When the gateway accepts `Task`, delete that helper and return `Task` from the same method names.

The business-slice line count is the C# under `src/Conduit`, excluding the host project, container files, and this README.
