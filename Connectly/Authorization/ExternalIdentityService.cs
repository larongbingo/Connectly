using System.Security.Claims;

using Connectly.Application.Identity;
using Connectly.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace Connectly.Authorization;

public class ExternalIdentityService(IHttpContextAccessor httpContextAccessor, ConnectlyDbContext db)
    : IExternalIdentityService
{
    public string? GetExternalUserId()
    {
        return httpContextAccessor.HttpContext?.User.GetExternalId();
    }

    public async Task<User?> GetUserAsync(CancellationToken ct = default)
    {
        string? externalUserId = GetExternalUserId();

        if (externalUserId is null)
        {
            return null;
        }

        return await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.ExternalId == externalUserId, ct);
    }
}

public interface IExternalIdentityService
{
    string? GetExternalUserId();
    Task<User?> GetUserAsync(CancellationToken ct = default);
}

internal static class ClaimsPrincipalExtensions
{
    public static string? GetExternalId(this ClaimsPrincipal principal)
    {
        return principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
    }
}