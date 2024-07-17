using Ipfs;
using Ipfs.CoreApi;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A shared interface for all read-only kubo based nomad storage.
/// </summary>
public interface IReadOnlyNomadKuboEventStreamHandler<in TEventEntryContent> : ISharedEventStreamHandler<Cid, EventStream<Cid>, EventStreamEntry<Cid>>
{
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
    /// <param name="updateEvent">The update to apply without side effects.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    abstract Task ApplyEntryUpdateAsync(TEventEntryContent updateEvent, CancellationToken cancellationToken);
}