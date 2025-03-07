using Ipfs;
using Ipfs.CoreApi;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A shared interface for all Kubo-based nomad event stream handlers.
/// </summary>
/// <remarks>
/// If you're reading roaming data that you aren't publishing to, you don't need to resolve and replay the event stream.
/// </remarks>
public interface INomadKuboEventStreamHandler<in TEventEntryContent> : IEventStreamHandler<DagCid, Cid, EventStream<DagCid>, EventStreamEntry<DagCid>>
{
    /// <summary>
    /// The name of an Ipns key containing a Nomad event stream that can be appended and republished to modify the current folder.
    /// </summary>
    public IKey LocalEventStreamKey { get; init; }
    
    /// <summary>
    /// The name of an Ipns key containing the final object from advancing a nomad event stream from all sources.
    /// </summary>
    /// <remarks>
    /// Assuming each device is given the same data sources, advancing via Nomad should yield the same final state.  
    /// </remarks>
    public IKey RoamingKey { get; init; }
    
    /// <summary>
    /// The client to use for communicating with Ipfs.
    /// </summary>
    public ICoreApi Client { get; set; }

    /// <summary>
    /// Whether to pin content added to Ipfs.
    /// </summary>
    public IKuboOptions KuboOptions { get; set; }

    /// <summary>
    /// Applies an event stream update to this object without side effects.
    /// </summary>
    /// <param name="eventStreamEntry">The event stream entry</param>
    /// <param name="eventEntryContent">The update to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public Task ApplyEntryUpdateAsync(EventStreamEntry<DagCid> eventStreamEntry, TEventEntryContent eventEntryContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends the provided event entry of type <typeparamref name="TEventEntryContent"/> to the underlying event stream.
    /// </summary>
    /// <param name="targetId">A unique identifier that represents the scope the applied event occurred within.</param>
    /// <param name="eventId">A unique identifier for the event that occured within this <paramref name="targetId"/>.</param>
    /// <param name="eventEntryContent">The update event data to apply and persist.</param>
    /// <param name="timestampUtc">The recorded UTC timestamp when this event entry was applied.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>A task containing the event stream entry that was applied from the update.</returns>
    public Task<EventStreamEntry<DagCid>> AppendNewEntryAsync(string targetId, string eventId, TEventEntryContent eventEntryContent, DateTime? timestampUtc = null, CancellationToken cancellationToken = default);
}