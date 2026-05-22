#nullable enable

using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using Wickd.Adapters.Ccxt;
using Wickd.Backtesting;
using Wickd.Data;
using Wickd.Engines;
using Wickd.Infrastructure;
using Wickd.Models;

namespace Wickd;

/// <summary>
/// Application entry point and command-line interface wiring.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Exit code returned by command skeletons that parse successfully but are not implemented yet.
    /// </summary>
    internal const int NotImplementedExitCode = 1;

    /// <summary>
    /// Exit code returned when command arguments or configuration fail validation.
    /// </summary>
    internal const int ValidationErrorExitCode = 2;

    private delegate WickdSettings SettingsLoader(ParseResult parseResult);

    /// <summary>
    /// Runs the Wickd command-line interface.
    /// </summary>
    /// <param name="args">Command-line arguments supplied by the host process.</param>
    /// <returns>The process exit code.</returns>
    internal static Task<int> Main(string[] args)
    {
        return InvokeWithTimingAsync(BuildRootCommand(), args);
    }

    /// <summary>
    /// Invokes a root command and writes elapsed-time feedback after it completes.
    /// </summary>
    /// <param name="command">Root command to invoke.</param>
    /// <param name="args">Command-line arguments supplied by the caller.</param>
    /// <returns>The process exit code.</returns>
    internal static async Task<int> InvokeWithTimingAsync(RootCommand command, string[] args)
    {
        return await InvokeWithTimingAsync(() => command.Parse(args).InvokeAsync());
    }

    /// <summary>
    /// Invokes a command delegate and writes elapsed-time feedback after it completes.
    /// </summary>
    /// <param name="invoke">Command invocation delegate.</param>
    /// <returns>The process exit code.</returns>
    internal static async Task<int> InvokeWithTimingAsync(Func<Task<int>> invoke)
    {
        var stopwatch = Stopwatch.StartNew();
        int? exitCode = null;

        try
        {
            exitCode = await invoke();

            return exitCode.Value;
        }
        finally
        {
            stopwatch.Stop();
            WriteTimingResult(stopwatch.Elapsed, exitCode);
        }
    }

    /// <summary>
    /// Formats an elapsed command duration for terminal output.
    /// </summary>
    /// <param name="elapsed">Elapsed command duration.</param>
    /// <returns>Human-readable elapsed duration.</returns>
    internal static string FormatElapsedDuration(TimeSpan elapsed)
    {
        var roundedMilliseconds = (long)Math.Round(elapsed.TotalMilliseconds, MidpointRounding.AwayFromZero);
        if (roundedMilliseconds < 1000)
        {
            return FormattableString.Invariant($"{roundedMilliseconds} ms");
        }

        var roundedSeconds = Math.Round(elapsed.TotalSeconds, 2, MidpointRounding.AwayFromZero);
        if (roundedSeconds < 60)
        {
            return FormattableString.Invariant($"{roundedSeconds:0.00} s");
        }

        var wholeSeconds = (long)Math.Round(elapsed.TotalSeconds, MidpointRounding.AwayFromZero);
        var hours = wholeSeconds / 3600;
        var minutes = wholeSeconds % 3600 / 60;
        var seconds = wholeSeconds % 60;
        if (hours > 0)
        {
            return FormattableString.Invariant($"{hours} h {minutes:00} min {seconds:00} s");
        }

        return FormattableString.Invariant($"{minutes} min {seconds:00} s");
    }

    /// <summary>
    /// Formats a UTC timestamp for stable command output.
    /// </summary>
    /// <param name="timestampUtc">UTC timestamp to format.</param>
    /// <returns>The formatted UTC timestamp.</returns>
    internal static string FormatUtc(DateTimeOffset timestampUtc)
    {
        return timestampUtc
            .ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a byte count for compact command output.
    /// </summary>
    /// <param name="bytes">Byte count to format.</param>
    /// <returns>The formatted byte count.</returns>
    internal static string FormatByteCount(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (decimal)Math.Max(0, bytes);
        var unitIndex = 0;
        while (value >= 1024m && unitIndex < units.Length - 1)
        {
            value /= 1024m;
            unitIndex++;
        }

        return unitIndex == 0
            ? FormattableString.Invariant($"{value:0} {units[unitIndex]}")
            : FormattableString.Invariant($"{value:0.0} {units[unitIndex]}");
    }

    /// <summary>
    /// Resolves a candidate path and ensures it remains below the configured root.
    /// </summary>
    /// <param name="rootPath">Configured artifact root.</param>
    /// <param name="candidatePath">Candidate artifact path.</param>
    /// <param name="description">Artifact description used in diagnostics.</param>
    /// <returns>The resolved artifact path.</returns>
    /// <exception cref="WickdDataException">Thrown when the candidate path escapes the root.</exception>
    private static string ResolvePathUnderRoot(
        string rootPath,
        string candidatePath,
        string description)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new WickdDataException($"{description} root is required.");
        }

        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            throw new WickdDataException($"{description} path is required.");
        }

        var resolvedRoot = Path.GetFullPath(rootPath);
        var resolvedCandidate = Path.GetFullPath(candidatePath);
        var normalizedRoot = Path.TrimEndingDirectorySeparator(resolvedRoot);
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
        if (string.Equals(normalizedRoot, resolvedCandidate, GetPathComparison())
            || !resolvedCandidate.StartsWith(rootWithSeparator, GetPathComparison()))
        {
            throw new WickdDataException(
                $"Refusing to delete {description} outside the configured root: {resolvedCandidate}");
        }

        return resolvedCandidate;
    }

    /// <summary>
    /// Gets the appropriate path comparison for the current operating system.
    /// </summary>
    /// <returns>The path string comparison.</returns>
    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    /// <summary>
    /// Builds the root command and supported MVP subcommands.
    /// </summary>
    /// <param name="historicalDataSource">Historical data source used by fetch and backtest commands.</param>
    /// <param name="loadSettings">Settings loader used to resolve run commands.</param>
    /// <param name="backtestPipeline">Backtest pipeline used by the backtest command.</param>
    /// <returns>The configured root command.</returns>
    internal static RootCommand BuildRootCommand(
        HistoricalDataSource? historicalDataSource = null,
        Func<WickdSettings>? loadSettings = null,
        BacktestPipeline? backtestPipeline = null)
    {
        var dataSource = historicalDataSource ?? CreateDefaultHistoricalDataSource();
        var pipeline = backtestPipeline ?? new BacktestPipeline(dataSource);
        var configOption = CreateConfigOption();
        SettingsLoader settingsLoader = loadSettings is null
            ? parseResult => WickdConfigurationLoader.LoadResolved(parseResult.GetValue(configOption))
            : _ => loadSettings();
        var rootCommand = new RootCommand("Wickd trading research CLI.");
        rootCommand.Options.Add(configOption);

        rootCommand.Subcommands.Add(CreateFetchCommand(dataSource, settingsLoader));
        rootCommand.Subcommands.Add(CreateBacktestCommand(pipeline, settingsLoader));
        rootCommand.Subcommands.Add(CreateManageCommand(settingsLoader));
        rootCommand.Subcommands.Add(CreateConfigCommand(configOption));
        rootCommand.Subcommands.Add(CreateAnalyzeCommand());

        return rootCommand;
    }

    /// <summary>
    /// Creates the production historical data source for currently supported CLI exchanges.
    /// </summary>
    /// <returns>The configured production historical data source.</returns>
    private static HistoricalDataSource CreateDefaultHistoricalDataSource()
    {
        return new HistoricalDataSource([new CcxtBinanceMarketDataClient()]);
    }

    /// <summary>
    /// Creates the fetch command that validates a historical candle request before placeholder execution.
    /// </summary>
    /// <param name="historicalDataSource">Historical data source used to load or fetch candles.</param>
    /// <param name="loadSettings">Settings loader used to resolve the request.</param>
    /// <returns>The configured fetch command.</returns>
    private static Command CreateFetchCommand(
        HistoricalDataSource historicalDataSource,
        SettingsLoader loadSettings)
    {
        var marketOption = CreateMarketOption(required: true);
        var timeframeOption = CreateTimeframeOption(required: true);
        var fromOption = CreateFromOption(required: true);
        var toOption = CreateToOption(required: true);
        var aliasOption = CreateDatasetAliasOption();
        var forceOption = CreateForceOption();

        var command = new Command("fetch", "Fetch and cache historical candles for one market and timeframe.");
        command.Options.Add(marketOption);
        command.Options.Add(timeframeOption);
        command.Options.Add(fromOption);
        command.Options.Add(toOption);
        command.Options.Add(aliasOption);
        command.Options.Add(forceOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var settings = LoadSettings(loadSettings, parseResult);
            if (settings is null)
            {
                return ValidationErrorExitCode;
            }

            var request = ResolveRunRequest(
                parseResult,
                marketOption,
                timeframeOption,
                fromOption,
                toOption,
                settings);
            if (request is null)
            {
                return ValidationErrorExitCode;
            }

            try
            {
                var result = await historicalDataSource.LoadOrFetchAsync(request, cancellationToken);
                var alias = parseResult.GetValue(aliasOption);
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    var catalog = DatasetAliasCatalog.CreateDefault(settings);
                    var savedAlias = await catalog.SaveAsync(
                        alias,
                        result,
                        parseResult.GetValue(forceOption),
                        cancellationToken);
                    WriteDatasetAliasResult(savedAlias);
                }

                WriteFetchResult(result);
                return 0;
            }
            catch (Exception ex) when (ex is WickdDataException or DatasetAliasException)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the backtest command that validates a deterministic run request before placeholder execution.
    /// </summary>
    /// <param name="backtestPipeline">Backtest pipeline used to replay cached candles and write structure events.</param>
    /// <param name="loadSettings">Settings loader used to resolve the request.</param>
    /// <returns>The configured backtest command.</returns>
    private static Command CreateBacktestCommand(
        BacktestPipeline backtestPipeline,
        SettingsLoader loadSettings)
    {
        var marketOption = CreateMarketOption(required: false);
        var timeframeOption = CreateTimeframeOption(required: false);
        var fromOption = CreateFromOption(required: false);
        var toOption = CreateToOption(required: false);
        var datasetOption = CreateDatasetOption();
        var runIdOption = CreateBacktestRunIdOption();

        var command = new Command("backtest", "Run a deterministic backtest for one market and timeframe.");
        command.Options.Add(marketOption);
        command.Options.Add(timeframeOption);
        command.Options.Add(fromOption);
        command.Options.Add(toOption);
        command.Options.Add(datasetOption);
        command.Options.Add(runIdOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var settings = LoadSettings(loadSettings, parseResult);
            if (settings is null)
            {
                return ValidationErrorExitCode;
            }

            var request = await ResolveBacktestRunRequestAsync(
                parseResult,
                marketOption,
                timeframeOption,
                fromOption,
                toOption,
                datasetOption,
                settings,
                cancellationToken);
            if (request is null)
            {
                return ValidationErrorExitCode;
            }

            var runId = parseResult.GetValue(runIdOption);
            if (string.IsNullOrWhiteSpace(runId))
            {
                runId = GenerateRunId(DateTimeOffset.UtcNow);
            }

            try
            {
                var result = await backtestPipeline.RunAsync(request, runId, settings, cancellationToken);
                WriteBacktestResult(result);
                return 0;
            }
            catch (Exception ex) when (ex is WickdDataException or StructureException)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the manage command for local datasets and run artifacts.
    /// </summary>
    /// <param name="loadSettings">Settings loader used to find local artifact roots.</param>
    /// <returns>The configured manage command.</returns>
    private static Command CreateManageCommand(SettingsLoader loadSettings)
    {
        var command = new Command("manage", "Manage local dataset aliases, cached datasets, and run outputs.");
        command.Subcommands.Add(CreateManageDatasetsCommand("datasets", loadSettings));
        command.Subcommands.Add(CreateManageDatasetsCommand("aliases", loadSettings));
        command.Subcommands.Add(CreateManageRunsCommand(loadSettings));

        return command;
    }

    /// <summary>
    /// Creates a dataset-alias management command.
    /// </summary>
    /// <param name="commandName">Command name to expose.</param>
    /// <param name="loadSettings">Settings loader used to find the dataset alias catalog.</param>
    /// <returns>The configured datasets command.</returns>
    private static Command CreateManageDatasetsCommand(
        string commandName,
        SettingsLoader loadSettings)
    {
        var command = new Command(commandName, "List and delete dataset aliases and cached candle files.");
        command.Subcommands.Add(CreateManageDatasetsListCommand(loadSettings));
        command.Subcommands.Add(CreateManageDatasetsDeleteCommand(loadSettings));

        return command;
    }

    /// <summary>
    /// Creates the dataset alias list command.
    /// </summary>
    /// <param name="loadSettings">Settings loader used to find the dataset alias catalog.</param>
    /// <returns>The configured dataset list command.</returns>
    private static Command CreateManageDatasetsListCommand(SettingsLoader loadSettings)
    {
        var command = new Command("list", "List saved dataset aliases.");
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var settings = LoadSettings(loadSettings, parseResult);
            if (settings is null)
            {
                return ValidationErrorExitCode;
            }

            try
            {
                var aliases = await DatasetAliasCatalog
                    .CreateDefault(settings)
                    .LoadAsync(cancellationToken);
                WriteDatasetAliasList(aliases);
                return 0;
            }
            catch (DatasetAliasException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the dataset alias delete command.
    /// </summary>
    /// <param name="loadSettings">Settings loader used to find the dataset alias catalog.</param>
    /// <returns>The configured dataset delete command.</returns>
    private static Command CreateManageDatasetsDeleteCommand(SettingsLoader loadSettings)
    {
        var aliasOption = new Option<string>("--alias")
        {
            Description = "Dataset alias to delete.",
            Required = true
        };
        var deleteCacheOption = new Option<bool>("--delete-cache")
        {
            Description = "Also delete the cached candle file when no remaining alias references it."
        };

        var command = new Command("delete", "Delete a dataset alias and optionally its cached candle file.");
        command.Options.Add(aliasOption);
        command.Options.Add(deleteCacheOption);
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var settings = LoadSettings(loadSettings, parseResult);
            if (settings is null)
            {
                return ValidationErrorExitCode;
            }

            var alias = parseResult.GetRequiredValue(aliasOption);
            try
            {
                var catalog = DatasetAliasCatalog.CreateDefault(settings);
                var deleteCache = parseResult.GetValue(deleteCacheOption);
                if (deleteCache)
                {
                    var existing = await catalog.ResolveAsync(alias, cancellationToken);
                    _ = ResolvePathUnderRoot(
                        settings.CacheRoot,
                        existing.CandleCachePath,
                        "dataset cache file");
                }

                var result = await catalog.DeleteAsync(alias, cancellationToken);
                WriteDatasetAliasDeletionResult(result);

                if (deleteCache)
                {
                    DeleteDatasetCacheFile(settings, result);
                }

                return 0;
            }
            catch (Exception ex) when (ex is DatasetAliasException or WickdDataException)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the run management command.
    /// </summary>
    /// <param name="loadSettings">Settings loader used to find the run-output root.</param>
    /// <returns>The configured runs command.</returns>
    private static Command CreateManageRunsCommand(SettingsLoader loadSettings)
    {
        var command = new Command("runs", "List and delete local backtest run outputs.");
        command.Subcommands.Add(CreateManageRunsListCommand(loadSettings));
        command.Subcommands.Add(CreateManageRunsDeleteCommand(loadSettings));

        return command;
    }

    /// <summary>
    /// Creates the run list command.
    /// </summary>
    /// <param name="loadSettings">Settings loader used to find the run-output root.</param>
    /// <returns>The configured run list command.</returns>
    private static Command CreateManageRunsListCommand(SettingsLoader loadSettings)
    {
        var command = new Command("list", "List local backtest run outputs.");
        command.SetAction(parseResult =>
        {
            var settings = LoadSettings(loadSettings, parseResult);
            if (settings is null)
            {
                return ValidationErrorExitCode;
            }

            try
            {
                WriteRunArtifactList(RunArtifactCatalog.List(settings.RunsRoot));
                return 0;
            }
            catch (WickdDataException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the run delete command.
    /// </summary>
    /// <param name="loadSettings">Settings loader used to find the run-output root.</param>
    /// <returns>The configured run delete command.</returns>
    private static Command CreateManageRunsDeleteCommand(SettingsLoader loadSettings)
    {
        var runIdOption = new Option<string>("--run-id")
        {
            Description = "Run ID whose output directory should be deleted.",
            Required = true
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Confirm recursive deletion of the run output directory."
        };

        var command = new Command("delete", "Delete a local backtest run output directory.");
        command.Options.Add(runIdOption);
        command.Options.Add(forceOption);
        command.SetAction(parseResult =>
        {
            var settings = LoadSettings(loadSettings, parseResult);
            if (settings is null)
            {
                return ValidationErrorExitCode;
            }

            if (!parseResult.GetValue(forceOption))
            {
                Console.Error.WriteLine("Deleting a run output directory requires --force.");
                return ValidationErrorExitCode;
            }

            var runId = parseResult.GetRequiredValue(runIdOption);
            try
            {
                var result = RunArtifactCatalog.Delete(settings.RunsRoot, runId);
                WriteRunDeletionResult(result);
                return result.Deleted ? 0 : ValidationErrorExitCode;
            }
            catch (WickdDataException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the configuration management command.
    /// </summary>
    /// <param name="configOption">Global appsettings path option.</param>
    /// <returns>The configured config command.</returns>
    private static Command CreateConfigCommand(Option<string?> configOption)
    {
        var command = new Command("config", "Manage Wickd user configuration.");
        command.Subcommands.Add(CreateConfigInitCommand(configOption));
        command.Subcommands.Add(CreateConfigPathCommand(configOption));

        return command;
    }

    /// <summary>
    /// Creates the command that initializes editable user configuration files.
    /// </summary>
    /// <param name="configOption">Global appsettings path option.</param>
    /// <returns>The configured config init command.</returns>
    private static Command CreateConfigInitCommand(Option<string?> configOption)
    {
        var command = new Command("init", "Create editable Wickd configuration files.");
        command.SetAction(parseResult =>
        {
            try
            {
                var paths = ResolveConfigInitTarget(parseResult.GetValue(configOption));
                var appSettingsExisted = File.Exists(paths.AppSettingsPath);
                var marketsExisted = File.Exists(paths.MarketsPath);

                WickdConfigurationLoader.InitializeUserConfiguration(
                    paths.ConfigDirectory,
                    overwrite: false,
                    appSettingsFileName: Path.GetFileName(paths.AppSettingsPath));

                WriteConfigInitResult(paths.AppSettingsPath, appSettingsExisted, "appsettings");
                WriteConfigInitResult(paths.MarketsPath, marketsExisted, "markets");

                return 0;
            }
            catch (WickdConfigurationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the command that prints the active appsettings path.
    /// </summary>
    /// <param name="configOption">Global appsettings path option.</param>
    /// <returns>The configured config path command.</returns>
    private static Command CreateConfigPathCommand(Option<string?> configOption)
    {
        var command = new Command("path", "Print the active Wickd appsettings path.");
        command.SetAction(parseResult =>
        {
            try
            {
                Console.WriteLine(WickdConfigurationLoader.ResolveAppSettingsPath(parseResult.GetValue(configOption)));

                return 0;
            }
            catch (WickdConfigurationException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ValidationErrorExitCode;
            }
        });

        return command;
    }

    /// <summary>
    /// Creates the analyze command for future run journal analysis.
    /// </summary>
    /// <returns>The configured analyze command.</returns>
    private static Command CreateAnalyzeCommand()
    {
        var runIdOption = new Option<string>("--run-id")
        {
            Description = "Run ID whose journals should be analyzed.",
            Required = true
        };

        var command = new Command("analyze", "Analyze an existing backtest run.");
        command.Options.Add(runIdOption);
        command.SetAction(parseResult =>
        {
            _ = parseResult.GetRequiredValue(runIdOption);

            return ReturnNotImplemented("analyze");
        });

        return command;
    }

    /// <summary>
    /// Creates the global appsettings path option.
    /// </summary>
    /// <returns>The configured config option.</returns>
    private static Option<string?> CreateConfigOption()
    {
        return new Option<string?>("--config")
        {
            Description = "Path to the Wickd appsettings.json file.",
            Recursive = true
        };
    }

    /// <summary>
    /// Creates the shared market option used by run commands.
    /// </summary>
    /// <param name="required">Whether the option is required by the parser.</param>
    /// <returns>The configured market option.</returns>
    private static Option<string?> CreateMarketOption(bool required)
    {
        return new Option<string?>("--market")
        {
            Description = "Canonical market ID from markets.json.",
            Required = required
        };
    }

    /// <summary>
    /// Creates the shared timeframe option used by run commands.
    /// </summary>
    /// <param name="required">Whether the option is required by the parser.</param>
    /// <returns>The configured timeframe option.</returns>
    private static Option<string?> CreateTimeframeOption(bool required)
    {
        return new Option<string?>("--timeframe")
        {
            Description = "Candle timeframe, such as 5m.",
            Required = required
        };
    }

    /// <summary>
    /// Creates the shared inclusive UTC start time option used by run commands.
    /// </summary>
    /// <param name="required">Whether the option is required by the parser.</param>
    /// <returns>The configured start time option.</returns>
    private static Option<DateTimeOffset?> CreateFromOption(bool required)
    {
        return new Option<DateTimeOffset?>("--from")
        {
            Description = "Inclusive UTC start time for the requested candle window.",
            Required = required
        };
    }

    /// <summary>
    /// Creates the shared exclusive UTC end time option used by run commands.
    /// </summary>
    /// <param name="required">Whether the option is required by the parser.</param>
    /// <returns>The configured end time option.</returns>
    private static Option<DateTimeOffset?> CreateToOption(bool required)
    {
        return new Option<DateTimeOffset?>("--to")
        {
            Description = "Exclusive UTC end time for the requested candle window.",
            Required = required
        };
    }

    /// <summary>
    /// Creates the optional fetch alias option.
    /// </summary>
    /// <returns>The configured alias option.</returns>
    private static Option<string?> CreateDatasetAliasOption()
    {
        return new Option<string?>("--alias")
        {
            Description = "Optional friendly dataset alias saved for this fetched range."
        };
    }

    /// <summary>
    /// Creates the optional overwrite flag for alias saves.
    /// </summary>
    /// <returns>The configured force option.</returns>
    private static Option<bool> CreateForceOption()
    {
        return new Option<bool>("--force")
        {
            Description = "Overwrite an existing dataset alias."
        };
    }

    /// <summary>
    /// Creates the optional backtest dataset alias option.
    /// </summary>
    /// <returns>The configured dataset option.</returns>
    private static Option<string?> CreateDatasetOption()
    {
        return new Option<string?>("--dataset")
        {
            Description = "Dataset alias previously saved by fetch --alias."
        };
    }

    /// <summary>
    /// Creates the optional backtest run ID option.
    /// </summary>
    /// <returns>The configured run ID option.</returns>
    private static Option<string?> CreateBacktestRunIdOption()
    {
        return new Option<string?>("--run-id")
        {
            Description = "Optional run ID assigned to replayed backtest candles."
        };
    }

    /// <summary>
    /// Resolves command-line parse results into a validated run request.
    /// </summary>
    /// <param name="parseResult">The parsed command-line result.</param>
    /// <param name="marketOption">The market option to read.</param>
    /// <param name="timeframeOption">The timeframe option to read.</param>
    /// <param name="fromOption">The start time option to read.</param>
    /// <param name="toOption">The end time option to read.</param>
    /// <param name="settings">Loaded Wickd settings.</param>
    /// <returns>The validated run request, or <see langword="null" /> when validation fails.</returns>
    private static RunRequest? ResolveRunRequest(
        ParseResult parseResult,
        Option<string?> marketOption,
        Option<string?> timeframeOption,
        Option<DateTimeOffset?> fromOption,
        Option<DateTimeOffset?> toOption,
        WickdSettings settings)
    {
        try
        {
            var marketId = GetRequiredOptionValue(parseResult, marketOption, "--market");
            var timeframe = GetRequiredOptionValue(parseResult, timeframeOption, "--timeframe");
            var fromUtc = GetRequiredOptionValue(parseResult, fromOption, "--from");
            var toUtc = GetRequiredOptionValue(parseResult, toOption, "--to");

            return RunRequestFactory.Create(
                settings,
                marketId,
                timeframe,
                fromUtc,
                toUtc);
        }
        catch (Exception ex) when (ex is WickdConfigurationException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Resolves a backtest request from either a dataset alias or explicit run options.
    /// </summary>
    /// <param name="parseResult">The parsed command-line result.</param>
    /// <param name="marketOption">The market option to read.</param>
    /// <param name="timeframeOption">The timeframe option to read.</param>
    /// <param name="fromOption">The start time option to read.</param>
    /// <param name="toOption">The end time option to read.</param>
    /// <param name="datasetOption">The dataset alias option to read.</param>
    /// <param name="settings">Loaded Wickd settings.</param>
    /// <param name="cancellationToken">Cancellation token for alias catalog I/O.</param>
    /// <returns>The resolved run request, or <see langword="null" /> when validation fails.</returns>
    private static async Task<RunRequest?> ResolveBacktestRunRequestAsync(
        ParseResult parseResult,
        Option<string?> marketOption,
        Option<string?> timeframeOption,
        Option<DateTimeOffset?> fromOption,
        Option<DateTimeOffset?> toOption,
        Option<string?> datasetOption,
        WickdSettings settings,
        CancellationToken cancellationToken)
    {
        var datasetAlias = parseResult.GetValue(datasetOption);
        if (!string.IsNullOrWhiteSpace(datasetAlias))
        {
            if (IsSpecified(parseResult, marketOption)
                || IsSpecified(parseResult, timeframeOption)
                || IsSpecified(parseResult, fromOption)
                || IsSpecified(parseResult, toOption))
            {
                Console.Error.WriteLine("Use either --dataset or explicit --market/--timeframe/--from/--to options, not both.");
                return null;
            }

            try
            {
                return await DatasetAliasCatalog
                    .CreateDefault(settings)
                    .ResolveRunRequestAsync(datasetAlias, settings, cancellationToken);
            }
            catch (Exception ex) when (ex is DatasetAliasException or WickdConfigurationException or ArgumentException)
            {
                Console.Error.WriteLine(ex.Message);
                return null;
            }
        }

        return ResolveRunRequest(
            parseResult,
            marketOption,
            timeframeOption,
            fromOption,
            toOption,
            settings);
    }

    /// <summary>
    /// Loads settings and converts configuration failures into command validation output.
    /// </summary>
    /// <param name="loadSettings">Settings loader to invoke.</param>
    /// <param name="parseResult">The parsed command-line result.</param>
    /// <returns>The loaded settings, or <see langword="null" /> when loading fails.</returns>
    private static WickdSettings? LoadSettings(SettingsLoader loadSettings, ParseResult parseResult)
    {
        try
        {
            return loadSettings(parseResult);
        }
        catch (WickdConfigurationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Resolves where config init should create editable files.
    /// </summary>
    /// <param name="explicitAppSettingsPath">Optional appsettings path from --config.</param>
    /// <returns>The target configuration paths.</returns>
    private static WickdConfigurationPaths ResolveConfigInitTarget(string? explicitAppSettingsPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitAppSettingsPath))
        {
            var appSettingsPath = Path.GetFullPath(explicitAppSettingsPath);
            var directory = Path.GetDirectoryName(appSettingsPath)
                ?? throw new WickdConfigurationException($"Cannot resolve directory for {appSettingsPath}.");

            return WickdConfigurationLoader.GetConfigurationPaths(directory, Path.GetFileName(appSettingsPath));
        }

        var environmentPath = Environment.GetEnvironmentVariable(WickdConfigurationLoader.ConfigEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            var appSettingsPath = Path.GetFullPath(environmentPath);
            var directory = Path.GetDirectoryName(appSettingsPath)
                ?? throw new WickdConfigurationException($"Cannot resolve directory for {appSettingsPath}.");

            return WickdConfigurationLoader.GetConfigurationPaths(directory, Path.GetFileName(appSettingsPath));
        }

        return WickdConfigurationLoader.GetUserConfigurationPaths();
    }

    /// <summary>
    /// Writes whether a config init target was created or already existed.
    /// </summary>
    /// <param name="path">Configuration file path.</param>
    /// <param name="existed">Whether the file existed before initialization.</param>
    /// <param name="description">Short file description.</param>
    private static void WriteConfigInitResult(string path, bool existed, string description)
    {
        var action = existed ? "Found existing" : "Created";
        Console.WriteLine($"{action} {description} config: {path}");
    }

    /// <summary>
    /// Gets a required nullable value option.
    /// </summary>
    /// <typeparam name="T">Option value type.</typeparam>
    /// <param name="parseResult">The parsed command-line result.</param>
    /// <param name="option">The option to read.</param>
    /// <param name="optionName">Option name used in diagnostics.</param>
    /// <returns>The option value.</returns>
    /// <exception cref="ArgumentException">Thrown when the option is absent.</exception>
    private static T GetRequiredOptionValue<T>(
        ParseResult parseResult,
        Option<T?> option,
        string optionName)
        where T : struct
    {
        var value = parseResult.GetValue(option);
        if (value is null)
        {
            throw new ArgumentException($"Option '{optionName}' is required.");
        }

        return value.Value;
    }

    /// <summary>
    /// Gets a required nullable string option.
    /// </summary>
    /// <param name="parseResult">The parsed command-line result.</param>
    /// <param name="option">The option to read.</param>
    /// <param name="optionName">Option name used in diagnostics.</param>
    /// <returns>The option value.</returns>
    /// <exception cref="ArgumentException">Thrown when the option is absent.</exception>
    private static string GetRequiredOptionValue(
        ParseResult parseResult,
        Option<string?> option,
        string optionName)
    {
        var value = parseResult.GetValue(option);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Option '{optionName}' is required.");
        }

        return value;
    }

    /// <summary>
    /// Checks whether an option was explicitly specified.
    /// </summary>
    /// <param name="parseResult">The parsed command-line result.</param>
    /// <param name="option">The option to inspect.</param>
    /// <returns><see langword="true" /> when the option was specified.</returns>
    private static bool IsSpecified(ParseResult parseResult, Option option)
    {
        return parseResult.GetResult(option) is not null;
    }

    /// <summary>
    /// Writes the standard elapsed-time command footer.
    /// </summary>
    /// <param name="elapsed">Elapsed command duration.</param>
    /// <param name="exitCode">Command exit code.</param>
    private static void WriteTimingResult(TimeSpan elapsed, int? exitCode)
    {
        var exitCodeText = exitCode.HasValue
            ? $"exit code {exitCode.Value}"
            : "exit code unavailable";

        Console.Error.WriteLine($"Finished in {FormatElapsedDuration(elapsed)} ({exitCodeText}).");
    }

    /// <summary>
    /// Writes the fetch command result summary.
    /// </summary>
    /// <param name="result">Historical data result to report.</param>
    private static void WriteFetchResult(HistoricalDataResult result)
    {
        var action = result.CacheHit ? "Loaded" : "Fetched";
        var cacheState = result.CacheHit ? "cache hit" : "cache miss";

        Console.WriteLine(
            $"{action} {result.CandleCount} candles at {result.CachePath} ({cacheState}, gaps {result.Gaps.Count}).");
    }

    /// <summary>
    /// Writes the dataset alias save summary.
    /// </summary>
    /// <param name="datasetAlias">Saved dataset alias to report.</param>
    private static void WriteDatasetAliasResult(DatasetAlias datasetAlias)
    {
        Console.WriteLine(
            $"Saved dataset alias '{datasetAlias.Alias}' for {datasetAlias.MarketId} {datasetAlias.Timeframe}.");
    }

    /// <summary>
    /// Writes the saved dataset alias list.
    /// </summary>
    /// <param name="aliases">Dataset aliases to report.</param>
    private static void WriteDatasetAliasList(IReadOnlyList<DatasetAlias> aliases)
    {
        if (aliases.Count == 0)
        {
            Console.WriteLine("No dataset aliases found.");
            return;
        }

        Console.WriteLine("Dataset aliases:");
        foreach (var alias in aliases)
        {
            Console.WriteLine(
                $"{alias.Alias} | {alias.MarketId} | {alias.ExchangeId} | {alias.Timeframe} | {FormatUtc(alias.FromUtc)} -> {FormatUtc(alias.ToUtc)} | {alias.CandleCachePath}");
        }
    }

    /// <summary>
    /// Writes the dataset alias deletion result.
    /// </summary>
    /// <param name="result">Alias deletion result to report.</param>
    private static void WriteDatasetAliasDeletionResult(DatasetAliasDeletionResult result)
    {
        Console.WriteLine($"Deleted dataset alias '{result.DeletedAlias.Alias}'.");
    }

    /// <summary>
    /// Deletes a dataset cache file when it is not still referenced by another alias.
    /// </summary>
    /// <param name="settings">Loaded Wickd settings.</param>
    /// <param name="result">Alias deletion result that identifies the candidate cache file.</param>
    /// <exception cref="WickdDataException">Thrown when cache deletion is unsafe or fails.</exception>
    private static void DeleteDatasetCacheFile(
        WickdSettings settings,
        DatasetAliasDeletionResult result)
    {
        var cachePath = result.DeletedAlias.CandleCachePath;
        var isStillReferenced = result.RemainingAliases.Any(alias =>
            string.Equals(
                Path.GetFullPath(alias.CandleCachePath),
                Path.GetFullPath(cachePath),
                GetPathComparison()));
        if (isStillReferenced)
        {
            Console.WriteLine($"Left shared dataset cache file in place: {cachePath}");
            return;
        }

        var resolvedCachePath = ResolvePathUnderRoot(
            settings.CacheRoot,
            cachePath,
            "dataset cache file");
        if (!File.Exists(resolvedCachePath))
        {
            Console.WriteLine($"Dataset cache file was already missing: {resolvedCachePath}");
            return;
        }

        try
        {
            File.Delete(resolvedCachePath);
            Console.WriteLine($"Deleted dataset cache file: {resolvedCachePath}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WickdDataException(
                $"Could not delete dataset cache file: {resolvedCachePath}. {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Writes the local run artifact list.
    /// </summary>
    /// <param name="runs">Run artifacts to report.</param>
    private static void WriteRunArtifactList(IReadOnlyList<RunArtifact> runs)
    {
        if (runs.Count == 0)
        {
            Console.WriteLine("No runs found.");
            return;
        }

        Console.WriteLine("Runs:");
        foreach (var run in runs)
        {
            Console.WriteLine(
                $"{run.RunId} | files {run.FileCount} | {FormatByteCount(run.TotalBytes)} | updated {FormatUtc(run.LastWriteTimeUtc)} | {run.DirectoryPath}");
        }
    }

    /// <summary>
    /// Writes the run deletion result.
    /// </summary>
    /// <param name="result">Run deletion result to report.</param>
    private static void WriteRunDeletionResult(RunDeletionResult result)
    {
        if (result.Deleted)
        {
            Console.WriteLine($"Deleted run '{result.RunId}' at {result.DirectoryPath}.");
            return;
        }

        Console.Error.WriteLine($"Run '{result.RunId}' was not found at {result.DirectoryPath}.");
    }

    /// <summary>
    /// Writes the Phase 3 backtest result summary.
    /// </summary>
    /// <param name="result">Backtest pipeline result to report.</param>
    private static void WriteBacktestResult(BacktestPipelineResult result)
    {
        Console.WriteLine(
            $"Replayed {result.ReplayResult.CandleCount} candles for run '{result.RunId}' from {result.ReplayResult.CachePath} (gaps {result.ReplayResult.Gaps.Count}). Wrote {result.StructureResult.EventCount} structure events to {result.StructuresPath}. Setup and trade execution are not implemented yet.");
    }

    /// <summary>
    /// Generates a simple UTC run ID for backtest commands that do not provide one.
    /// </summary>
    /// <param name="utcNow">UTC timestamp used in the run ID.</param>
    /// <returns>The generated run ID.</returns>
    private static string GenerateRunId(DateTimeOffset utcNow)
    {
        return FormattableString.Invariant($"run-{utcNow:yyyyMMddTHHmmssZ}");
    }

    /// <summary>
    /// Writes the standard placeholder message for command skeletons.
    /// </summary>
    /// <param name="commandName">The command name to report.</param>
    /// <returns>The placeholder exit code.</returns>
    private static int ReturnNotImplemented(string commandName)
    {
        Console.Error.WriteLine($"Command '{commandName}' is not implemented yet.");
        return NotImplementedExitCode;
    }
}
