﻿using CommunityToolkit.Diagnostics;
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
    public static async Task<EventStreamEntry<Cid>> AppendEventStreamEntryAsync<T>(this INomadKuboEventStreamHandler<T> handler, Cid updateEventContentCid, string eventId, string targetId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var client = handler.Client;

        using (await AppendLock.DisposableWaitAsync(cancellationToken: cancellationToken))
        {
            // Get local event stream.
            cancellationToken.ThrowIfCancellationRequested();

            // Append the event to the local event stream.
            var newEventStreamEntry = new EventStreamEntry<Cid>
            {
                TargetId = targetId,
                EventId = eventId,
                TimestampUtc = DateTime.UtcNow,
                Content = updateEventContentCid,
            };

            // Get new cid for new local event stream entry.
            var newEventStreamEntryCid = await client.Dag.PutAsync(newEventStreamEntry, pin: handler.KuboOptions.ShouldPin, cancel: cancellationToken);

            // Add new entry cid to event stream content.
            handler.LocalEventStream.Entries.Add(newEventStreamEntryCid);

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
    public static async Task<EventStreamEntry<Cid>> ContentPointerToStreamEntryAsync(Cid cid, ICoreApi client,
        bool useCache, CancellationToken token)
    {
        var (streamEntry, _) = await client.ResolveDagCidAsync<EventStreamEntry<Cid>>(cid, nocache: !useCache, token);
        Guard.IsNotNull(streamEntry);
        return streamEntry;
    }

    /// <summary>
    /// Resolves the full event stream from all <see cref="ISources{T}.Sources"/> organized by date and advances the <paramref name="eventStreamHandler"/> to the given <paramref name="maxDateTimeUtc"/>.
    /// </summary>
    /// <param name="eventStreamHandler">The event stream handler whose sources are resolved to crawl for event stream entries.</param>
    /// <param name="maxDateTimeUtc">The max datetime to advance the stream to.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async IAsyncEnumerable<EventStreamEntry<Cid>> AdvanceEventStreamToAtLeastAsync<TEventStreamEntryContent>(this INomadKuboEventStreamHandler<TEventStreamEntryContent> eventStreamHandler, DateTime maxDateTimeUtc, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
    public static async IAsyncEnumerable<EventStreamEntry<Cid>> ResolveEventStreamEntriesAsync<TEventStreamEntryContent>(this INomadKuboEventStreamHandler<TEventStreamEntryContent> eventStreamHandler, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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
                Guard.IsNotNullOrWhiteSpace(entryCid);
                var entry = await eventStreamHandler.ResolveContentPointerAsync<EventStreamEntry<Cid>, TEventStreamEntryContent>(entryCid, cancellationToken);
                Guard.IsNotNull(entry);
                entriesDict[entryCid] = entry;

                if (entry.Content is null)
                    throw new ArgumentNullException(nameof(entry.Content), $"{nameof(entry.Content)} was unexpectedly null on {entryCid}");

                Guard.IsNotNullOrWhiteSpace(entry.TargetId);
                Guard.IsNotNullOrWhiteSpace(entry.EventId);

                // Added source
                if (entry.EventId == nameof(SourceAddEvent))
                {
                    var sourceAddEvent = await eventStreamHandler.ResolveContentPointerAsync<SourceAddEvent, TEventStreamEntryContent>(entry.Content, cancellationToken);
                    Guard.IsNotNullOrWhiteSpace(sourceAddEvent.AddedSourcePointer);
                    Guard.IsNotNullOrWhiteSpace(sourceAddEvent.EventId);
                    Guard.IsNotNullOrWhiteSpace(sourceAddEvent.TargetId);

                    // Add to handler
                    if (eventStreamHandler.Sources.All(x => x != sourceAddEvent.AddedSourcePointer))
                    {
                        eventStreamHandler.Sources.Add(sourceAddEvent.AddedSourcePointer);
                        Logger.LogInformation($"Added source {sourceAddEvent.AddedSourcePointer} to event stream handler {eventStreamHandler.EventStreamHandlerId}");
                    }

                    // Add to queue
                    var newKvp = new KeyValuePair<Cid, Dictionary<Cid, EventStreamEntry<Cid>>>(sourceAddEvent.AddedSourcePointer, []);

                    if (queue.All(x => x.Key != newKvp.Key))
                        queue.Enqueue(newKvp);

                    if (!sourceEvents.ContainsKey(newKvp.Key))
                        sourceEvents.Add(newKvp.Key, newKvp.Value);

                    Logger.LogInformation($"Enqueued new source {newKvp.Key} for entry resolution");

                    // Unmark as removed if needed
                    if (removedSources.Any(x => x == sourceAddEvent.AddedSourcePointer))
                    {
                        removedSources.Remove(sourceAddEvent.AddedSourcePointer);
                        Logger.LogInformation($"Unmarked source {sourceAddEvent.AddedSourcePointer} as removed {eventStreamHandler.EventStreamHandlerId}");
                    }
                }
                // Removed source
                else if (entry.EventId == nameof(SourceRemoveEvent))
                {
                    var sourceRemoveEvent = await eventStreamHandler.ResolveContentPointerAsync<SourceRemoveEvent, TEventStreamEntryContent>(entry.Content, cancellationToken);
                    Guard.IsNotNullOrWhiteSpace(sourceRemoveEvent.RemovedSourcePointer);
                    Guard.IsNotNullOrWhiteSpace(sourceRemoveEvent.EventId);
                    Guard.IsNotNullOrWhiteSpace(sourceRemoveEvent.TargetId);

                    if (eventStreamHandler.Sources.Contains(sourceRemoveEvent.RemovedSourcePointer))
                    {
                        Logger.LogInformation($"Removed source {sourceRemoveEvent.RemovedSourcePointer} from event stream handler {eventStreamHandler.EventStreamHandlerId}");
                        eventStreamHandler.Sources.Remove(sourceRemoveEvent.RemovedSourcePointer);
                    }

                    // Don't want to re-resolve if source is re-added
                    // Rather than removing the event stream source and entries,
                    // mark as 'removed' and don't yield.
                    removedSources.Add(sourceRemoveEvent.RemovedSourcePointer);
                    Logger.LogInformation($"Marked source {sourceRemoveEvent.RemovedSourcePointer} as removed {eventStreamHandler.EventStreamHandlerId}");
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
                Guard.IsNotNullOrWhiteSpace(entry.Value.Content);
                Guard.IsNotNullOrWhiteSpace(entry.Value.EventId);
                Guard.IsNotNullOrWhiteSpace(entry.Value.TargetId);

                if (entry.Value.EventId != nameof(SourceAddEvent) && entry.Value.EventId != nameof(SourceRemoveEvent))
                    yield return entry.Value;
            }
        }
    }
}