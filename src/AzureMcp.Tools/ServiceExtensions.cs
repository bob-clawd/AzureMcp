using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using AzureMcp.Tools.Configuration;
using AzureMcp.Tools.WorkItems;
using Microsoft.Extensions.DependencyInjection;

namespace AzureMcp.Tools;

public static class ServiceExtensions
{
    public static IServiceCollection WithAzureMcp(this IServiceCollection services, AzureMcpOptions options) => services
        .AddSingleton(options)
        .AddInfrastructure(options)
        .AddImplementations<Tool>();

    public static IEnumerable<Type> GetTools() => GetImplementations<Tool>();

    private static IServiceCollection AddInfrastructure(this IServiceCollection services, AzureMcpOptions options)
    {
        services.AddHttpClient<IAzureDevOpsWorkItemClient, AzureDevOpsWorkItemClient>(client =>
        {
            client.BaseAddress = new Uri(options.OrganizationUrl + "/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var token = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        });

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
