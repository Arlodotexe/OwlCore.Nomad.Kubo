using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A base class for event stream handler implementations.
/// </summary>
public abstract class NomadKuboEventStreamHandler<TEventEntryContent> : INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <inheritdoc />
    public required string EventStreamHandlerId { get; init; }

    /// <inheritdoc />
    public EventStreamEntry<Cid>? EventStreamPosition { get; set; }

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
    public virtual async Task AdvanceEventStreamAsync(EventStreamEntry<Cid> streamEntry, CancellationToken cancellationToken)
    {
        var (result, _) = await Client.ResolveDagCidAsync<TEventEntryContent>(streamEntry.Content, nocache: false, cancellationToken);
        if (result is not null)
            await ApplyEntryUpdateAsync(streamEntry, result, cancellationToken);

        EventStreamPosition = streamEntry;
    }

    /// <inheritdoc />
    public virtual async Task<EventStreamEntry<Cid>> AppendNewEntryAsync(string targetId, string eventId, TEventEntryContent eventEntryContent, DateTime? timestampUtc = null, CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(eventEntryContent);
        var localUpdateEventCid = await Client.Dag.PutAsync(eventEntryContent, pin: KuboOptions.ShouldPin, cancel: cancellationToken);
        var newEntry = await this.AppendEventStreamEntryAsync(localUpdateEventCid, eventId, targetId, cancellationToken);
        return newEntry;
    }

    /// <inheritdoc />
    public abstract Task ResetEventStreamPositionAsync(CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract Task ApplyEntryUpdateAsync(EventStreamEntry<Cid> streamEntry, TEventEntryContent updateEvent, CancellationToken cancellationToken);
}