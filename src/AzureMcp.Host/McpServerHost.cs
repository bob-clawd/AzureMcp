using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AzureMcp.Host;

public static class McpServerHost
{
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = ParseOptions(args, Environment.GetEnvironmentVariables());
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services.Compose(options);

        var host = builder.Build();
        await host.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static AzureMcpOptions ParseOptions(string[] args, System.Collections.IDictionary environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environmentVariables);

        string? configPath = null;
        string? organizationUrl = null;
        string? project = null;
        string? personalAccessToken = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--config":
                    configPath = ReadSingleValueArgument(args, ref index, argument, configPath);
                    break;
                case "--organization-url":
                    organizationUrl = ReadSingleValueArgument(args, ref index, argument, organizationUrl);
                    break;
                case "--project":
                    project = ReadSingleValueArgument(args, ref index, argument, project);
                    break;
                case "--pat":
                    personalAccessToken = ReadSingleValueArgument(args, ref index, argument, personalAccessToken);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{argument}'. Expected '--config <path>' plus optional '--organization-url <url>' '--pat <token>' and '--project <name>'.", nameof(args));
            }
        }

        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Missing required option '--config <path>'.", nameof(args));

        configPath = Path.GetFullPath(configPath);

        organizationUrl ??= environmentVariables["AZURE_MCP_ORGANIZATION_URL"] as string;
        project ??= environmentVariables["AZURE_MCP_PROJECT"] as string;
        personalAccessToken ??= environmentVariables["AZURE_MCP_PAT"] as string;

        string? normalizedOrganizationUrl = null;
        if (!string.IsNullOrWhiteSpace(organizationUrl))
        {
            if (!Uri.TryCreate(organizationUrl, UriKind.Absolute, out var parsedOrganizationUrl)
                || (parsedOrganizationUrl.Scheme != Uri.UriSchemeHttps && parsedOrganizationUrl.Scheme != Uri.UriSchemeHttp))
            {
                throw new ArgumentException($"Invalid organization URL '{organizationUrl}'.", nameof(args));
            }

            normalizedOrganizationUrl = parsedOrganizationUrl.ToString().TrimEnd('/');
        }

        return new AzureMcpOptions
        {
            ConfigPath = configPath,
            OrganizationUrl = normalizedOrganizationUrl,
            Project = string.IsNullOrWhiteSpace(project) ? null : project.Trim(),
            PersonalAccessToken = string.IsNullOrWhiteSpace(personalAccessToken) ? null : personalAccessToken.Trim()
        };
    }

    private static string ReadSingleValueArgument(string[] args, ref int index, string argumentName, string? currentValue)
    {
        if (currentValue is not null)
            throw new ArgumentException($"The '{argumentName}' option may only be specified once.", nameof(args));

        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for '{argumentName}'. Expected '{argumentName} <value>'.", nameof(args));

        var value = args[++index];
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"The '{argumentName}' value must not be empty or whitespace.", nameof(args));

        return value;
    }
}
