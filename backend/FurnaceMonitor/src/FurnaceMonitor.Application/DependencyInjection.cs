using Microsoft.Extensions.DependencyInjection;

namespace FurnaceMonitor.Application;

/// <summary>
/// Registers application-layer dependencies with the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Services are registered in the Infrastructure layer because they
        // depend on concrete repositories / DbContext. This extension exists
        // for future app-layer-only registrations & FluentValidation setup.
        return services;
    }
}
