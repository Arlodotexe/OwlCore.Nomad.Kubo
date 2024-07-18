using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;
using OwlCore.ComponentModel;
using OwlCore.Diagnostics;
using OwlCore.Extensions;
using OwlCore.Nomad.Kubo.Events;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Extension methods for <see cref="IModifiableNomadKuboEventStreamHandler{TEventEntryContent}"/>.
/// </summary>
public static class NomadKuboEventStreamHandlerExtensions
{
    /// <summary>
    /// Handles resolving a dag content pointer for Kubo on an event stream handler.
    /// </summary>
    /// <returns>The resolved content.</returns>
    public static async Task<TResult> ResolveContentPointerAsync<TResult, TEventEntryContent>(this IReadOnlyNomadKuboEventStreamHandler<TEventEntryContent> handler, Cid cid, CancellationToken ctk)
    {
        var resolved = await handler.Client.ResolveDagCidAsync<TResult>(cid, nocache: !handler.KuboOptions.UseCache, ctk);
        Guard.IsNotNull(resolved.Result);        
        return resolved.Result;
    }


    /// <summary>
    /// Given a published ipns key containing roaming nomad data, return the sources that were published. 
    /// </summary>
    /// <returns>The sources from the roaming key.</returns>
    public static async Task<ICollection<Cid>?> GetSourcesFromRoamingKeyAsync<TRoamingContent>(this Cid roamingNomadEventStreamCid, ICoreApi client, IKuboOptions kuboOptions, CancellationToken cancellationToken)
        where TRoamingContent : ISources<Cid>
    {
        // Resolve roaming content
        var (roamingContent, _) = await roamingNomadEventStreamCid.ResolveDagCidAsync<TRoamingContent>(client, !kuboOptions.UseCache, cancellationToken);
        Guard.IsNotNull(roamingContent);

        return roamingContent.Sources;
    }
    
    /// <summary>
    /// Publishes the inner content to the roaming ipns key on <paramref name="eventStreamHandler"/>.
    /// </summary>
    public static async Task PublishRoamingAsync<TEventStreamHandler, TEventEntryContent, TContent>(
        this TEventStreamHandler eventStreamHandler,
        CancellationToken cancellationToken)
        where TContent : class, ISources<Cid>
        where TEventStreamHandler : IModifiableNomadKuboEventStreamHandler<TEventEntryContent>, IDelegable<TContent>
    {
        var cid = await eventStreamHandler.Client.Dag.PutAsync(eventStreamHandler.Inner, cancel: cancellationToken);
        Guard.IsNotNull(cid);

        _ = await eventStreamHandler.Client.Name.PublishAsync(cid, lifetime: eventStreamHandler.KuboOptions.IpnsLifetime, key: eventStreamHandler.RoamingKeyName, cancel: cancellationToken);
    }

    /// <summary>
    /// Appends a new event entry to the provided modifiable event stream handler.
    /// </summary>
    /// <param name="eventStreamHandler">The event stream handler to append the provided <paramref name="updateEventContent"/> to.</param>
    /// <param name="updateEventContent">The content to include in the appended update event.</param>
    /// <param name="eventId">A unique identifier for the event being applied.</param>
    /// <param name="ipnsLifetime">The ipns lifetime for the published key to be valid for. Node must be online at least once per this interval of time for the published ipns key to stay in the dht.</param>
    /// <param name="getDefaultEventStream">Gets the default event stream type when needed for creation.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task AppendNewEntryAsync<TEventEntryContent>(
        this IModifiableNomadKuboEventStreamHandler<TEventEntryContent> eventStreamHandler,
        TEventEntryContent updateEventContent, string eventId, TimeSpan ipnsLifetime, Func<EventStream<Cid>> getDefaultEventStream,
        CancellationToken cancellationToken)
        where TEventEntryContent : notnull
    {
        // Get or create event stream on ipns
        var key = await eventStreamHandler.Client.GetOrCreateKeyAsync(eventStreamHandler.LocalEventStreamKeyName,
            _ => getDefaultEventStream(), ipnsLifetime, cancellationToken: cancellationToken);
        Guard.IsNotNull(key);

        var (eventStream, _) = await eventStreamHandler.Client.ResolveDagCidAsync<EventStream<Cid>>(key.Id,
            nocache: !eventStreamHandler.KuboOptions.UseCache, cancellationToken);
        Guard.IsNotNull(eventStream);

        var updateEventDagCid = await eventStreamHandler.Client.Dag.PutAsync(updateEventContent,
            pin: eventStreamHandler.KuboOptions.ShouldPin, cancel: cancellationToken);

        // Create new nomad event stream entry
        var newEventStreamEntry = new EventStreamEntry<Cid>
        {
            TargetId = eventStreamHandler.Id,
            EventId = eventId,
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
        _ = await eventStreamHandler.Client.Name.PublishAsync(updatedEventStreamDagCid,
            key: eventStreamHandler.LocalEventStreamKeyName, lifetime: eventStreamHandler.KuboOptions.IpnsLifetime,
            cancellationToken);
    }

    /// <summary>
    /// Converts a nomad event stream entry content pointer to a <see cref="EventStreamEntry{Cid}"/>.
    /// </summary>
    /// <param name="cid">The content pointer to resolve.</param>
    /// <param name="client">The client to use to resolve the pointer.</param>
    /// <param name="useCache">Whether to use cache or not.</param>
    /// <param name="token">A token that can be used to cancel the ongoing operation.</param>
    public static async Task<EventStreamEntry<Cid>> ContentPointerToStreamEntryAsync(Cid cid, ICoreApi client,
        bool useCache, CancellationToken token)
    {
        var (streamEntry, _) = await client.ResolveDagCidAsync<EventStreamEntry<Cid>>(cid, nocache: !useCache, token);
        Guard.IsNotNull(streamEntry);
        return streamEntry;
    }


    /// <summary>
    /// Resolves the full event stream from all sources organized by date, advancing all listening <see cref="ISharedEventStreamHandler{TContentPointer,TEventStreamSource,TEventStreamEntry,TListeningHandlers}.ListeningEventStreamHandlers"/> on the given <paramref name="eventStreamHandler"/> using data from all available <see cref="ISources{TEventStreamSource}.Sources"/>.
    /// </summary>
    public static async IAsyncEnumerable<EventStreamEntry<Cid>> AdvanceSharedEventStreamAsync<TEventStreamEntryContent>(this IReadOnlyNomadKuboEventStreamHandler<TEventStreamEntryContent> eventStreamHandler, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Playback event stream
        // Order event entries by oldest first
        await foreach (var eventEntry in eventStreamHandler.ResolveEventStreamEntriesAsync(cancellationToken).OrderBy(x => x.TimestampUtc))
        {
            // Advance event stream for all listening objects
            await eventStreamHandler.ListeningEventStreamHandlers
                .Where(x => x.Id == eventEntry.TargetId)
                .InParallel(x => x.TryAdvanceEventStreamAsync(eventEntry, cancellationToken));

            yield return eventEntry;
        }
    }

    /// <summary>
    /// Resolves the full event stream from all <see cref="ISources{T}.Sources"/> organized by date and advances the <paramref name="eventStreamHandler"/> to the given <paramref name="maxDateTimeUtc"/>.
    /// </summary>
    public static async IAsyncEnumerable<EventStreamEntry<Cid>> AdvanceEventStreamToAtLeastAsync<TEventStreamEntryContent>(this IReadOnlyNomadKuboEventStreamHandler<TEventStreamEntryContent> eventStreamHandler, DateTime maxDateTimeUtc, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Playback event stream
        // Order event entries by oldest first
        await foreach (var eventEntry in eventStreamHandler.ResolveEventStreamEntriesAsync(cancellationToken)
                           .OrderBy(x => x.TimestampUtc)
                           .Where(x => (x.TimestampUtc ?? ThrowHelper.ThrowArgumentNullException<DateTime>()) <= maxDateTimeUtc)
                           .WithCancellation(cancellationToken))
        {
            // Advance event stream for all listening objects
            await eventStreamHandler.TryAdvanceEventStreamAsync(eventEntry, cancellationToken);
            yield return eventEntry;
        }
    }

    /// <summary>
    /// Resolves the full event stream from all sources, organized by date.
    /// </summary>
    /// <param name="eventStreamHandler">The event stream handler whose sources are resolved to crawl for event stream entries.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async IAsyncEnumerable<EventStreamEntry<Cid>> ResolveEventStreamEntriesAsync<TEventStreamEntryContent>(this IReadOnlyNomadKuboEventStreamHandler<TEventStreamEntryContent> eventStreamHandler, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Resolve initial sources
        var sourceEvents = new Dictionary<Cid, Dictionary<Cid, EventStreamEntry<Cid>>>();

        foreach (var source in eventStreamHandler.Sources)
            sourceEvents.Add(source, []);

        // Track sources that shouldn't be yielded
        // Remove from this if source is re-added
        var removedSources = new HashSet<Cid>();

        var queue = new Queue<KeyValuePair<Cid, Dictionary<Cid, EventStreamEntry<Cid>>>>(sourceEvents);
        while (queue.Count > 0 && queue.Dequeue() is var sourceKvp)
        {
            // Resolve event stream for each source
            var sourceCid = sourceKvp.Key;
            Guard.IsNotNullOrWhiteSpace(sourceCid);

            var eventStream = await eventStreamHandler.ResolveContentPointerAsync<EventStream<Cid>, TEventStreamEntryContent>(sourceCid, cancellationToken);
            Guard.IsNotNullOrWhiteSpace(eventStream.TargetId);
            Guard.IsNotNullOrWhiteSpace(eventStreamHandler.Id);

            if (removedSources.Contains(sourceCid))
            {
                Logger.LogWarning($"Source {sourceCid} was marked as removed and has been skipped. It will not be resolved unless it is re-added.");
                continue;
            }

            if (eventStream.TargetId != eventStreamHandler.Id)
            {
                Logger.LogWarning($"Event stream {nameof(eventStream.TargetId)} {eventStream.TargetId} does not match event stream handler Id {eventStreamHandler.Id}, skipping");
                continue;
            }

            // Resolve and collect event stream entries
            var entriesDict = sourceKvp.Value;
            foreach (var entryCid in eventStream.Entries)
            {
                Guard.IsNotNullOrWhiteSpace(entryCid);
                var entry = await eventStreamHandler.ResolveContentPointerAsync<EventStreamEntry<Cid>, TEventStreamEntryContent>(entryCid, cancellationToken);
                entriesDict[entryCid] = entry;

                Guard.IsNotNullOrWhiteSpace(entry.TargetId);
                Guard.IsNotNullOrWhiteSpace(entry.EventId);
                Guard.IsNotNullOrWhiteSpace(entry.Content);

                if (entry.TargetId != eventStreamHandler.Id)
                {
                    Logger.LogWarning($"Event stream entry {nameof(entry.TargetId)} {entry.TargetId} does not match event handler Id {eventStreamHandler.Id}, skipping");
                    continue;
                }

                // Added source
                if (entry.EventId == nameof(SourceAddEvent))
                {
                    var sourceAddEvent = await eventStreamHandler.ResolveContentPointerAsync<SourceAddEvent, TEventStreamEntryContent>(entry.Content, cancellationToken);
                    Guard.IsNotNullOrWhiteSpace(sourceAddEvent.AddedSourcePointer);
                    Guard.IsNotNullOrWhiteSpace(sourceAddEvent.EventId);
                    Guard.IsNotNullOrWhiteSpace(sourceAddEvent.TargetId);

                    // Add to handler
                    if (!eventStreamHandler.Sources.Contains(sourceAddEvent.AddedSourcePointer))
                        eventStreamHandler.Sources.Add(sourceAddEvent.AddedSourcePointer);

                    // Add to queue
                    var newKvp = new KeyValuePair<Cid, Dictionary<Cid, EventStreamEntry<Cid>>>(sourceAddEvent.AddedSourcePointer, []);
                    queue.Enqueue(newKvp);
                    sourceEvents.Add(newKvp.Key, newKvp.Value);

                    // Unmark as removed if needed
                    if (removedSources.Contains(sourceAddEvent.AddedSourcePointer))
                        removedSources.Remove(sourceAddEvent.AddedSourcePointer);
                }
                // Removed source
                else if (entry.EventId == nameof(SourceRemoveEvent))
                {
                    var sourceRemoveEvent = await eventStreamHandler.ResolveContentPointerAsync<SourceRemoveEvent, TEventStreamEntryContent>(entry.Content, cancellationToken);
                    Guard.IsNotNullOrWhiteSpace(sourceRemoveEvent.RemovedSourcePointer);
                    Guard.IsNotNullOrWhiteSpace(sourceRemoveEvent.EventId);
                    Guard.IsNotNullOrWhiteSpace(sourceRemoveEvent.TargetId);

                    if (eventStreamHandler.Sources.Contains(sourceRemoveEvent.RemovedSourcePointer))
                        eventStreamHandler.Sources.Remove(sourceRemoveEvent.RemovedSourcePointer);

                    // Don't want to re-resolve if source is re-added
                    // Rather than removing the event stream source and entries,
                    // mark as 'removed' and don't yield.
                    removedSources.Add(sourceRemoveEvent.RemovedSourcePointer);
                }
            }
        }

        // Emit resolved event stream entries.
        foreach (var eventDataKvp in sourceEvents)
        {
            var sourceCid = eventDataKvp.Key;
            var eventStreamEntries = eventDataKvp.Value;
            Guard.IsNotNullOrWhiteSpace(sourceCid);

            // Exclude removed (not re-added) sources
            if (removedSources.Contains(sourceCid))
            {
                Logger.LogWarning($"Event stream source {sourceCid} was marked as removed, skipping");
                continue;
            }

            foreach (var entry in eventStreamEntries)
            {
                Guard.IsNotNullOrWhiteSpace(entry.Value.Content);
                Guard.IsNotNullOrWhiteSpace(entry.Value.EventId);
                Guard.IsNotNullOrWhiteSpace(entry.Value.TargetId);
                yield return entry.Value;
            }
        }
    }
}