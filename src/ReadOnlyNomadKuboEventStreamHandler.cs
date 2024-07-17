using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A read-only shared stream handler implementation.
/// </summary>
public abstract class ReadOnlyNomadKuboEventStreamHandler<TEventEntryContent> : IReadOnlyNomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <summary>
    /// Creates a new instance of <see cref="ReadOnlyNomadKuboEventStreamHandler{TEventEntryContent}"/>.
    /// </summary>
    /// <param name="listeningEventStreamHandlers">A shared collection of all available event streams that should participate in playback of events using their respective <see cref="IEventStreamHandler{TEventStreamEntry}.TryAdvanceEventStreamAsync"/>. </param>
    protected ReadOnlyNomadKuboEventStreamHandler(ICollection<ISharedEventStreamHandler<Cid, EventStream<Cid>, EventStreamEntry<Cid>>> listeningEventStreamHandlers)
    {
        listeningEventStreamHandlers.Add(this);
        ListeningEventStreamHandlers = listeningEventStreamHandlers;
    }

    /// <inheritdoc />
    public EventStreamEntry<Cid>? EventStreamPosition { get; set; }

    /// <inheritdoc />
    public virtual required ICollection<Cid> Sources { get; init; }

    /// <inheritdoc />
    public required string Id { get; init; }

    /// <inheritdoc />
    public required ICoreApi Client { get; set; }

    /// <inheritdoc />
    public required IKuboOptions KuboOptions { get; set; }

    /// <summary>
    /// The key name used for modifications via a local event stream.
    /// </summary>
    public required string LocalEventStreamKeyName { get; init; }

    /// <inheritdoc />
    public ICollection<ISharedEventStreamHandler<Cid, EventStream<Cid>, EventStreamEntry<Cid>>> ListeningEventStreamHandlers { get; set; }

    /// <inheritdoc />
    public virtual async Task TryAdvanceEventStreamAsync(EventStreamEntry<Cid> streamEntry, CancellationToken cancellationToken)
    {
        var (result, _) = await Client.ResolveDagCidAsync<TEventEntryContent>(streamEntry.Content, nocache: false, cancellationToken);
        if (result is not null)
            await ApplyEntryUpdateAsync(result, cancellationToken);

        EventStreamPosition = streamEntry;
    }

    /// <inheritdoc />
    public abstract Task ResetEventStreamPositionAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract Task ApplyEntryUpdateAsync(TEventEntryContent updateEvent, CancellationToken cancellationToken);
}