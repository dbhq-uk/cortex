namespace Cortex.Core.References;

/// <summary>
/// Configuration options for <see cref="FileSequenceStore"/>.
/// </summary>
public sealed class FileSequenceStoreOptions
{
    /// <summary>
    /// Path to the JSON file where sequence state is persisted.
    /// </summary>
    public required string FilePath { get; set; }
}
