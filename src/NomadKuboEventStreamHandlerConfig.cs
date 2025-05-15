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
    /// The key name used to create the local event stream key.
    /// </summary>
    public string? LocalKeyName { get; set; }

    /// <summary>
    /// The key name used to create the roaming data key.
    /// </summary>
    public string? RoamingKeyName { get; set; }

    /// <summary>
    /// A boolean that represents whether the roaming value can be resolved (whether a roaming id is known) and whether it should be resolved (roaming value is not known).
    /// </summary>
    public bool CanAndShouldResolveRoamingValue => RoamingId is not null && RoamingValue is null;

    /// <summary>
    /// A boolean that indicates whether the local value can be resolved (whether a local key is known) and whether it should be resolved (local value is not known).
    /// </summary>
    public bool CanAndShouldResolveLocalValue => LocalKey is not null && LocalValue is null;
    
    /// <summary>
    /// A boolean that indicates whether this object has a local key/value pair.
    /// </summary>
    public bool HasLocalKvp => LocalKey is not null && LocalValue is not null;
  
    /// <summary>
    /// A boolean that indicates whether this config has local and roaming keys.
    /// </summary>
    public bool NoKeys => (LocalKey is null || RoamingKey is null) && (LocalKey is null == RoamingKey is null ? true : throw new InvalidOperationException("Either roaming or local key was null, but not both. This is an invalid configuration. Event stream handler configs and repositories are currently designed to have a 1:1 roaming/local KVP correspondence. See for details https://github.com/Arlodotexe/OwlCore.Nomad.Kubo/issues/8"));
    
    /// <summary>
    /// Pre-resolved event stream entries for this handler. Optional.
    /// </summary>
    /// <remarks>
    /// A null value indicates the event stream entries have not been resolved.
    /// </remarks>
    public ICollection<EventStreamEntry<DagCid>>? ResolvedEventStreamEntries { get; set; }
}