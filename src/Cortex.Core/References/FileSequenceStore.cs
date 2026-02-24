using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Cortex.Core.References;

/// <summary>
/// Persists reference code sequence state to a JSON file on disk.
/// </summary>
public sealed class FileSequenceStore : ISequenceStore
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Creates a new <see cref="FileSequenceStore"/>.
    /// </summary>
    public FileSequenceStore(IOptions<FileSequenceStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.FilePath);

        _filePath = options.Value.FilePath;
    }

    /// <inheritdoc />
    public async Task<SequenceState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new SequenceState(DateOnly.MinValue, 0);
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<FileData>(json, JsonOptions);

            if (data is null)
            {
                return new SequenceState(DateOnly.MinValue, 0);
            }

            return new SequenceState(DateOnly.Parse(data.Date), data.Sequence);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return new SequenceState(DateOnly.MinValue, 0);
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(SequenceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new FileData
        {
            Date = state.Date.ToString("yyyy-MM-dd"),
            Sequence = state.Sequence
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }

    private sealed class FileData
    {
        public string Date { get; init; } = "";
        public int Sequence { get; init; }
    }
}
