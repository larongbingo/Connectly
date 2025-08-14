using System.Security.Claims;

using Connectly.Application.Follower;
using Connectly.Application.Identity;
using Connectly.Authorization;
using Connectly.Infrastructure.Data;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ConnectlyDbContext>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(async (doc, ctx, ct) =>
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
                        AuthorizationUrl =
                            new Uri("https://ewan.au.auth0.com/authorize?audience=https://connectly-noobnoob"),
                        TokenUrl = new Uri("https://ewan.au.auth0.com/oauth/token"),
                        Scopes = new Dictionary<string, string> { ["openid"] = "OpenID" }
                    }
                }
            }
        };
    });

    options.AddOperationTransformer(async (op, ctx, ct) =>
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
    });

    options.AddOperationTransformer(async (op, ctx, ct) =>
    {
        string? displayName = ctx.Description.ActionDescriptor.DisplayName;
        if (string.IsNullOrEmpty(displayName))
        {
            return;
        }

        op.Summary = displayName;
    });
});

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


WebApplication app = builder.Build();

// The app mainly uses sqlite
using (IServiceScope scope = app.Services.CreateScope())
{
    ConnectlyDbContext db = scope.ServiceProvider.GetRequiredService<ConnectlyDbContext>();
    db.Database.Migrate();
}


app.MapOpenApi("/swagger/connectly.json");
app.UseSwaggerUI(options =>
{
    options.OAuthClientId("xPKdcBAqzJ2YgrbRPtyNYEPFfFqpBnf3");
    options.SwaggerEndpoint("/swagger/connectly.json", "Connectly API");
});

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();

RouteGroupBuilder users = app.MapGroup("/api/users").WithTags("Users");

users.MapGet("/", (ConnectlyDbContext db) => db.Users.AsNoTracking().ToList().Select(x => x.ToFilteredUser()))
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

            User user = new(newUser.Username, identity.GetExternalUserId());
            await db.Users.AddAsync(user, ct);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/users/{user.Id}", user.ToFilteredUser());
        })
    .WithDisplayName("CreateUser")
    .WithDescription("Creates a new user")
    .RequireAuthorization(nameof(NoopAuthorizationRequirement));

RouteGroupBuilder followers = app.MapGroup("/api/followers").WithTags("Followers").RequireAuthorization();
followers.MapGet("/",
        async ([FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
            CancellationToken ct) =>
        {
            User? user = await identity.GetUserAsync(ct);
            return await db.Followers
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .Join(
                    db.Users,
                    f => f.FollowerId,
                    u => u.Id,
                    (f, u) => new DetailedFollower(f.FollowerId, u.Username))
                .ToListAsync(ct);
        })
    .WithDisplayName("GetFollowers")
    .WithDescription("Gets the current user's followers");
followers.MapPost("/{userId:guid}",
    async (Guid userId, [FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
        CancellationToken ct) =>
    {
        var user = await identity.GetUserAsync(ct);
        var isUserToFollowExists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct);
        if (!isUserToFollowExists)
            return Results.NotFound();

        var isAlreadyFollowing =
            await db.Followers.AsNoTracking().AnyAsync(x => x.UserId == user.Id && x.FollowerId == userId, ct);
        if (isAlreadyFollowing)
            return Results.BadRequest();
        
        var follower = new Follower(user.Id, userId);
        await db.Followers.AddAsync(follower, ct);
        await db.SaveChangesAsync(ct);
        return Results.Created("/api/followers", follower.Id);
    })
    .WithDisplayName("FollowUser")
    .WithDescription("Follows a user");
followers.MapDelete("/{userId:guid}", 
    async (Guid userId, [FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity,
        CancellationToken ct) =>
    {
        var user = await identity.GetUserAsync(ct);
        var isUserToUnfollowExists = await db.Users.AsNoTracking().AnyAsync(x => x.Id == userId, ct);
        if (!isUserToUnfollowExists)
            return Results.NotFound();

        var isAlreadyFollowing =
            await db.Followers.AsNoTracking().AnyAsync(x => x.UserId == user.Id && x.FollowerId == userId, ct);
        if (!isAlreadyFollowing)
            return Results.BadRequest();
        
        var follower = new Follower(user.Id, userId);
        db.Followers.Remove(follower);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    })
    .WithDisplayName("UnfollowUser")
    .WithDescription("Unfollows a user");

app.Run();