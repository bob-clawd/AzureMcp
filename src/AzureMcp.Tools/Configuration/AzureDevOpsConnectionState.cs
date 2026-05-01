namespace AzureMcp.Tools.Configuration;

internal sealed class AzureDevOpsConnectionState : IAzureDevOpsConnectionState
{
    public string? ConfigPath { get; }

    public AzureDevOpsConnectionState(string? configPath)
    {
        ConfigPath = string.IsNullOrWhiteSpace(configPath)
            ? null
            : Path.GetFullPath(configPath);
    }

    public bool TryGetRequired(out AzureDevOpsConnectionInfo connection, out ErrorInfo? error)
    {
        var configRead = TryReadConfigFile();
        if (configRead.Error is not null)
        {
            connection = default!;
            error = configRead.Error;
            return false;
        }

        var configOrg = NormalizeUrl(configRead.OrganizationUrl);
        var pat = NormalizeString(configRead.PersonalAccessToken);
        var project = NormalizeString(configRead.Project);

        if (!string.IsNullOrWhiteSpace(configOrg)
            && (!Uri.TryCreate(configOrg, UriKind.Absolute, out var configParsed)
                || (configParsed.Scheme != Uri.UriSchemeHttps && configParsed.Scheme != Uri.UriSchemeHttp)))
        {
            connection = default!;
            error = new ErrorInfo(
                $"AzureMcp config file is invalid. Config file: '{ConfigPath}'. The 'organizationUrl' must be an absolute http/https URL. Fix the file.",
                new Dictionary<string, string>
                {
                    ["path"] = ConfigPath ?? string.Empty,
                    ["organizationUrl"] = configOrg
                });
            return false;
        }

        var org = configOrg ?? AzureDevOpsDefaults.DefaultOrganizationUrl;

        if (!string.IsNullOrWhiteSpace(org)
            && (!Uri.TryCreate(org, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)))
        {
            connection = default!;
            error = new ErrorInfo(
                $"AzureMcp config file is invalid. Config file: '{ConfigPath}'. The 'organizationUrl' must be an absolute http/https URL. Fix the file.",
                new Dictionary<string, string>
                {
                    ["path"] = ConfigPath ?? string.Empty,
                    ["organizationUrl"] = org
                });
            return false;
        }

        connection = new AzureDevOpsConnectionInfo(
            OrganizationUrl: org!,
            PersonalAccessToken: pat,
            Project: project);

        error = null;
        return true;
    }

    private (string? OrganizationUrl, string? PersonalAccessToken, string? Project, ErrorInfo? Error) TryReadConfigFile()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ConfigPath) || !File.Exists(ConfigPath))
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
                    "AzureMcp config file could not be read/parsed. Fix or replace the file.",
                    new Dictionary<string, string>
                    {
                        ["path"] = ConfigPath ?? string.Empty,
                        ["exception"] = ex.GetType().Name,
                        ["message"] = ex.Message
                    }));
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
