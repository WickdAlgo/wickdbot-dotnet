#nullable enable

using System.CommandLine;
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
    internal static int Main(string[] args)
    {
        return BuildRootCommand().Parse(args).Invoke();
    }

    /// <summary>
    /// Builds the root command and supported MVP subcommands.
    /// </summary>
    /// <returns>The configured root command.</returns>
    internal static RootCommand BuildRootCommand()
    {
        var rootCommand = new RootCommand("WickdBot trading research CLI.");

        rootCommand.Subcommands.Add(CreateFetchCommand());
        rootCommand.Subcommands.Add(CreateBacktestCommand());
        rootCommand.Subcommands.Add(CreateAnalyzeCommand());

        return rootCommand;
    }

    private static Command CreateFetchCommand()
    {
        var marketOption = CreateMarketOption();
        var timeframeOption = CreateTimeframeOption();
        var fromOption = CreateFromOption();
        var toOption = CreateToOption();

        var command = new Command("fetch", "Fetch and cache historical candles for one market and timeframe.");
        command.Options.Add(marketOption);
        command.Options.Add(timeframeOption);
        command.Options.Add(fromOption);
        command.Options.Add(toOption);
        command.SetAction(parseResult =>
        {
            var request = ResolveRunRequest(parseResult, marketOption, timeframeOption, fromOption, toOption);
            if (request is null)
            {
                return ValidationErrorExitCode;
            }

            return ReturnNotImplemented("fetch");
        });

        return command;
    }

    private static Command CreateBacktestCommand()
    {
        var marketOption = CreateMarketOption();
        var timeframeOption = CreateTimeframeOption();
        var fromOption = CreateFromOption();
        var toOption = CreateToOption();

        var command = new Command("backtest", "Run a deterministic backtest for one market and timeframe.");
        command.Options.Add(marketOption);
        command.Options.Add(timeframeOption);
        command.Options.Add(fromOption);
        command.Options.Add(toOption);
        command.SetAction(parseResult =>
        {
            var request = ResolveRunRequest(parseResult, marketOption, timeframeOption, fromOption, toOption);
            if (request is null)
            {
                return ValidationErrorExitCode;
            }

            return ReturnNotImplemented("backtest");
        });

        return command;
    }

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

    private static Option<string> CreateMarketOption()
    {
        return new Option<string>("--market")
        {
            Description = "Canonical market ID from markets.json.",
            Required = true
        };
    }

    private static Option<string> CreateTimeframeOption()
    {
        return new Option<string>("--timeframe")
        {
            Description = "Candle timeframe, such as 5m.",
            Required = true
        };
    }

    private static Option<DateTimeOffset> CreateFromOption()
    {
        return new Option<DateTimeOffset>("--from")
        {
            Description = "Inclusive UTC start time for the requested candle window.",
            Required = true
        };
    }

    private static Option<DateTimeOffset> CreateToOption()
    {
        return new Option<DateTimeOffset>("--to")
        {
            Description = "Exclusive UTC end time for the requested candle window.",
            Required = true
        };
    }

    private static RunRequest? ResolveRunRequest(
        ParseResult parseResult,
        Option<string> marketOption,
        Option<string> timeframeOption,
        Option<DateTimeOffset> fromOption,
        Option<DateTimeOffset> toOption)
    {
        try
        {
            var settings = WickdBotConfigurationLoader.LoadDefault();
            return RunRequestFactory.Create(
                settings,
                parseResult.GetRequiredValue(marketOption),
                parseResult.GetRequiredValue(timeframeOption),
                parseResult.GetRequiredValue(fromOption),
                parseResult.GetRequiredValue(toOption));
        }
        catch (Exception ex) when (ex is WickdBotConfigurationException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            return null;
        }
    }

    private static int ReturnNotImplemented(string commandName)
    {
        Console.Error.WriteLine($"Command '{commandName}' is not implemented yet.");
        return NotImplementedExitCode;
    }
}
