using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AzureMcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace AzureMcp.Host;

public static class HostExtensions
{
    internal static string ServerVersion => Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(HostExtensions).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    internal static IServiceCollection Compose(this IServiceCollection services, AzureMcpOptions options) => services
        .WithAzureMcp(options.OrganizationUrl, options.PersonalAccessToken, options.Project)
        .AddMcpRuntime();

    private static IServiceCollection AddMcpRuntime(this IServiceCollection services)
    {
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            WriteIndented = true
        };

        var builder = services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = "AzureMcp",
                Version = ServerVersion
            };
        });

        builder.WithStdioServerTransport();
        builder.WithTools(ServiceExtensions.GetTools(), serializerOptions);

        return services;
    }
}
