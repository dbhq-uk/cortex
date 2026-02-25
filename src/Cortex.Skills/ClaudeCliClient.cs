using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.Skills;

/// <summary>
/// Invokes the Claude CLI for one-shot stateless completions.
/// Shells out to the <c>claude</c> process with <c>-p</c> (print mode).
/// </summary>
public sealed class ClaudeCliClient : ILlmClient
{
    private readonly ILogger<ClaudeCliClient> _logger;
    private readonly ClaudeCliOptions _options;

    /// <summary>
    /// Creates a new <see cref="ClaudeCliClient"/>.
    /// </summary>
    public ClaudeCliClient(
        ILogger<ClaudeCliClient> logger,
        IOptions<ClaudeCliOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.CliPath,
            Arguments = "-p",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogDebug("Invoking Claude CLI with {PromptLength} character prompt", prompt.Length);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to start Claude CLI at '{_options.CliPath}'. Is it installed and on PATH?", ex);
        }

        await process.StandardInput.WriteAsync(prompt.AsMemory(), cts.Token);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
        var error = await process.StandardError.ReadToEndAsync(cts.Token);

        await process.WaitForExitAsync(cts.Token);

        if (process.ExitCode != 0)
        {
            _logger.LogError(
                "Claude CLI exited with code {ExitCode}: {Error}",
                process.ExitCode, error);

            throw new InvalidOperationException(
                $"Claude CLI exited with code {process.ExitCode}: {error}");
        }

        _logger.LogDebug("Claude CLI returned {OutputLength} characters", output.Length);

        return output.Trim();
    }
}
