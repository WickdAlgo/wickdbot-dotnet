namespace Wickd.Infrastructure;

/// <summary>
/// Describes the resolved Wickd user configuration files.
/// </summary>
/// <param name="ConfigDirectory">Directory that owns the configuration files.</param>
/// <param name="AppSettingsPath">Resolved appsettings.json path.</param>
/// <param name="MarketsPath">Resolved markets.json path.</param>
internal sealed record WickdConfigurationPaths(
    string ConfigDirectory,
    string AppSettingsPath,
    string MarketsPath);
