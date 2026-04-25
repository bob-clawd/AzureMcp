namespace AzureMcp.Tools.Configuration;

internal sealed class AzureDevOpsConnectionState : IAzureDevOpsConnectionState
{
    private readonly object _gate = new();

    public string ConfigPath { get; }

    private string? _organizationUrl;
    private string? _personalAccessToken;
    private string? _project;

    public AzureDevOpsConnectionState(string configPath, string? organizationUrl, string? personalAccessToken, string? project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
        ConfigPath = Path.GetFullPath(configPath);
        _organizationUrl = NormalizeUrl(organizationUrl);
        _personalAccessToken = NormalizeString(personalAccessToken);
        _project = NormalizeString(project);
    }

    public void Set(string? organizationUrl, string? personalAccessToken, string? project)
    {
        lock (_gate)
        {
            _organizationUrl = NormalizeUrl(organizationUrl) ?? _organizationUrl;
            _personalAccessToken = NormalizeString(personalAccessToken) ?? _personalAccessToken;
            _project = NormalizeString(project) ?? _project;
        }
    }

    public AzureDevOpsConnectionInfo GetRequired()
    {
        if (TryGetRequired(out var connection, out _, out _))
            return connection;

        throw new InvalidOperationException("Configuration is missing. Use TryGetRequired to get the actionable error.");
    }

    public bool TryGetRequired(
        out AzureDevOpsConnectionInfo connection,
        out ErrorInfo? error,
        out IReadOnlyList<string>? missingEnvironmentVariables)
    {
        lock (_gate)
        {
            var org = _organizationUrl;
            var pat = _personalAccessToken;
            var project = _project;

            if (org is null || pat is null || project is null)
            {
                var configRead = TryReadConfigFile();
                if (configRead.Error is not null)
                {
                    connection = default!;
                    error = configRead.Error;
                    missingEnvironmentVariables = null;
                    return false;
                }

                org ??= NormalizeUrl(configRead.OrganizationUrl);
                pat ??= NormalizeString(configRead.PersonalAccessToken);
                project ??= NormalizeString(configRead.Project);
            }

            org ??= NormalizeUrl(Environment.GetEnvironmentVariable("AZURE_MCP_ORGANIZATION_URL"));
            pat ??= NormalizeString(Environment.GetEnvironmentVariable("AZURE_MCP_PAT"));
            project ??= NormalizeString(Environment.GetEnvironmentVariable("AZURE_MCP_PROJECT"));

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(org)) missing.Add("AZURE_MCP_ORGANIZATION_URL");
            if (string.IsNullOrWhiteSpace(pat)) missing.Add("AZURE_MCP_PAT");

            if (missing.Count > 0)
            {
                connection = default!;
                error = AzureMcpErrors.MissingConfig(missing);
                missingEnvironmentVariables = missing;
                return false;
            }

            connection = new AzureDevOpsConnectionInfo(
                OrganizationUrl: org!,
                PersonalAccessToken: pat!,
                Project: project);

            error = null;
            missingEnvironmentVariables = null;
            return true;
        }
    }

    public bool TryPersist(out ErrorInfo? error)
    {
        lock (_gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var existing = TryReadConfigFile();
                if (existing.Error is not null)
                {
                    // If the existing config is unreadable, we still allow rewriting it with current state.
                    existing = (null, null, null, null);
                }

                var data = new ConfigFile
                {
                    OrganizationUrl = _organizationUrl ?? NormalizeUrl(existing.OrganizationUrl),
                    PersonalAccessToken = _personalAccessToken ?? NormalizeString(existing.PersonalAccessToken),
                    Project = _project ?? NormalizeString(existing.Project)
                };

                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true
                });

                File.WriteAllText(ConfigPath, json);

                TrySetSecureFileMode(ConfigPath);

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = new ErrorInfo(
                    "Failed to write AzureMcp config file",
                    new Dictionary<string, string>
                    {
                        ["path"] = ConfigPath,
                        ["exception"] = ex.GetType().Name,
                        ["message"] = ex.Message
                    });

                return false;
            }
        }
    }

    private (string? OrganizationUrl, string? PersonalAccessToken, string? Project, ErrorInfo? Error) TryReadConfigFile()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return (null, null, null, null);

            var json = File.ReadAllText(ConfigPath);
            if (string.IsNullOrWhiteSpace(json))
                return (null, null, null, null);

            var data = System.Text.Json.JsonSerializer.Deserialize<ConfigFile>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return (data?.OrganizationUrl, data?.PersonalAccessToken, data?.Project, null);
        }
        catch (Exception ex)
        {
            return (null, null, null,
                new ErrorInfo(
                    "AzureMcp config file could not be read/parsed. Ask the user for the values and call `configure_connection` to rewrite the file.",
                    new Dictionary<string, string>
                    {
                        ["path"] = ConfigPath,
                        ["exception"] = ex.GetType().Name,
                        ["message"] = ex.Message
                    }));
        }
    }

    private static void TrySetSecureFileMode(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // best-effort only
        }
    }

    private sealed class ConfigFile
    {
        public string? OrganizationUrl { get; set; }

        public string? PersonalAccessToken { get; set; }

        public string? Project { get; set; }
    }

    private static string? NormalizeString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeUrl(string? value)
    {
        value = NormalizeString(value);
        return value is null ? null : value.TrimEnd('/');
    }
}
