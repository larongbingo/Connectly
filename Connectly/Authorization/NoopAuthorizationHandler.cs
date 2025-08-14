using Microsoft.AspNetCore.Authorization;

namespace Connectly.Authorization;

public class NoopAuthorizationHandler : AuthorizationHandler<NoopAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        NoopAuthorizationRequirement requirement)
    {
        context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public class NoopAuthorizationRequirement : IAuthorizationRequirement;