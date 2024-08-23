using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A read-only shared stream handler implementation.
/// </summary>
public abstract class NomadKuboEventStreamHandler<TEventEntryContent> : INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <summary>
    /// Creates a new instance of <see cref="NomadKuboEventStreamHandler{TEventEntryContent}"/>.
    /// </summary>
    /// <param name="listeningEventStreamHandlers">A shared collection of all available event streams that should participate in playback of events using their respective <see cref="IEventStreamHandler{TContentPointer, TEventStream, TEventStreamEntry}.AdvanceEventStreamAsync"/>. </param>
    protected NomadKuboEventStreamHandler(ICollection<ISharedEventStreamHandler<Cid, EventStream<Cid>, EventStreamEntry<Cid>>> listeningEventStreamHandlers)
    {
        listeningEventStreamHandlers.Add(this);
        ListeningEventStreamHandlers = listeningEventStreamHandlers;
    }

    /// <inheritdoc />
    public required string EventStreamHandlerId { get; init; }

    /// <inheritdoc />
    public EventStreamEntry<Cid>? EventStreamPosition { get; set; }

    /// <inheritdoc />
    public required ICollection<EventStreamEntry<Cid>> AllEventStreamEntries { get; set; }

    /// <inheritdoc />
    public required EventStream<Cid> LocalEventStream { get; set; }

    /// <inheritdoc />
    public virtual required ICollection<Cid> Sources { get; init; }

    /// <inheritdoc />
    public required IKey LocalEventStreamKey { get; init; }

    /// <inheritdoc />
    public required IKey RoamingKey { get; init; }

    /// <inheritdoc />
    public required ICoreApi Client { get; set; }

    /// <inheritdoc />
    public required IKuboOptions KuboOptions { get; set; }

    /// <inheritdoc />
    public ICollection<ISharedEventStreamHandler<Cid, EventStream<Cid>, EventStreamEntry<Cid>>> ListeningEventStreamHandlers { get; set; }

    /// <inheritdoc />
    public virtual async Task AdvanceEventStreamAsync(EventStreamEntry<Cid> streamEntry, CancellationToken cancellationToken)
    {
        var (result, _) = await Client.ResolveDagCidAsync<TEventEntryContent>(streamEntry.Content, nocache: false, cancellationToken);
        if (result is not null)
            await ApplyEntryUpdateAsync(result, cancellationToken);

        EventStreamPosition = streamEntry;
    }
    
    /// <inheritdoc />
    public abstract Task<EventStreamEntry<Cid>> AppendNewEntryAsync(TEventEntryContent updateEvent, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public abstract Task ResetEventStreamPositionAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract Task ApplyEntryUpdateAsync(TEventEntryContent updateEvent, CancellationToken cancellationToken);
}