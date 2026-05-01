using System.Text;
using AzureMcp.Tools.Configuration;

namespace AzureMcp.Tools.Tests.Host;

public sealed class AzureDevOpsConnectionStateTests
{
    [Fact]
    public void TryGetRequired_UsesDefaults_WhenNoConfigPathProvided()
    {
        var state = new AzureDevOpsConnectionState(null);

        var ok = state.TryGetRequired(out var connection, out var error);

        ok.IsTrue();
        error.IsNull();
        connection.OrganizationUrl.Is(AzureDevOpsDefaults.DefaultOrganizationUrl);
        connection.PersonalAccessToken.IsNull();
        connection.Project.IsNull();
    }

    [Fact]
    public void TryGetRequired_UsesDefaults_WhenConfigFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"azuremcp-{Guid.NewGuid():N}.json");
        var state = new AzureDevOpsConnectionState(path);

        var ok = state.TryGetRequired(out var connection, out var error);

        ok.IsTrue();
        error.IsNull();
        connection.OrganizationUrl.Is(AzureDevOpsDefaults.DefaultOrganizationUrl);
        connection.PersonalAccessToken.IsNull();
        connection.Project.IsNull();
    }

    [Fact]
    public void TryGetRequired_ConfigOverridesDefaults_WhenFileExists()
    {
        var path = WriteTempFile("""
        {
          "organizationUrl": "https://ado.contoso.local/tfs/DefaultCollection",
          "personalAccessToken": "secret-pat",
          "project": "Demo"
        }
        """);

        try
        {
            var state = new AzureDevOpsConnectionState(path);

            var ok = state.TryGetRequired(out var connection, out var error);

            ok.IsTrue();
            error.IsNull();
            connection.OrganizationUrl.Is("https://ado.contoso.local/tfs/DefaultCollection");
            connection.PersonalAccessToken.Is("secret-pat");
            connection.Project.Is("Demo");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetRequired_Fails_WhenConfigJsonIsMalformed()
    {
        var path = WriteTempFile("{ this is not json }");

        try
        {
            var state = new AzureDevOpsConnectionState(path);

            var ok = state.TryGetRequired(out _, out var error);

            ok.IsFalse();
            error.IsNotNull();
            error!.Message.IsContaining("could not be read/parsed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TryGetRequired_Fails_WhenConfigOrganizationUrlIsInvalid()
    {
        var path = WriteTempFile("""
        {
          "organizationUrl": "not-a-url"
        }
        """);

        try
        {
            var state = new AzureDevOpsConnectionState(path);

            var ok = state.TryGetRequired(out _, out var error);

            ok.IsFalse();
            error.IsNotNull();
            error!.Message.IsContaining("organizationUrl");
            error.Message.IsContaining("absolute http/https URL");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"azuremcp-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}
