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
    public void ProjectFilesPackPublicNuGetMetadata()
    {
        var cliProjectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Wickd.Cli",
            "Wickd.Cli.csproj"));
        var coreProjectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Wickd.Core",
            "Wickd.Core.csproj"));
        var adapterProjectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Wickd.Adapters.Ccxt",
            "Wickd.Adapters.Ccxt.csproj"));
        var propsPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "Directory.Build.props"));
        var cliProject = XDocument.Load(cliProjectPath);
        var coreProject = XDocument.Load(coreProjectPath);
        var adapterProject = XDocument.Load(adapterProjectPath);
        var props = XDocument.Load(propsPath);
        var cliProjectText = File.ReadAllText(cliProjectPath);

        Assert.Equal("true", cliProject.Descendants("PackAsTool").Single().Value);
        Assert.Equal("wickd", cliProject.Descendants("ToolCommandName").Single().Value);
        Assert.Equal("wickd", cliProject.Descendants("PackageId").Single().Value);
        Assert.Equal("Wickd.Core", coreProject.Descendants("PackageId").Single().Value);
        Assert.Equal("Wickd.Adapters.Ccxt", adapterProject.Descendants("PackageId").Single().Value);
        Assert.Equal("0.1.0-preview.1", props.Descendants("WickdVersion").Single().Value);
        Assert.Equal("WickdAlgo, DevBD1", props.Descendants("Authors").Single().Value);
        Assert.Equal("https://github.com/WickdAlgo/wickd-core", props.Descendants("RepositoryUrl").Single().Value);
        Assert.Equal("https://github.com/WickdAlgo/wickd-core", props.Descendants("PackageProjectUrl").Single().Value);
        Assert.Equal("Apache-2.0", props.Descendants("PackageLicenseExpression").Single().Value);
        Assert.Contains("appsettings.defaults.json", cliProjectText);
        Assert.Contains("markets.defaults.json", cliProjectText);
        Assert.DoesNotContain("appsettings.Local.json", cliProjectText);
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
