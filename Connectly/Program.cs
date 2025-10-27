using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;

using Connectly;
using Connectly.Application.Follower;
using Connectly.Application.Identity;
using Connectly.Application.Posts;
using Connectly.Authorization;
using Connectly.Infrastructure.Data;

using DotNetEnv;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

Env.TraversePath().Load();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ConnectlyDbContext>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Info.Title = "Connectly API";
        doc.Info.Version = "Alpha";
        doc.Info.Description = "A rewrite of the Connectly API in ASP.NET Core";

        doc.Components ??= new OpenApiComponents();
        doc.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
        {
            ["Bearer"] = new()
            {
                Type = SecuritySchemeType.OAuth2,
                In = ParameterLocation.Header,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Token from Auth0",
                Flows = new OpenApiOAuthFlows
                {
                    Implicit = new OpenApiOAuthFlow
                    {
#pragma warning disable S1075
                        AuthorizationUrl =
                            new Uri("https://ewan.au.auth0.com/authorize?audience=https://connectly-noobnoob"),
                        TokenUrl = new Uri("https://ewan.au.auth0.com/oauth/token"),
                        Scopes = new Dictionary<string, string> { ["openid"] = "OpenID" }
#pragma warning restore S1075
                    }
                }
            }
        };
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((op, ctx, ct) =>
    {
        bool hasAuthorizeAttribute =
            ctx.Description.ActionDescriptor.EndpointMetadata.OfType<AuthorizeAttribute>().Any();
        if (hasAuthorizeAttribute)
        {
            op.Security ??= new List<OpenApiSecurityRequirement>();
            op.Security.Add(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    ["openid"]
                }
            });
        }

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((op, ctx, ct) =>
    {
        string? displayName = ctx.Description.ActionDescriptor.DisplayName;
        if (string.IsNullOrEmpty(displayName))
        {
            return Task.CompletedTask;
        }

        op.Summary = displayName;

        return Task.CompletedTask;
    });
});

builder.Services.AddHttpLogging(logging => logging.LoggingFields =
    HttpLoggingFields.RequestPath | HttpLoggingFields.RequestMethod |
    HttpLoggingFields.ResponseStatusCode);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = "https://ewan.au.auth0.com";
        options.Audience = "https://connectly-noobnoob";
        options.TokenValidationParameters = new TokenValidationParameters { NameClaimType = ClaimTypes.NameIdentifier };
    });

builder.Services.AddConnectlyAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.GetExternalId() ?? context.Request.Headers.Host.ToString(),
            factory: partition => new SlidingWindowRateLimiterOptions()
            {
                Window = TimeSpan.FromMinutes(1),
                AutoReplenishment = true,
                PermitLimit = 30,
                QueueLimit = 0,
                SegmentsPerWindow = 6,
            })
    );
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        NewRelic.Api.Agent.NewRelic.RecordCustomEvent("Custom/RateLimiterMaxedOut", [
            new("ExternalId", context.HttpContext.User.GetExternalId() ?? string.Empty),
        ]);
        NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/RateLimiterMaxedOut", 1);
        return ValueTask.CompletedTask;
    };
});

WebApplication app = builder.Build();

app.UseRateLimiter();

app.UseHttpLogging();

app.MapOpenApi("/swagger/connectly.json");
app.UseSwaggerUI(options =>
{
    options.OAuthClientId("xPKdcBAqzJ2YgrbRPtyNYEPFfFqpBnf3");
    options.SwaggerEndpoint("/swagger/connectly.json", "Connectly API");
});

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

RouteGroupBuilder users = app.MapGroup("/api/users").WithTags("Users").RequireRateLimiting("default");
users.MapGet("/", (ConnectlyDbContext db) => db.Users.AsNoTracking().AsEnumerable().Select(x => x.ToFilteredUser()))
    .WithDisplayName("GetUsers")
    .WithDescription("Gets all users")
    .RequireAuthorization();
users.MapGet("/{id:guid}", (ConnectlyDbContext db, Guid id) => db.Users.AsNoTracking().FirstOrDefault(x => x.Id == id))
    .WithDisplayName("GetUser")
    .WithDescription("Gets a user by id")
    .RequireAuthorization();
users.MapGet("/profile", async ([FromServices] IExternalIdentityService identity, CancellationToken ct) =>
    {
        User? user = await identity.GetUserAsync(ct);
        return user?.ToFilteredUser();
    })
    .WithDisplayName("GetProfile")
    .WithDescription("Gets the current user's profile")
    .RequireAuthorization();
users.MapPost("/",
        async ([FromBody] NewUser newUser, [FromServices] ConnectlyDbContext db,
            [FromServices] IExternalIdentityService identity, CancellationToken ct) =>
        {
            bool isUsernameTaken = await db.Users.AsNoTracking()
                .AnyAsync(x => x.Username == newUser.Username, ct);
            bool isExternalIdTaken = await db.Users.AsNoTracking()
                .AnyAsync(x => x.ExternalId == identity.GetExternalUserId(), ct);
            if (isUsernameTaken || isExternalIdTaken)
            {
                return Results.BadRequest();
            }

            if (!newUser.Username.ContainsPrintableValidCharacters())
            {
                NewRelic.Api.Agent.NewRelic.RecordCustomEvent("Custom/CreateUserInvalidCharacters", [
                    new("ExternalId", identity.GetExternalUserId() ?? string.Empty),
                    new("Username", newUser.Username)
                ]);
                NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/CreateUserInvalidCharacters", 1);
                return Results.BadRequest();
            }

            User user = new(newUser.Username, identity.GetExternalUserId()!);
            await db.Users.AddAsync(user, ct);
            await db.SaveChangesAsync(ct);

            NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/CreateUser", 1);

            return Results.Created($"/api/users/{user.Id}", user.ToFilteredUser());
        })
    .WithDisplayName("CreateUser")
    .WithDescription("Creates a new user")
    .RequireAuthorization(nameof(NoopAuthorizationRequirement));

RouteGroupBuilder follows = app.MapGroup("/api/follows").WithTags("Follows").RequireAuthorization()
    .RequireRateLimiting("default");
follows.MapGet("/",
        async ([FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
            CancellationToken ct) =>
        {
            User? user = await identity.GetUserAsync(ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            List<DetailedFollower> results = await db.Followers
                .AsNoTracking()
                .Where(x => x.FollowerId == user.Id)
                .Join(
                    db.Users,
                    f => f.FollowerId,
                    u => u.Id,
                    (f, u) => new DetailedFollower(f.FollowerId, u.Username))
                .ToListAsync(ct);

            return Results.Ok(results);
        })
    .WithDisplayName("GetFollowing")
    .WithDescription("Gets the current user's followings");
follows.MapPost("/{userId:guid}",
        async (Guid userId, [FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
            CancellationToken ct) =>
        {
            User? user = await identity.GetUserAsync(ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            bool isUserToFollowExists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct);
            if (!isUserToFollowExists)
            {
                return Results.NotFound();
            }

            if (user.Id == userId)
            {
                return Results.BadRequest();
            }

            bool isAlreadyFollowing =
                await db.Followers.AsNoTracking().AnyAsync(x => x.UserId == user.Id && x.FollowerId == userId, ct);
            if (isAlreadyFollowing)
            {
                return Results.BadRequest();
            }

            Follower follower = new(user.Id, userId);
            await db.Followers.AddAsync(follower, ct);
            await db.SaveChangesAsync(ct);
            return Results.Created("/api/followers", follower.Id);
        })
    .WithDisplayName("FollowUser")
    .WithDescription("Follows a user");
follows.MapDelete("/{userId:guid}",
        async (Guid userId, [FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
            CancellationToken ct) =>
        {
            User? user = await identity.GetUserAsync(ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            bool isUserToUnfollowExists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct);
            if (!isUserToUnfollowExists)
            {
                return Results.NotFound();
            }

            Follower? follow =
                await db.Followers
                    .FirstOrDefaultAsync(x => x.UserId == user.Id && x.FollowerId == userId, ct);
            if (follow is null)
            {
                return Results.BadRequest();
            }

            db.Followers.Remove(follow);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
    .WithDisplayName("UnfollowUser")
    .WithDescription("Unfollows a user");

RouteGroupBuilder posts = app.MapGroup("/api/posts").WithTags("Posts").RequireAuthorization()
    .RequireRateLimiting("default");
posts.MapGet("/",
        async ([FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
            CancellationToken ct, [FromQuery] string type = "all") =>
        {
            User? user = await identity.GetUserAsync(ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            type = type.ToUpper(CultureInfo.InvariantCulture);
            List<Post> results = type switch
            {
                "USER" => await db.Posts.AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Where(x => x.UserId == user.Id)
                    .ToListAsync(ct),
                "FOLLOWING" => await db.Posts.AsNoTracking()
                    .OrderByDescending(x => x.CreatedAt)
                    .Join(db.Followers,
                        p => p.UserId,
                        f => f.FollowerId,
                        (p, f) => new { p, f })
                    .Where(x => x.f.FollowerId == user.Id)
                    .Select(x => x.p)
                    .ToListAsync(ct),
                _ => await db.Posts.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(ct)
            };
            return Results.Ok(results);
        })
    .WithDisplayName("GetPosts")
    .WithDescription("Get all posts");
posts.MapGet("/{postId:guid}",
        async (Guid postId, [FromServices] ConnectlyDbContext db, CancellationToken ct) =>
        {
            Post? post = await db.Posts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == postId, ct);
            return post is null ? Results.NotFound() : Results.Ok(post);
        })
    .WithDisplayName("GetPost")
    .WithDescription("Get a post by id");
posts.MapPost("/",
        async ([FromBody] NewPost newPost, [FromServices] ConnectlyDbContext db,
            [FromServices] IExternalIdentityService identity, CancellationToken ct) =>
        {
            if (string.IsNullOrEmpty(newPost.Content))
            {
                return Results.BadRequest();
            }

            User? user = await identity.GetUserAsync(ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            if (!newPost.Content.ContainsPrintableValidCharacters())
            {
                NewRelic.Api.Agent.NewRelic.RecordCustomEvent("Custom/CreatePostInvalidCharacters", [
                    new("UserId", user.Id),
                    new("Content", newPost.Content)
                ]);
                NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/CreatePostInvalidCharacters", 1);
                return Results.BadRequest();
            }

            Post post = new(newPost.Content, user.Id);
            await db.Posts.AddAsync(post, ct);
            await db.SaveChangesAsync(ct);

            NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/CreatePost", 1);

            return Results.Created($"/api/posts/{post.Id}", post.Id);
        })
    .WithDisplayName("CreatePost")
    .WithDescription("Create a new post");
posts.MapDelete("/{postId:guid}",
        async (Guid postId, [FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
            CancellationToken ct) =>
        {
            User? user = await identity.GetUserAsync(ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            Post? post = await db.Posts.FirstOrDefaultAsync(x => x.Id == postId, ct);
            if (post is null)
            {
                return Results.NotFound();
            }

            if (post.UserId != user.Id)
            {
                return Results.BadRequest();
            }

            db.Posts.Remove(post);
            await db.SaveChangesAsync(ct);

            NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/DeletePost", 1);

            return Results.NoContent();
        })
    .WithDisplayName("DeletePost")
    .WithDescription("Delete a post");

await app.RunAsync();