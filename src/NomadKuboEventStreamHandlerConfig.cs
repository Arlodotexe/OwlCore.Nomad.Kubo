using Ipfs;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Represents the configuration needed to construct either a read-only or modifiable event stream handler.
/// </summary>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
public record NomadKuboEventStreamHandlerConfig<TRoaming>
{
    /// <summary>
    /// The ID of the roaming data.
    /// </summary>
    public Cid? RoamingId { get; set; }

    /// <summary>
    /// The value of the roaming data.
    /// </summary>
    public TRoaming? RoamingValue { get; set; }

    /// <summary>
    /// The value of the local event stream.
    /// </summary>
    public EventStream<DagCid>? LocalValue { get; set; }

    /// <summary>
    /// The key used to publish the roaming data.
    /// </summary>
    public IKey? RoamingKey { get; set; }

    /// <summary>
    /// The key used to publish the local event stream.
    /// </summary>
    public IKey? LocalKey { get; set; }

    /// <summary>
    /// The key name used to publish the local event stream.
    /// </summary>
    public required string LocalKeyName { get; set; }

    /// <summary>
    /// The key name used to published the roaming data.
    /// </summary>
    public required string RoamingKeyName { get; set; }

    /// <summary>
    /// Pre-resolved event stream entries for this handler. Optional.
    /// </summary>
    /// <remarks>
    /// A null value indicates the event stream entries have not been resolved.
    /// </remarks>
    public ICollection<EventStreamEntry<DagCid>>? ResolvedEventStreamEntries { get; set; }
}