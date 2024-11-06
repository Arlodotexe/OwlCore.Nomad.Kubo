using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.ComponentModel;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Represents a modifiable Kubo repository.
/// </summary>
public class NomadKuboRepository<TModifiable, TReadOnly, TRoaming, TEventEntryContent> : INomadKuboRepository<TModifiable, TReadOnly>
    where TReadOnly : notnull
    where TModifiable : TReadOnly, INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <summary>
    /// The IPFS client used to interact with the network.
    /// </summary>
    public required ICoreApi Client { get; init; }

    /// <summary>
    /// The ID of the event stream handler that represents this node.
    /// </summary>
    /// <remarks>
    /// There can be only one 'self' event stream handler config for an item, even in a repository.
    /// </remarks>
    public string? SelfRoamingId { get; set; }

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
    public event EventHandler<TReadOnly[]>? ItemsAdded;

    /// <inheritdoc/>
    public event EventHandler<TReadOnly[]>? ItemsRemoved;

    /// <inheritdoc/>
    public virtual async Task<TReadOnly> GetAsync(string id, CancellationToken cancellationToken)
    {
        // Return read-only if keys aren't found.
        // Data should not be null when key is null
        //  - Key should be null when the node is unpaired, which means to create a read-only instance.
        //  - Data must be supplied to create read-only instance, must be pre-populated.
        var config = await GetEventStreamHandlerConfigAsync(id, cancellationToken);
        if (config.LocalValue is null || config.LocalKey is null)
        {
            // An "in-memory" state is required at the callsight where ReadOnly is constructed.
            Guard.IsNotNull(config.RoamingValue);
            Guard.IsNotNull(config.RoamingKey);
            return ReadOnlyFromHandlerConfig(config);
        }

        // Return modifiable if keys are present.
        // Key should not be null when data is null.
        //  - When data is null, it implies that either it hasn't been published or isn't needed yet, both of which are only possible for a modifiable instance.
        //    - Initial data isn't always required since we can start from a seed state and advance the event stream handler.
        //    - Initial data may be desired anyway when:
        //      - Advancing from a checkpoint, especially the last known roaming state.
        //      - Using all published sources (including the original source node) instead of just the sources paired to you.
        var modifiable = ModifiableFromHandlerConfig(config);

        // Resolve entries and advance event stream.
        await foreach (var entry in modifiable.ResolveEventStreamEntriesAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (modifiable.EventStreamHandlerId == entry.TargetId)
            {
                await modifiable.AdvanceEventStreamAsync(entry, cancellationToken);
                modifiable.AllEventStreamEntries.Add(entry);
            }
        }

        return modifiable;
    }

    /// <inheritdoc/>
    public virtual async Task<TModifiable> CreateAsync(CancellationToken cancellationToken)
    {
        var selfEventStreamHandlerConfig = await GetEventStreamHandlerConfigAsync(SelfRoamingId, cancellationToken);

        // If local is null, roaming should be too, and vice versa
        Guard.IsEqualTo(selfEventStreamHandlerConfig.LocalKey is null, selfEventStreamHandlerConfig.RoamingKey is null);

        // Setup local and roaming data
        var needsSetup = selfEventStreamHandlerConfig.RoamingId is null ||
                         selfEventStreamHandlerConfig.LocalKey is null ||
                         selfEventStreamHandlerConfig.RoamingKey is null;

        if (needsSetup)
        {
            // Key doesn't exist, create it and return data.
            // Data isn't published and won't resolve.
            // The known default value must be passed around manually
            // until it is ready to be published.
            var (local, roaming) = await NomadKeyGen.CreateAsync(selfEventStreamHandlerConfig.LocalKeyName, selfEventStreamHandlerConfig.RoamingKeyName, eventStreamLabel: DefaultEventStreamLabel, GetDefaultRoamingValue, Client, cancellationToken);

            SelfRoamingId = roaming.Key.Id;
            selfEventStreamHandlerConfig.RoamingId = roaming.Key.Id;
            selfEventStreamHandlerConfig.LocalKey = local.Key;
            selfEventStreamHandlerConfig.RoamingKey = roaming.Key;
            selfEventStreamHandlerConfig.LocalValue = local.Value;
            selfEventStreamHandlerConfig.RoamingValue = roaming.Value;
        }

        Guard.IsNotNull(selfEventStreamHandlerConfig.RoamingId);

        // Created default or retrieved values are used.
        var modifiable = await GetAsync(selfEventStreamHandlerConfig.RoamingId, cancellationToken);

        return (TModifiable)modifiable;
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TReadOnly> GetAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var selfEventStreamHandlerConfig = await GetEventStreamHandlerConfigAsync(SelfRoamingId, cancellationToken);

        // List all items stored in this Kubo repo
        // Only yields the item that represents this node.
        // There can only be one.
        if (selfEventStreamHandlerConfig.RoamingId is not null)
            yield return await GetAsync(selfEventStreamHandlerConfig.RoamingId, cancellationToken);
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
    where TModifiable : IEventStreamHandler<Cid, EventStream<Cid>, EventStreamEntry<Cid>>;