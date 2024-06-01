using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;
using OwlCore.ComponentModel;

// ReSharper disable once CheckNamespace
namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Extension methods for <see cref="IModifiableNomadKuboEventStreamHandler{TEventEntryContent}"/>.
/// </summary>
public static class NomadKuboEventStreamHandlerExtensions
{
    /// <summary>
    /// Publishes the inner content to the roaming ipns key on <paramref name="eventStreamHandler"/>.
    /// </summary>
    public static async Task PublishRoamingAsync<TEventStreamHandler, TEventEntryContent, TContent>(
        this TEventStreamHandler eventStreamHandler,
        CancellationToken cancellationToken)
        where TContent : class, ISources<KuboNomadEventStream>
        where TEventStreamHandler : IModifiableNomadKuboEventStreamHandler<TEventEntryContent>, IDelegable<TContent>
    {
        // Move event stream handler sources used to construct object into inner to be serialized for roaming object
        foreach (var source in eventStreamHandler.Sources)
        {
            if (eventStreamHandler.Inner.Sources.All(x => x.Id != source.Id))
                eventStreamHandler.Inner.Sources.Add(source);
        }

        var cid = await eventStreamHandler.Client.Dag.PutAsync(eventStreamHandler.Inner, cancel: cancellationToken);
        Guard.IsNotNull(cid);

        _ = await eventStreamHandler.Client.Name.PublishAsync(cid, lifetime: eventStreamHandler.KuboOptions.IpnsLifetime, key: eventStreamHandler.RoamingKeyName, cancel: cancellationToken);
    }

    /// <summary>
    /// Appends a new event entry to the provided modifiable event stream handler.
    /// </summary>
    /// <param name="eventStreamHandler">The event stream handler to append the provided <paramref name="updateEvent"/> to.</param>
    /// <param name="updateEvent">The update event to append.</param>
    /// <param name="ipnsLifetime">The ipns lifetime for the published key to be valid for. Node must be online at least once per this interval of time for the published ipns key to stay in the dht.</param>
    /// <param name="getDefaultEventStream">Gets the default event stream type when needed for creation.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task AppendNewEntryAsync<TEventEntryContent>(
        this IModifiableNomadKuboEventStreamHandler<TEventEntryContent> eventStreamHandler,
        TEventEntryContent updateEvent, TimeSpan ipnsLifetime, Func<KuboNomadEventStream> getDefaultEventStream,
        CancellationToken cancellationToken)
        where TEventEntryContent : notnull
    {
        // Get or create event stream on ipns
        var key = await eventStreamHandler.Client.GetOrCreateKeyAsync(eventStreamHandler.LocalEventStreamKeyName,
            _ => getDefaultEventStream(), ipnsLifetime, cancellationToken: cancellationToken);
        Guard.IsNotNull(key);

        var (eventStream, _) = await eventStreamHandler.Client.ResolveDagCidAsync<KuboNomadEventStream>(key.Id,
            nocache: !eventStreamHandler.KuboOptions.UseCache, cancellationToken);
        Guard.IsNotNull(eventStream);

        var updateEventDagCid = await eventStreamHandler.Client.Dag.PutAsync(updateEvent,
            pin: eventStreamHandler.KuboOptions.ShouldPin, cancel: cancellationToken);

        // Create new nomad event stream entry
        var newEventStreamEntry = new KuboNomadEventStreamEntry
        {
            Id = eventStreamHandler.Id,
            TimestampUtc = DateTime.UtcNow,
            Content = updateEventDagCid,
        };

        var newEventStreamEntryDagCid = await eventStreamHandler.Client.Dag.PutAsync(newEventStreamEntry,
            pin: eventStreamHandler.KuboOptions.ShouldPin, cancel: cancellationToken);

        // Add new event to event stream
        eventStream.Entries.Add(newEventStreamEntryDagCid);

        var updatedEventStreamDagCid = await eventStreamHandler.Client.Dag.PutAsync(eventStream,
            pin: eventStreamHandler.KuboOptions.ShouldPin, cancel: cancellationToken);

        // Publish updated event stream
        _ = await eventStreamHandler.Client.Name.PublishAsync($"/ipfs/{updatedEventStreamDagCid}",
            key: eventStreamHandler.LocalEventStreamKeyName, lifetime: eventStreamHandler.KuboOptions.IpnsLifetime,
            cancellationToken);
    }

    /// <summary>
    /// Converts a nomad event stream entry content pointer to a <see cref="KuboNomadEventStreamEntry"/>.
    /// </summary>
    /// <param name="cid">The content pointer to resolve.</param>
    /// <param name="client">The client to use to resolve the pointer.</param>
    /// <param name="useCache">Whether to use cache or not.</param>
    /// <param name="token">A token that can be used to cancel the ongoing operation.</param>
    public static async Task<KuboNomadEventStreamEntry> ContentPointerToStreamEntryAsync(Cid cid, ICoreApi client,
        bool useCache, CancellationToken token)
    {
        var (streamEntry, _) =
            await client.ResolveDagCidAsync<KuboNomadEventStreamEntry>(cid, nocache: !useCache, token);
        Guard.IsNotNull(streamEntry);
        return streamEntry;
    }
}