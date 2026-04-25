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

        string? organizationUrl = null;
        string? project = null;
        string? personalAccessToken = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
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
                    throw new ArgumentException($"Unknown argument '{argument}'. Expected '--organization-url <url>' '--pat <token>' and optional '--project <name>'.", nameof(args));
            }
        }

        organizationUrl ??= environmentVariables["AZURE_MCP_ORGANIZATION_URL"] as string;
        project ??= environmentVariables["AZURE_MCP_PROJECT"] as string;
        personalAccessToken ??= environmentVariables["AZURE_MCP_PAT"] as string;

        if (string.IsNullOrWhiteSpace(organizationUrl))
            throw new ArgumentException("Missing Azure DevOps organization URL. Use '--organization-url <url>' or set 'AZURE_MCP_ORGANIZATION_URL'.", nameof(args));

        if (!Uri.TryCreate(organizationUrl, UriKind.Absolute, out var parsedOrganizationUrl)
            || (parsedOrganizationUrl.Scheme != Uri.UriSchemeHttps && parsedOrganizationUrl.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException($"Invalid organization URL '{organizationUrl}'.", nameof(args));
        }

        if (string.IsNullOrWhiteSpace(personalAccessToken))
            throw new ArgumentException("Missing Azure DevOps PAT. Use '--pat <token>' or set 'AZURE_MCP_PAT'.", nameof(args));

        return new AzureMcpOptions
        {
            OrganizationUrl = parsedOrganizationUrl.ToString().TrimEnd('/'),
            Project = string.IsNullOrWhiteSpace(project) ? null : project.Trim(),
            PersonalAccessToken = personalAccessToken.Trim()
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
