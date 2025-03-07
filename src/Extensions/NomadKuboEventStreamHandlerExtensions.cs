using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;
using OwlCore.ComponentModel;
using OwlCore.Diagnostics;
using OwlCore.Extensions;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Extension methods for <see cref="INomadKuboEventStreamHandler{TEventEntryContent}"/>.
/// </summary>
public static class NomadKuboEventStreamHandlerExtensions
{
    /// <summary>
    /// Handles resolving a dag content pointer for Kubo on an event stream handler.
    /// </summary>
    /// <returns>The resolved content.</returns>
    public static async Task<TResult> ResolveContentPointerAsync<TResult, TEventEntryContent>(this INomadKuboEventStreamHandler<TEventEntryContent> handler, Cid cid, CancellationToken ctk)
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
    /// Publishes the local event stream to the local ipns key on <paramref name="eventStreamHandler"/>.
    /// </summary>
    /// <typeparam name="TEventStreamHandler">The event stream handler type.</typeparam>
    /// <typeparam name="TEventEntryContent">The type for <see cref="EventStreamEntry{T}.Content"/> on this handler.</typeparam>
    /// <param name="eventStreamHandler">The handler to publish the local event stream for.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns></returns>
    public static async Task PublishLocalAsync<TEventStreamHandler, TEventEntryContent>(this TEventStreamHandler eventStreamHandler, CancellationToken cancellationToken)
        where TEventStreamHandler : INomadKuboEventStreamHandler<TEventEntryContent>
    {
        var cid = await eventStreamHandler.Client.Dag.PutAsync(eventStreamHandler.LocalEventStream, pin: eventStreamHandler.KuboOptions.ShouldPin, cancel: cancellationToken);
        Guard.IsNotNull(cid);

        _ = await eventStreamHandler.Client.Name.PublishAsync(cid, lifetime: eventStreamHandler.KuboOptions.IpnsLifetime, key: eventStreamHandler.LocalEventStreamKey.Name, cancel: cancellationToken);
    }

    /// <summary>
    /// Publishes the inner content to the roaming ipns key on <paramref name="eventStreamHandler"/>.
    /// </summary>
    public static async Task PublishRoamingAsync<TEventStreamHandler, TEventEntryContent, TContent>(
        this TEventStreamHandler eventStreamHandler,
        CancellationToken cancellationToken)
        where TContent : class, ISources<Cid>
        where TEventStreamHandler : INomadKuboEventStreamHandler<TEventEntryContent>, IDelegable<TContent>
    {
        var cid = await eventStreamHandler.Client.Dag.PutAsync(eventStreamHandler.Inner, pin: eventStreamHandler.KuboOptions.ShouldPin, cancel: cancellationToken);
        Guard.IsNotNull(cid);

        _ = await eventStreamHandler.Client.Name.PublishAsync(cid, lifetime: eventStreamHandler.KuboOptions.IpnsLifetime, key: eventStreamHandler.RoamingKey.Name, cancel: cancellationToken);
    }

    /// <summary>
    /// A lock for <see cref="AppendEventStreamEntryAsync{T}"/>.
    /// </summary>
    private static SemaphoreSlim AppendLock { get; } = new(1, 1);

    /// <summary>
    /// Appends a new event to the event stream.
    /// </summary>
    /// <param name="handler">The storage interface to operate on.</param>
    /// <param name="updateEventContentCid">The CID to use for the content of this update event.</param>
    /// <param name="eventId">A unique identifier for this event type.</param>
    /// <param name="targetId">A unique identifier for the provided <paramref name="handler"/> that can be used to reapply the event later.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>A task containing the new event stream entry.</returns>
    public static async Task<EventStreamEntry<DagCid>> AppendEventStreamEntryAsync<T>(this INomadKuboEventStreamHandler<T> handler, DagCid updateEventContentCid, string eventId, string targetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = handler.Client;

        using (await AppendLock.DisposableWaitAsync(cancellationToken: cancellationToken))
        {
            // Get local event stream.
            cancellationToken.ThrowIfCancellationRequested();

            // Append the event to the local event stream.
            var newEventStreamEntry = new EventStreamEntry<DagCid>
            {
                TargetId = targetId,
                EventId = eventId,
                TimestampUtc = DateTime.UtcNow,
                Content = updateEventContentCid,
            };

            // Get new cid for new local event stream entry.
            var newEventStreamEntryCid = await client.Dag.PutAsync(newEventStreamEntry, pin: handler.KuboOptions.ShouldPin, cancel: cancellationToken);

            // Add new entry cid to event stream content.
            handler.LocalEventStream.Entries.Add((DagCid)newEventStreamEntryCid);

            return newEventStreamEntry;
        }
    }

    /// <summary>
    /// Converts a nomad event stream entry content pointer to a <see cref="EventStreamEntry{Cid}"/>.
    /// </summary>
    /// <param name="cid">The content pointer to resolve.</param>
    /// <param name="client">The client to use to resolve the pointer.</param>
    /// <param name="useCache">Whether to use cache or not.</param>
    /// <param name="token">A token that can be used to cancel the ongoing operation.</param>
    public static async Task<EventStreamEntry<DagCid>> ContentPointerToStreamEntryAsync(Cid cid, ICoreApi client,
        bool useCache, CancellationToken token)
    {
        var (streamEntry, _) = await client.ResolveDagCidAsync<EventStreamEntry<DagCid>>(cid, nocache: !useCache, token);
        Guard.IsNotNull(streamEntry);
        return streamEntry;
    }

    /// <summary>
    /// Resolves the full event stream from all <see cref="ISources{T}.Sources"/> organized by date and advances the <paramref name="eventStreamHandler"/> to the given <paramref name="maxDateTimeUtc"/>.
    /// </summary>
    /// <param name="eventStreamHandler">The event stream handler whose sources are resolved to crawl for event stream entries.</param>
    /// <param name="maxDateTimeUtc">The max datetime to advance the stream to.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async IAsyncEnumerable<EventStreamEntry<DagCid>> AdvanceEventStreamToAtLeastAsync<TEventStreamEntryContent>(this INomadKuboEventStreamHandler<TEventStreamEntryContent> eventStreamHandler, DateTime maxDateTimeUtc, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.IsNotNull(eventStreamHandler);

        // Playback event stream
        // Order event entries by oldest first
        await foreach (var eventEntry in eventStreamHandler.ResolveEventStreamEntriesAsync(cancellationToken)
                           .Where(x => (x.TimestampUtc ?? ThrowHelper.ThrowArgumentNullException<DateTime>()) <= maxDateTimeUtc)
                           .OrderBy(x => x.TimestampUtc)
                           .WithCancellation(cancellationToken)
                       )
        {
            Guard.IsNotNull(eventEntry);
            await eventStreamHandler.AdvanceEventStreamAsync(eventEntry, cancellationToken);
            yield return eventEntry;
        }
    }

    /// <summary>
    /// Resolves the full event stream from all sources, organized by date.
    /// </summary>
    /// <param name="eventStreamHandler">The event stream handler whose sources are resolved to crawl for event stream entries.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async IAsyncEnumerable<EventStreamEntry<DagCid>> ResolveEventStreamEntriesAsync<TEventStreamEntryContent>(this INomadKuboEventStreamHandler<TEventStreamEntryContent> eventStreamHandler, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Resolve initial sources
        var sourceEvents = new Dictionary<Cid, Dictionary<DagCid, EventStreamEntry<DagCid>>>();

        foreach (var source in eventStreamHandler.Sources)
            sourceEvents.Add(source, []);

        // Track sources that shouldn't be yielded
        // Remove from this if source is re-added
        var removedSources = new HashSet<Cid>();

        var queue = new Queue<KeyValuePair<Cid, Dictionary<DagCid, EventStreamEntry<DagCid>>>>(sourceEvents);
        while (queue.Count > 0 && queue.Dequeue() is var sourceKvp)
        {
            // Resolve event stream for each source
            var sourceCid = sourceKvp.Key;
            Guard.IsNotNullOrWhiteSpace(sourceCid);

            var eventStream = await eventStreamHandler.ResolveContentPointerAsync<EventStream<DagCid>, TEventStreamEntryContent>(sourceCid, cancellationToken);
            Guard.IsNotNullOrWhiteSpace(eventStreamHandler.EventStreamHandlerId);

            if (removedSources.Contains(sourceCid))
            {
                Logger.LogWarning($"Source {sourceCid} was marked as removed and has been skipped. It will not be resolved unless it is re-added.");
                continue;
            }

            // Resolve and collect event stream entries
            var entriesDict = sourceKvp.Value;
            foreach (var entryCid in eventStream.Entries)
            {
                Guard.IsNotNullOrWhiteSpace(entryCid?.ToString());
                var entry = await eventStreamHandler.Client.Dag.GetAsync<EventStreamEntry<DagCid>>(entryCid, cancel: cancellationToken);
                Guard.IsNotNull(entry);
                entriesDict[entryCid] = entry;

                if (entry.Content is null)
                    throw new ArgumentNullException(nameof(entry.Content), $"{nameof(entry.Content)} was unexpectedly null on {entryCid}");

                Guard.IsNotNullOrWhiteSpace(entry.TargetId);
                Guard.IsNotNullOrWhiteSpace(entry.EventId);

                // Added source
                if (entry.EventId == "SourceAddEvent")
                {
                    var entryContent = await eventStreamHandler.Client.Dag.GetAsync<Cid>(entry.Content, cancel: cancellationToken);

                    // Add to handler
                    if (eventStreamHandler.Sources.All(x => x != entryContent))
                    {
                        eventStreamHandler.Sources.Add(entryContent);
                        Logger.LogInformation($"Added source {entryContent} to event stream handler {eventStreamHandler.EventStreamHandlerId}");
                    }

                    // Add to queue
                    var newKvp = new KeyValuePair<Cid, Dictionary<DagCid, EventStreamEntry<DagCid>>>(entryContent, []);

                    if (queue.All(x => x.Key != newKvp.Key))
                        queue.Enqueue(newKvp);

                    if (!sourceEvents.ContainsKey(newKvp.Key))
                        sourceEvents.Add(newKvp.Key, newKvp.Value);

                    Logger.LogInformation($"Enqueued new source {newKvp.Key} for entry resolution");

                    // Unmark as removed if needed
                    if (removedSources.Any(x => x == entryContent))
                    {
                        removedSources.Remove(entryContent);
                        Logger.LogInformation($"Unmarked source {entryContent} as removed {eventStreamHandler.EventStreamHandlerId}");
                    }
                }
                // Removed source
                else if (entry.EventId == "SourceRemoveEvent")
                {
                    var entryContent = await eventStreamHandler.Client.Dag.GetAsync<Cid>(entry.Content, cancel: cancellationToken);

                    if (eventStreamHandler.Sources.Contains(entryContent))
                    {
                        Logger.LogInformation($"Removed source {entryContent} from event stream handler {eventStreamHandler.EventStreamHandlerId}");
                        eventStreamHandler.Sources.Remove(entryContent);
                    }

                    // Don't want to re-resolve if source is re-added
                    // Rather than removing the event stream source and entries,
                    // mark as 'removed' and don't yield.
                    removedSources.Add(entry.Content);
                    Logger.LogInformation($"Marked source {entry.Content} as removed {eventStreamHandler.EventStreamHandlerId}");
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
                Guard.IsNotNull(entry.Value);
                Guard.IsNotNullOrWhiteSpace(entry.Value.Content?.ToString());
                Guard.IsNotNullOrWhiteSpace(entry.Value.EventId);
                Guard.IsNotNullOrWhiteSpace(entry.Value.TargetId);

                if (entry.Value.EventId != "SourceAddEvent" && entry.Value.EventId != "SourceRemoveEvent")
                    yield return entry.Value;
            }
        }
    }
}