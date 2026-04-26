using System.Reflection;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace AzureMcp.Tools;

public static class ServiceExtensions
{
    public static IServiceCollection WithAzureMcp(
        this IServiceCollection services,
        string configPath)
    {
        return services
            .AddSingleton<IAzureDevOpsConnectionState>(new AzureDevOpsConnectionState(configPath))
            .AddInfrastructure()
            .AddImplementations<Tool>();
    }

    public static IEnumerable<Type> GetTools() => GetImplementations<Tool>();

    private static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient<IAzureDevOpsWorkItemClient, AzureDevOpsWorkItemClient>();
        services.AddHttpClient<IAzureDevOpsPullRequestClient, AzureDevOpsPullRequestClient>();

        return services;
    }

    private static IServiceCollection AddImplementations<T>(this IServiceCollection services)
    {
        foreach (var type in GetImplementations<T>())
            services.AddSingleton(type);

        return services;
    }

    private static IEnumerable<Type> GetImplementations<T>() => Assembly.GetExecutingAssembly()
        .GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(T)))
        .Distinct();
}
