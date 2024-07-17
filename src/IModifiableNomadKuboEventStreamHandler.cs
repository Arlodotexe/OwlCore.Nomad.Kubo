using Ipfs;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A modifiable kubo-based storage interface.
/// </summary>
public interface IModifiableNomadKuboEventStreamHandler<TEventEntryContent> :
    IModifiableSharedEventStreamHandler<TEventEntryContent, Cid, EventStream<Cid>, EventStreamEntry<Cid>>,
    IReadOnlyNomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <summary>
    /// The name of an Ipns key containing a Nomad event stream that can be appended and republished to modify the current folder.
    /// </summary>
    public string LocalEventStreamKeyName { get; init; }
    
    /// <summary>
    /// The name of an Ipns key containing the final object from advancing a nomad event stream from all sources.
    /// </summary>
    /// <remarks>
    /// Assuming each device is given the same data sources, advancing via Nomad should yield the same final state.  
    /// </remarks>
    public string RoamingKeyName { get; init; }
}