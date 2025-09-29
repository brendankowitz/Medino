namespace Medino;

/// <summary>
/// Context object that flows through context-aware pipeline behaviors, allowing request transformation and metadata enrichment
/// </summary>
/// <typeparam name="TRequest">Request type</typeparam>
public class PipelineContext<TRequest> where TRequest : notnull
{
    private readonly Dictionary<string, object> _metadata = new();

    /// <summary>
    /// The request being processed. Can be replaced by pipeline behaviors to transform the request.
    /// </summary>
    public TRequest Request { get; set; }

    /// <summary>
    /// Metadata dictionary for enriching the pipeline context with additional information
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    /// <summary>
    /// Creates a new pipeline context
    /// </summary>
    /// <param name="request">The initial request</param>
    public PipelineContext(TRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    /// <summary>
    /// Sets metadata value for the specified key
    /// </summary>
    /// <param name="key">Metadata key</param>
    /// <param name="value">Metadata value</param>
    public void SetMetadata(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Metadata key cannot be null or whitespace", nameof(key));
        }

        _metadata[key] = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets metadata value for the specified key
    /// </summary>
    /// <typeparam name="T">Expected type of the metadata value</typeparam>
    /// <param name="key">Metadata key</param>
    /// <returns>The metadata value if found, otherwise default(T)</returns>
    public T? GetMetadata<T>(string key)
    {
        if (_metadata.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Checks if metadata exists for the specified key
    /// </summary>
    /// <param name="key">Metadata key</param>
    /// <returns>True if metadata exists, otherwise false</returns>
    public bool HasMetadata(string key)
    {
        return _metadata.ContainsKey(key);
    }
}