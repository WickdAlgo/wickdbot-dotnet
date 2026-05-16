#nullable enable

using System.CommandLine;
using System.Diagnostics;
using WickdBot.Backtesting;
using WickdBot.Data;
using WickdBot.Engines;
using WickdBot.Infrastructure;
using WickdBot.Models;

namespace WickdBot;

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

    /// <summary>
    /// Runs the WickdBot command-line interface.
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
    /// Builds the root command and supported MVP subcommands.
    /// </summary>
    /// <param name="historicalDataSource">Historical data source used by fetch and backtest commands.</param>
    /// <param name="loadSettings">Settings loader used to resolve run commands.</param>
    /// <param name="backtestPipeline">Backtest pipeline used by the backtest command.</param>
    /// <returns>The configured root command.</returns>
    internal static RootCommand BuildRootCommand(
        HistoricalDataSource? historicalDataSource = null,
        Func<WickdBotSettings>? loadSettings = null,
        BacktestPipeline? backtestPipeline = null)
    {
        var dataSource = historicalDataSource ?? HistoricalDataSource.CreateDefault();
        var pipeline = backtestPipeline ?? new BacktestPipeline(dataSource);
        var settingsLoader = loadSettings ?? WickdBotConfigurationLoader.LoadDefault;
        var rootCommand = new RootCommand("WickdBot trading research CLI.");

        rootCommand.Subcommands.Add(CreateFetchCommand(dataSource, settingsLoader));
        rootCommand.Subcommands.Add(CreateBacktestCommand(pipeline, settingsLoader));
        rootCommand.Subcommands.Add(CreateAnalyzeCommand());

        return rootCommand;
    }

    /// <summary>
    /// Creates the fetch command that validates a historical candle request before placeholder execution.
    /// </summary>
    /// <param name="historicalDataSource">Historical data source used to load or fetch candles.</param>
    /// <param name="loadSettings">Settings loader used to resolve the request.</param>
    /// <returns>The configured fetch command.</returns>
    private static Command CreateFetchCommand(
        HistoricalDataSource historicalDataSource,
        Func<WickdBotSettings> loadSettings)
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
            var settings = LoadSettings(loadSettings);
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
            catch (Exception ex) when (ex is WickdBotDataException or DatasetAliasException)
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
        Func<WickdBotSettings> loadSettings)
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
            var settings = LoadSettings(loadSettings);
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
            catch (Exception ex) when (ex is WickdBotDataException or StructureException)
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
    /// <param name="settings">Loaded WickdBot settings.</param>
    /// <returns>The validated run request, or <see langword="null" /> when validation fails.</returns>
    private static RunRequest? ResolveRunRequest(
        ParseResult parseResult,
        Option<string?> marketOption,
        Option<string?> timeframeOption,
        Option<DateTimeOffset?> fromOption,
        Option<DateTimeOffset?> toOption,
        WickdBotSettings settings)
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
        catch (Exception ex) when (ex is WickdBotConfigurationException or ArgumentException)
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
    /// <param name="settings">Loaded WickdBot settings.</param>
    /// <param name="cancellationToken">Cancellation token for alias catalog I/O.</param>
    /// <returns>The resolved run request, or <see langword="null" /> when validation fails.</returns>
    private static async Task<RunRequest?> ResolveBacktestRunRequestAsync(
        ParseResult parseResult,
        Option<string?> marketOption,
        Option<string?> timeframeOption,
        Option<DateTimeOffset?> fromOption,
        Option<DateTimeOffset?> toOption,
        Option<string?> datasetOption,
        WickdBotSettings settings,
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
            catch (Exception ex) when (ex is DatasetAliasException or WickdBotConfigurationException or ArgumentException)
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
    /// <returns>The loaded settings, or <see langword="null" /> when loading fails.</returns>
    private static WickdBotSettings? LoadSettings(Func<WickdBotSettings> loadSettings)
    {
        try
        {
            return loadSettings();
        }
        catch (WickdBotConfigurationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }
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
