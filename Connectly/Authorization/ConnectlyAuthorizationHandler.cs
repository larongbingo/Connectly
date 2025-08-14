using Connectly.Application.Identity;

using Microsoft.AspNetCore.Authorization;

namespace Connectly.Authorization;

public class ConnectlyAuthorizationHandler(IExternalIdentityService identityService)
    : AuthorizationHandler<ConnectlyAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ConnectlyAuthorizationRequirement requirement)
    {
        User? user = await identityService.GetUserAsync();
        if (user is null)
        {
            context.Fail(new AuthorizationFailureReason(this, "User must have an account and logged in"));
            return;
        }

        context.Succeed(requirement);
    }
}

public class ConnectlyAuthorizationRequirement : IAuthorizationRequirement;