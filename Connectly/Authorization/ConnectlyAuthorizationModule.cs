using Microsoft.AspNetCore.Authorization;

namespace Connectly.Authorization;

public static class ConnectlyAuthorizationModule
{
    public static IServiceCollection AddConnectlyAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(nameof(ConnectlyAuthorizationRequirement), 
                policy => policy.Requirements.Add(new ConnectlyAuthorizationRequirement()));
            options.AddPolicy(nameof(NoopAuthorizationRequirement), 
                policy => policy.Requirements.Add(new NoopAuthorizationRequirement()));
            options.DefaultPolicy = options.GetPolicy(nameof(ConnectlyAuthorizationRequirement));
        });
        services.AddScoped<IExternalIdentityService, ExternalIdentityService>();
        services.AddScoped<IAuthorizationHandler, ConnectlyAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, NoopAuthorizationHandler>();
        services.AddHttpContextAccessor();
        return services;
    }
}