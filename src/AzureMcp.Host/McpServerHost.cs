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

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--config":
                    configPath = ReadSingleValueArgument(args, ref index, argument, configPath);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{argument}'. Expected '--config <path>'.", nameof(args));
            }
        }

        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Missing required option '--config <path>'.", nameof(args));

        configPath = Path.GetFullPath(configPath);

        return new AzureMcpOptions
        {
            ConfigPath = configPath,
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
