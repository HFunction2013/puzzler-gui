using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using PuzzlerGui.Models;

namespace PuzzlerGui.Services;

/// <summary>Runs the <c>puzzler</c> executable and reads its JSON report.</summary>
public sealed class PuzzlerRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Func<string?> _solverPathProvider;

    public PuzzlerRunner(Func<string?> solverPathProvider)
    {
        _solverPathProvider = solverPathProvider;
    }

    public string? FindSolver() => LocateSolver(_solverPathProvider());

    private static string L(string key, params object?[] args) =>
        string.Format(LocalizationService.Instance[key], args);

    public async Task<SolveOutcome> SolveAsync(
        PuzzleDocument document,
        SolveParameters parameters,
        CancellationToken cancellationToken)
    {
        var executable = FindSolver();
        if (executable is null)
        {
            return new SolveOutcome
            {
                Status = SolveStatus.SolverMissing,
                Message = L("banner.solverMissing"),
            };
        }

        var workDirectory = CreateWorkDirectory();
        var puzzlePath = Path.Combine(workDirectory, "puzzle.pzle");
        var reportPath = Path.Combine(workDirectory, "report.json");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await File.WriteAllTextAsync(puzzlePath, PuzzleYaml.Save(document), cancellationToken);

            var arguments = BuildArguments(parameters, puzzlePath, reportPath);

            var run = await RunAsync(executable, arguments, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            SolveReport? report = null;
            var json = string.Empty;
            if (File.Exists(reportPath))
            {
                json = await File.ReadAllTextAsync(reportPath, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try
                    {
                        report = JsonSerializer.Deserialize<SolveReport>(json, JsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        return new SolveOutcome
                        {
                            Status = SolveStatus.Failed,
                            Message = L("runner.unreadableReport", ex.Message),
                            Duration = stopwatch.Elapsed,
                        };
                    }
                }
            }

            return run.ExitCode switch
            {
                0 => new SolveOutcome
                {
                    Status = SolveStatus.Success,
                    Report = report,
                    RawJson = json,
                    Message = FirstLine(run.StandardError),
                    Duration = stopwatch.Elapsed,
                },
                2 => new SolveOutcome
                {
                    Status = SolveStatus.NoSolution,
                    Report = report,
                    RawJson = json,
                    Message = L("solution.emptyNoSolution"),
                    Duration = stopwatch.Elapsed,
                },
                1 => new SolveOutcome
                {
                    Status = SolveStatus.InvalidPuzzle,
                    Report = report,
                    RawJson = json,
                    Message = FirstLine(run.StandardError) is { Length: > 0 } error
                        ? error
                        : L("runner.invalidPuzzle"),
                    Duration = stopwatch.Elapsed,
                },
                _ => new SolveOutcome
                {
                    Status = SolveStatus.Failed,
                    Message = L("runner.exitCode", run.ExitCode, FirstLine(run.StandardError)),
                    Duration = stopwatch.Elapsed,
                },
            };
        }
        catch (OperationCanceledException)
        {
            return new SolveOutcome
            {
                Status = SolveStatus.Cancelled,
                Message = L("banner.searchCancelled"),
                Duration = stopwatch.Elapsed,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new SolveOutcome
            {
                Status = SolveStatus.Failed,
                Message = L("runner.couldNotRun", ex.Message),
                Duration = stopwatch.Elapsed,
            };
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    internal static List<string> BuildArguments(
        SolveParameters parameters,
        string puzzlePath,
        string reportPath)
    {
        var arguments = new List<string> { puzzlePath, "--out", reportPath };

        if (parameters.CompressJson)
        {
            arguments.Add("--compress");
        }

        if (parameters.MaxSolutions is > 0)
        {
            arguments.Add("--limit");
            arguments.Add(parameters.MaxSolutions.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (parameters.NodeLimit is > 0)
        {
            arguments.Add("--nodes");
            arguments.Add(parameters.NodeLimit.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (parameters.TimeLimitSeconds is > 0)
        {
            arguments.Add("--time");
            arguments.Add(parameters.TimeLimitSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (!parameters.Dedupe)
        {
            arguments.Add("--no-dedupe");
        }

        if (parameters.Seed is not null)
        {
            arguments.Add("--seed");
            arguments.Add(parameters.Seed.Value.ToString(CultureInfo.InvariantCulture));
        }

        return arguments;
    }

    public async Task<ValidateOutcome> ValidateAsync(PuzzleDocument document, CancellationToken cancellationToken)
    {
        var executable = FindSolver();
        if (executable is null)
        {
            return new ValidateOutcome
            {
                IsValid = false,
                Output = L("banner.solverMissing"),
            };
        }

        var workDirectory = CreateWorkDirectory();
        var puzzlePath = Path.Combine(workDirectory, "puzzle.pzle");
        try
        {
            await File.WriteAllTextAsync(puzzlePath, PuzzleYaml.Save(document), cancellationToken);
            var run = await RunAsync(executable, new[] { puzzlePath, "--validate" }, cancellationToken);
            var output = new StringBuilder();
            if (run.StandardOutput.Trim().Length > 0)
            {
                output.Append(run.StandardOutput.Trim());
            }

            if (run.StandardError.Trim().Length > 0)
            {
                if (output.Length > 0)
                {
                    output.AppendLine();
                }

                output.Append(run.StandardError.Trim());
            }

            return new ValidateOutcome
            {
                IsValid = run.ExitCode == 0,
                Output = output.ToString(),
            };
        }
        catch (OperationCanceledException)
        {
            return new ValidateOutcome { IsValid = false, Output = L("runner.validationCancelled") };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return new ValidateOutcome { IsValid = false, Output = L("runner.couldNotRun", ex.Message) };
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    public static string? LocateSolver(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var expanded = Environment.ExpandEnvironmentVariables(configuredPath.Trim());
            if (File.Exists(expanded))
            {
                return Path.GetFullPath(expanded);
            }
        }

        var fromEnvironment = Environment.GetEnvironmentVariable("PUZZLER_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
        {
            return Path.GetFullPath(fromEnvironment);
        }

        var names = OperatingSystem.IsWindows()
            ? new[] { "puzzler.exe", "puzzler" }
            : new[] { "puzzler" };

        foreach (var name in names)
        {
            var beside = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(beside))
            {
                return beside;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (path is not null)
        {
            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var name in names)
                {
                    try
                    {
                        var candidate = Path.Combine(directory.Trim(), name);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Ignore malformed PATH entries.
                    }
                }
            }
        }

        return null;
    }

    private static async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException(L("runner.processNotStarted"));
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process already exited.
            }

            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static string CreateWorkDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "puzzler-gui",
            Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temporary files are best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary files are best effort.
        }
    }

    private static string FirstLine(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("warning:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return trimmed;
        }

        return string.Empty;
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
