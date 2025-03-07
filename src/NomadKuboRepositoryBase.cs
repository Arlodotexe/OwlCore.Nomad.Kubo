using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Represents a modifiable Kubo repository.
/// </summary>
public abstract class NomadKuboRepositoryBase<TModifiable, TReadOnly, TRoaming, TEventEntryContent> : INomadKuboRepositoryBase<TModifiable, TReadOnly>
    where TReadOnly : notnull
    where TModifiable : TReadOnly, INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <summary>
    /// The IPFS client used to interact with the network.
    /// </summary>
    public required ICoreApi Client { get; init; }
    
    /// <summary>
    /// Various options to use when interacting with Kubo's API.
    /// </summary>
    public required IKuboOptions KuboOptions { get; init; }

    /// <summary>
    /// The ID of the event stream handler that represents this node.
    /// </summary>
    /// <remarks>
    /// There can be only one 'self' event stream handler config for an item, even in a repository.
    /// </remarks>
    public string? SelfRoamingId { get; protected set; }

    /// <summary>
    /// A delegate that gets a config used to construct an event stream handler given an arbitrary id.
    /// </summary>
    public required GetEventStreamHandlerConfigAsyncDelegate<TRoaming> GetEventStreamHandlerConfigAsync { get; set; }

    /// <summary>
    /// A delegate that gets the default roaming value for an item.
    /// </summary>
    public required Func<IKey, IKey, TRoaming> GetDefaultRoamingValue { get; set; }

    /// <summary>
    /// The default event stream label.
    /// </summary>
    public required string DefaultEventStreamLabel { get; set; }

    /// <summary>
    /// A delegate that constructs a read-only instance from a handler configuration.
    /// </summary>
    public required ReadOnlyFromHandlerConfigDelegate<TReadOnly, TRoaming> ReadOnlyFromHandlerConfig { get; set; }

    /// <summary>
    /// A delegate that constructs a modifiable instance from a handler configuration.
    /// </summary>
    public required ModifiableFromHandlerConfigDelegate<TModifiable, TRoaming> ModifiableFromHandlerConfig { get; set; }

    /// <inheritdoc/>
    public virtual event EventHandler<TReadOnly[]>? ItemsAdded;

    /// <inheritdoc/>
    public virtual event EventHandler<TReadOnly[]>? ItemsRemoved;
    
    /// <summary>
    /// Retrieves the items in the registry.
    /// </summary>
    /// <param name="config">The handler config to use when constructing the instance.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>An async enumerable containing the items in the registry.</returns>
    public virtual async Task<TReadOnly> GetAsync(NomadKuboEventStreamHandlerConfig<TRoaming> config, CancellationToken cancellationToken)
    {
        if (config.RoamingValue is null && config.RoamingId is not null)
        {
            // Roaming value should always be provided on creation (when unpublished)
            // If roaming value is missing but the key is not, it may be published
            // Resolve roaming value if needed.
            var (resolvedRoaming, _) = await Client.ResolveDagCidAsync<TRoaming>(config.RoamingId, nocache: !KuboOptions.UseCache, cancellationToken);
            config.RoamingValue = resolvedRoaming;

            // In this state, the local value may also be missing but resolvable.
            // Resolve local value if needed.
            if (config.LocalValue is null && config.LocalKey is not null)
            {
                var (resolvedLocal, _) = await Client.ResolveDagCidAsync<EventStream<DagCid>>(config.LocalKey.Id, nocache: !KuboOptions.UseCache, cancellationToken);
                config.LocalValue = resolvedLocal;
            }
        }
        
        if (config.LocalValue is null || config.LocalKey is null)
        {
            // An "in-memory" state is required at the callsite where ReadOnly is constructed.
            Guard.IsNotNull(config.RoamingValue);
            Guard.IsNotNull(config.RoamingId);
            return ReadOnlyFromHandlerConfig(config);
        }
        
        // Resolved entries list must be initialized before instantiating handlers.
        // Allows handlers to reference the list when populated here.
        var resolvedEntriesWasNull = config.ResolvedEventStreamEntries is null;
        config.ResolvedEventStreamEntries ??= [];

        // Return modifiable if keys are present.
        // Key should not be null when data is null.
        //  - When data is null, it implies that either it hasn't been published or isn't needed yet, both of which are only possible for a modifiable instance.
        //    - Initial data isn't always required since we can start from a seed state and advance the event stream handler.
        //    - Initial data may be desired anyway when:
        //      - Advancing from a checkpoint, especially the last known roaming state.
        //      - Using all published sources (including the original source node) instead of just the sources paired to you.
        var modifiable = ModifiableFromHandlerConfig(config);
        if (resolvedEntriesWasNull)
        {
            // Resolve entries and advance event stream.
            await foreach (var entry in modifiable.ResolveEventStreamEntriesAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (modifiable.EventStreamHandlerId == entry.TargetId)
                    await modifiable.AdvanceEventStreamAsync(entry, cancellationToken);

                config.ResolvedEventStreamEntries.Add(entry);
            }
        }
        else
        {
            foreach (var entry in config.ResolvedEventStreamEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (modifiable.EventStreamHandlerId == entry.TargetId)
                    await modifiable.AdvanceEventStreamAsync(entry, cancellationToken);
            }
        }

        return modifiable;
    }

    /// <inheritdoc/>
    public virtual async Task<TReadOnly> GetAsync(string id, CancellationToken cancellationToken)
    {
        // Return read-only if keys aren't found.
        // Data should not be null when key is null
        //  - Key should be null when the node is unpaired, which means to create a read-only instance.
        //  - Data must be supplied to create read-only instance, must be pre-populated.
        var config = await GetEventStreamHandlerConfigAsync(id, cancellationToken);
        return await GetAsync(config, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TReadOnly> GetAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var selfEventStreamHandlerConfig = await GetEventStreamHandlerConfigAsync(SelfRoamingId, cancellationToken);

        // List all items stored in this Kubo repo
        // Only yields the item that represents this node.
        // There is only one known in this context, can be overriden in base class.
        if (selfEventStreamHandlerConfig.RoamingId is not null)
            yield return await GetAsync(selfEventStreamHandlerConfig, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task DeleteAsync(TModifiable item, CancellationToken cancellationToken)
    {
        var selfEventStreamHandlerConfig = await GetEventStreamHandlerConfigAsync(SelfRoamingId, cancellationToken);

        if (item.EventStreamHandlerId != selfEventStreamHandlerConfig.RoamingId)
            throw new ArgumentException($"The provided {nameof(item.EventStreamHandlerId)} does not match the {nameof(SelfRoamingId)} for this repository. Only the roaming id that represents this node can be deleted.");

        // Delete the roaming and local keys
        await Client.Key.RemoveAsync(selfEventStreamHandlerConfig.RoamingKeyName, cancellationToken);
        await Client.Key.RemoveAsync(selfEventStreamHandlerConfig.LocalKeyName, cancellationToken);
    }
}

/// <summary>
/// A delegate that gets a configuration used to construct an event stream handler given an arbitrary ID.
/// </summary>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
/// <param name="roamingId">A unique identifier for the roaming data. A null value represents an object yet to be created.</param>
/// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
/// <returns>A task that represents the asynchronous operation. The task result contains the event stream handler configuration.</returns>
public delegate Task<NomadKuboEventStreamHandlerConfig<TRoaming>> GetEventStreamHandlerConfigAsyncDelegate<TRoaming>(string? roamingId, CancellationToken cancellationToken);

/// <summary>
/// A delegate that constructs a read-only instance from a handler configuration.
/// </summary>
/// <param name="handlerConfig">The handler configuration to use.</param>
/// <returns>A new instance of the read-only type.</returns>
public delegate TReadOnly ReadOnlyFromHandlerConfigDelegate<out TReadOnly, TRoaming>(NomadKuboEventStreamHandlerConfig<TRoaming> handlerConfig);

/// <summary>
/// A delegate that constructs a modifiable instance from a handler configuration.
/// </summary>
/// <typeparam name="TModifiable">The type of the result.</typeparam>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
/// <param name="handlerConfig">The handler configuration to use.</param>
/// <returns>A new instance of the modifiable type.</returns>
public delegate TModifiable ModifiableFromHandlerConfigDelegate<out TModifiable, TRoaming>(NomadKuboEventStreamHandlerConfig<TRoaming> handlerConfig)
    where TModifiable : IEventStreamHandler<DagCid, Cid, EventStream<DagCid>, EventStreamEntry<DagCid>>;