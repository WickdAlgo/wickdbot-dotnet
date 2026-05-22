using System.Xml.Linq;
using Wickd.Infrastructure;

namespace Wickd.Tests;

/// <summary>
/// Verifies NuGet-tool configuration distribution behavior.
/// </summary>
public class ConfigurationDistributionTests
{
    /// <summary>
    /// Confirms explicit command-line config paths win over environment and user defaults.
    /// </summary>
    [Fact]
    public void ResolveAppSettingsPathPrefersExplicitPath()
    {
        using var directory = new TemporaryDirectory();
        var explicitPath = Path.Combine(directory.DirectoryPath, "explicit", "appsettings.json");
        var environmentPath = Path.Combine(directory.DirectoryPath, "environment", "appsettings.json");
        var userDirectory = Path.Combine(directory.DirectoryPath, "user");

        WithConfigEnvironment(environmentPath, () =>
        {
            var resolvedPath = WickdConfigurationLoader.ResolveAppSettingsPath(explicitPath, userDirectory);

            Assert.Equal(Path.GetFullPath(explicitPath), resolvedPath);
        });
    }

    /// <summary>
    /// Confirms the WICKD_CONFIG environment variable wins over user defaults.
    /// </summary>
    [Fact]
    public void ResolveAppSettingsPathPrefersEnvironmentPathOverUserDefault()
    {
        using var directory = new TemporaryDirectory();
        var environmentPath = Path.Combine(directory.DirectoryPath, "environment", "appsettings.json");
        var userDirectory = Path.Combine(directory.DirectoryPath, "user");

        WithConfigEnvironment(environmentPath, () =>
        {
            var resolvedPath = WickdConfigurationLoader.ResolveAppSettingsPath(null, userDirectory);

            Assert.Equal(Path.GetFullPath(environmentPath), resolvedPath);
            Assert.False(File.Exists(Path.Combine(userDirectory, "appsettings.json")));
        });
    }

    /// <summary>
    /// Confirms missing user-profile configuration is initialized from bundled defaults.
    /// </summary>
    [Fact]
    public void ResolveAppSettingsPathInitializesMissingUserConfiguration()
    {
        using var directory = new TemporaryDirectory();
        var userDirectory = Path.Combine(directory.DirectoryPath, "user");

        WithConfigEnvironment(null, () =>
        {
            var resolvedPath = WickdConfigurationLoader.ResolveAppSettingsPath(null, userDirectory);
            var settings = WickdConfigurationLoader.Load(resolvedPath);

            Assert.Equal(Path.Combine(userDirectory, "appsettings.json"), resolvedPath);
            Assert.True(File.Exists(Path.Combine(userDirectory, "markets.json")));
            Assert.Equal("BTC_USDT_PERP", settings.DefaultMarketId);
        });
    }

    /// <summary>
    /// Confirms config initialization never overwrites user-edited files by default.
    /// </summary>
    [Fact]
    public void InitializeUserConfigurationDoesNotOverwriteExistingFiles()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = directory.WriteFile("appsettings.json", "{ \"custom\": true }");
        var marketsPath = directory.WriteFile("markets.json", "{ \"markets\": [] }");

        WickdConfigurationLoader.InitializeUserConfiguration(directory.DirectoryPath);

        Assert.Equal("{ \"custom\": true }", File.ReadAllText(appSettingsPath));
        Assert.Equal("{ \"markets\": [] }", File.ReadAllText(marketsPath));
    }

    /// <summary>
    /// Confirms local appsettings overrides are optional for distributed users.
    /// </summary>
    [Fact]
    public void InitializedUserConfigurationLoadsWithoutLocalOverride()
    {
        using var directory = new TemporaryDirectory();
        var paths = WickdConfigurationLoader.InitializeUserConfiguration(directory.DirectoryPath);

        var settings = WickdConfigurationLoader.Load(paths.AppSettingsPath);

        Assert.False(File.Exists(Path.Combine(directory.DirectoryPath, "appsettings.Local.json")));
        Assert.Equal("BTC_USDT_PERP", settings.DefaultMarketId);
    }

    /// <summary>
    /// Confirms the CLI can initialize config files at an explicit path.
    /// </summary>
    [Fact]
    public async Task ConfigInitCreatesExplicitConfigurationFiles()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = Path.Combine(directory.DirectoryPath, "custom", "Wickd.json");
        var command = Program.BuildRootCommand();

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "config",
                "init",
                "--config",
                appSettingsPath
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Created appsettings config", result.Output);
        Assert.True(File.Exists(appSettingsPath));
        Assert.True(File.Exists(Path.Combine(directory.DirectoryPath, "custom", "markets.json")));
    }

    /// <summary>
    /// Confirms the CLI prints the active explicit config path.
    /// </summary>
    [Fact]
    public async Task ConfigPathPrintsExplicitConfigurationPath()
    {
        using var directory = new TemporaryDirectory();
        var appSettingsPath = Path.Combine(directory.DirectoryPath, "appsettings.json");
        var command = Program.BuildRootCommand();

        var result = await CommandLineTestRunner.InvokeCommandAsync(
            command,
            [
                "config",
                "path",
                "--config",
                appSettingsPath
            ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(Path.GetFullPath(appSettingsPath), result.Output);
    }

    /// <summary>
    /// Confirms package metadata is configured for a .NET tool without private local settings.
    /// </summary>
    [Fact]
    public void ProjectFilePacksDotnetToolDefaultsWithoutLocalSettings()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Wickd.Cli",
            "Wickd.Cli.csproj"));
        var project = XDocument.Load(projectPath);
        var projectText = File.ReadAllText(projectPath);

        Assert.Equal("true", project.Descendants("PackAsTool").Single().Value);
        Assert.Equal("wickd", project.Descendants("ToolCommandName").Single().Value);
        Assert.Equal("Wickd.Cli", project.Descendants("PackageId").Single().Value);
        Assert.Contains("appsettings.defaults.json", projectText);
        Assert.Contains("markets.defaults.json", projectText);
        Assert.DoesNotContain("appsettings.Local.json", projectText);
    }

    /// <summary>
    /// Runs an assertion while temporarily controlling WICKD_CONFIG.
    /// </summary>
    /// <param name="value">Temporary environment variable value.</param>
    /// <param name="assert">Assertion to run.</param>
    private static void WithConfigEnvironment(string? value, Action assert)
    {
        var previous = Environment.GetEnvironmentVariable(WickdConfigurationLoader.ConfigEnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(WickdConfigurationLoader.ConfigEnvironmentVariableName, value);
            assert();
        }
        finally
        {
            Environment.SetEnvironmentVariable(WickdConfigurationLoader.ConfigEnvironmentVariableName, previous);
        }
    }
}
