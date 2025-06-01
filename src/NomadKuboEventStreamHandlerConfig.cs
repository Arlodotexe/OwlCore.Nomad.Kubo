using Ipfs;
using Newtonsoft.Json;
using OwlCore.ComponentModel;
using OwlCore.Kubo;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Represents the configuration needed to construct either a read-only or modifiable event stream handler.
/// </summary>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
public record NomadKuboEventStreamHandlerConfig<TRoaming> : ISources<Cid>
{
    /// <summary>
    /// The ID of the roaming data.
    /// </summary>
    public Cid? RoamingId { get; set; }

    /// <summary>
    /// The value of the roaming data.
    /// </summary>
    /// <remarks>
    /// If this value is set when an event stream handler is created, it will be used as the initial value for the roaming data.
    /// Any event stream entries that are applied to the event stream will be applied starting from this value.
    /// </remarks>
    public TRoaming? RoamingValue { get; set; }

    /// <summary>
    /// The value of the local event stream.
    /// </summary>
    public EventStream<DagCid>? LocalValue { get; set; }

    /// <summary>
    /// The key used to publish the roaming data.
    /// </summary>
    public Key? RoamingKey { get; set; }

    /// <summary>
    /// The key used to publish the local event stream.
    /// </summary>
    public Key? LocalKey { get; set; }

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
    [JsonIgnore]
    public bool CanAndShouldResolveRoamingValue => RoamingId is not null && RoamingValue is null;

    /// <summary>
    /// A boolean that indicates whether the local value can be resolved (whether a local key is known) and whether it should be resolved (local value is not known).
    /// </summary>
    [JsonIgnore]
    public bool CanAndShouldResolveLocalValue => LocalKey is not null && LocalValue is null;

    /// <summary>
    /// A boolean that indicates whether this object has a local key/value pair.
    /// </summary>
    [JsonIgnore]
    public bool HasLocalKvp => LocalKey is not null && LocalValue is not null;

    /// <summary>
    /// A boolean that indicates whether this config has local and roaming keys.
    /// </summary>
    [JsonIgnore]
    public bool NoKeys => (LocalKey is null || RoamingKey is null) && (LocalKey is null == RoamingKey is null ? true : throw new InvalidOperationException("Either roaming or local key was null, but not both. This is an invalid configuration. Event stream handler configs and repositories are currently designed to have a 1:1 roaming/local KVP correspondence. See for details https://github.com/Arlodotexe/OwlCore.Nomad.Kubo/issues/8"));

    /// <summary>
    /// Pre-resolved event stream entries for this handler. Optional.
    /// </summary>
    /// <remarks>
    /// A null value indicates the event stream entries have not been resolved.
    /// <para/>
    /// This is for runtime use by <see cref="NomadKuboRepository{TModifiable, TReadOnly, TRoaming, TEventEntryContent, TCreateParam}"/>. An assigned value indicates resolved entries for ALL sources, not just the local event stream. 
    /// <para/>
    /// If setting this property, ensure that you also resolve remote event stream entries for non-local sources, as this property is used to determine the full set of event stream entries for the handler. 
    /// <para/>
    /// If you do not set this property, it will be resolved automatically when needed.
    /// </remarks>
    public ICollection<EventStreamEntry<DagCid>>? ResolvedEventStreamEntries { get; set; }

    /// <summary>
    /// A collection of all local event stream sources that this handler is paired with.
    /// </summary>
    public ICollection<Cid> Sources { get; init; } = new List<Cid>();
}