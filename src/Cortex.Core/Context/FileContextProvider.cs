using System.Globalization;
using System.Text;
using Cortex.Core.References;

namespace Cortex.Core.Context;

/// <summary>
/// File-based implementation of <see cref="IContextProvider"/> that stores
/// context entries as markdown files with YAML front matter in a directory.
/// </summary>
public sealed class FileContextProvider : IContextProvider
{
    private readonly string _directory;

    /// <summary>
    /// Creates a new FileContextProvider that reads/writes to the specified directory.
    /// </summary>
    /// <param name="directory">The directory path for context files.</param>
    public FileContextProvider(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
    }

    /// <inheritdoc />
    public async Task StoreAsync(ContextEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Directory.CreateDirectory(_directory);

        var fileName = $"{entry.EntryId}.md";
        var filePath = Path.Combine(_directory, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"entryId: {entry.EntryId}");
        sb.AppendLine($"category: {entry.Category}");

        if (entry.Tags.Count > 0)
        {
            sb.AppendLine($"tags: [{string.Join(", ", entry.Tags)}]");
        }

        if (entry.ReferenceCode.HasValue)
        {
            sb.AppendLine($"referenceCode: {entry.ReferenceCode.Value}");
        }

        sb.AppendLine($"createdAt: {entry.CreatedAt:O}");
        sb.AppendLine("---");
        sb.Append(entry.Content);

        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContextEntry>> QueryAsync(
        ContextQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var files = Directory.GetFiles(_directory, "*.md");
        var entries = new List<ContextEntry>();

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var entry = ParseEntry(content);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        var results = entries.AsEnumerable();

        if (!string.IsNullOrEmpty(query.Keywords))
        {
            results = results.Where(e =>
                e.Content.Contains(query.Keywords, StringComparison.OrdinalIgnoreCase));
        }

        if (query.Category.HasValue)
        {
            results = results.Where(e => e.Category == query.Category.Value);
        }

        if (query.Tags is { Count: > 0 })
        {
            results = results.Where(e =>
                e.Tags.Any(t => query.Tags.Contains(t)));
        }

        if (query.ReferenceCode.HasValue)
        {
            results = results.Where(e =>
                e.ReferenceCode.HasValue && e.ReferenceCode.Value == query.ReferenceCode.Value);
        }

        var ordered = results.OrderByDescending(e => e.CreatedAt);

        return query.MaxResults.HasValue
            ? ordered.Take(query.MaxResults.Value).ToList()
            : ordered.ToList();
    }

    private static ContextEntry? ParseEntry(string fileContent)
    {
        var frontMatterEnd = fileContent.IndexOf("---", 3, StringComparison.Ordinal);
        if (frontMatterEnd < 0)
        {
            return null;
        }

        var frontMatter = fileContent[3..frontMatterEnd].Trim();
        var body = fileContent[(frontMatterEnd + 3)..].TrimStart('\r', '\n');

        string? entryId = null;
        var category = ContextCategory.Operational;
        var tags = new List<string>();
        ReferenceCode? referenceCode = null;
        var createdAt = DateTimeOffset.UtcNow;

        foreach (var line in frontMatter.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            var key = trimmed[..colonIndex].Trim();
            var value = trimmed[(colonIndex + 1)..].Trim();

            switch (key)
            {
                case "entryId":
                    entryId = value;
                    break;
                case "category":
                    if (Enum.TryParse<ContextCategory>(value, out var cat))
                    {
                        category = cat;
                    }
                    break;
                case "tags":
                    var tagContent = value.TrimStart('[').TrimEnd(']');
                    if (!string.IsNullOrWhiteSpace(tagContent))
                    {
                        tags.AddRange(tagContent.Split(',',
                            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                    }
                    break;
                case "referenceCode":
                    referenceCode = new ReferenceCode(value);
                    break;
                case "createdAt":
                    if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind, out var dt))
                    {
                        createdAt = dt;
                    }
                    break;
            }
        }

        if (entryId is null)
        {
            return null;
        }

        return new ContextEntry
        {
            EntryId = entryId,
            Content = body,
            Category = category,
            Tags = tags,
            ReferenceCode = referenceCode,
            CreatedAt = createdAt
        };
    }
}
