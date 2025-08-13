using System.Reflection.Metadata;
using System.Security.Claims;
using Connectly.Application.Identity;
using Connectly.Authorization;
using Connectly.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ConnectlyDbContext>();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(async (doc, ctx, ct) =>
    {
        doc.Info.Title = "Connectly API";
        doc.Info.Version = "Alpha";
        doc.Info.Description = "A rewrite of the Connectly API in ASP.NET Core";

        doc.Components ??= new OpenApiComponents();
        doc.Components.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>()
        {
            ["Bearer"] = new()
            {
                Type = SecuritySchemeType.OAuth2,
                In = ParameterLocation.Header,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Token from Auth0",
                Flows = new OpenApiOAuthFlows()
                {
                    Implicit = new OpenApiOAuthFlow()
                    {
                        AuthorizationUrl = new Uri("https://ewan.au.auth0.com/authorize?audience=https://connectly-noobnoob"),
                        TokenUrl = new Uri("https://ewan.au.auth0.com/oauth/token"),
                        Scopes = new Dictionary<string, string>()
                        {
                            ["openid"] = "OpenID",
                        }
                    }
                }
            }
        };
    });
    
    options.AddOperationTransformer(async (op, ctx, ct) =>
    {
        var hasAuthorizeAttribute = ctx.Description.ActionDescriptor.EndpointMetadata.OfType<AuthorizeAttribute>().Any();
        if (hasAuthorizeAttribute)
        {
            op.Security ??= new List<OpenApiSecurityRequirement>();
            op.Security.Add(new OpenApiSecurityRequirement()
            {
                {
                    new OpenApiSecurityScheme()
                    {
                        Reference = new OpenApiReference()
                        {
                            Type = ReferenceType.SecurityScheme, Id = "Bearer"
                        }
                    }, 
                    ["openapi"]
                }
            });
        }
    });
    
    options.AddOperationTransformer(async (op, ctx, ct) =>
    {
        var displayName = ctx.Description.ActionDescriptor.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            return;
                
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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });
builder.Services.AddConnectlyAuthorization();


var app = builder.Build();

// The app mainly uses sqlite
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConnectlyDbContext>();
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

var users = app.MapGroup("/api/users").WithTags("Users");

users.MapGet("/", (ConnectlyDbContext db) => db.Users.AsNoTracking().ToList().Select(x => x.ToFilteredUser())).RequireAuthorization();
users.MapGet("/{id:guid}", (ConnectlyDbContext db, Guid id) => db.Users.AsNoTracking().FirstOrDefault(x => x.Id == id)).RequireAuthorization();
users.MapGet("/profile", async ([FromServices] IExternalIdentityService identity, CancellationToken ct) =>
{
    var user = await identity.GetUserAsync(ct);
    return user?.ToFilteredUser();
})
    .WithDisplayName("GetProfile")
    .WithDescription("Gets the current user's profile")
    .RequireAuthorization();
users.MapPost("/", async ([FromBody] NewUser newUser,[FromServices] ConnectlyDbContext db, [FromServices] IExternalIdentityService identity, CancellationToken ct) =>
{
    var isUsernameTaken = await db.Users.AsNoTracking().AnyAsync(x => x.Username == newUser.Username, cancellationToken: ct);
    var isExternalIdTaken = await db.Users.AsNoTracking().AnyAsync(x => x.ExternalId == identity.GetExternalUserId(), cancellationToken: ct);
    if (isUsernameTaken || isExternalIdTaken)
        return Results.BadRequest();
    
    var user = new User(newUser.Username, identity.GetExternalUserId());
    await db.Users.AddAsync(user, cancellationToken: ct);
    await db.SaveChangesAsync(cancellationToken: ct);
    return Results.Created($"/api/users/{user.Id}", user.ToFilteredUser());
})
    .WithDisplayName("CreateUser")
    .WithDescription("Creates a new user")
    .RequireAuthorization(nameof(NoopAuthorizationRequirement));

app.Run();
