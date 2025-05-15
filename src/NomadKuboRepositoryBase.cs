using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Diagnostics;
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
    /// The config objects for event stream handlers whose lifecycles are managed by this repository.
    /// </summary>
    public ICollection<NomadKuboEventStreamHandlerConfig<TRoaming>> ManagedConfigs { get; protected set; } = [];

    /// <summary>
    /// The keys that this node operator has access to.
    /// </summary>
    public required ICollection<IKey> ManagedKeys { get; init; } = new List<IKey>();

    /// <summary>
    /// Creates a new empty instance of <see cref="NomadKuboEventStreamHandlerConfig{TRoaming}"/> to use or manage in this repository.
    /// </summary>
    protected abstract NomadKuboEventStreamHandlerConfig<TRoaming> GetEmptyConfig();

    /// <summary>
    /// Gets an event stream handler config used to construct existing ReadOnly (managed by another repository or node operator) and existing Modifiable (lifecycle managed by this repository or node operator) event stream handlers. New Modifiable configurations are not handled here.
    /// </summary>
    /// <remarks>
    /// If local and roaming keys are returned, it can be used to create a modifiable event stream handler.
    /// <para/>
    /// If no local and roaming keys are returned but a roaming ID is, it can be used to create read-only instance.
    /// </remarks>
    /// <param name="roamingId">A unique identifier for the roaming data.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the event stream handler configuration.</returns>
    public virtual async Task<NomadKuboEventStreamHandlerConfig<TRoaming>> GetExistingConfigAsync(string roamingId, CancellationToken cancellationToken)
    {
        var keyNames = await GetExistingKeyNamesAsync(roamingId, cancellationToken);
        keyNames ??= (string.Empty, string.Empty);
        
        // The GetNomadKeysAsync helper method does three things:
        // ------------------------------------
        // Finds roaming key by a given roaming key id
        // Finds both roaming key and roaming key id by a given roaming key name
        // Finds local key by a given local key name
        // ------------------------------------
        // Handles all three possible configuration:
        // - Existing readonly (has roamingId but not roaming key)
        // - Existing modifiable (has roamingId and roaming Key)
        // - New modifiable (unused at this call site)
        // ------------------------------------
        var (localKey, roamingKey, foundRoamingId) = await NomadKeyHelpers.GetNomadKeysAsync(roamingId, keyNames.Value.RoamingKeyName, keyNames.Value.LocalKeyName, ManagedKeys.ToArray(), cancellationToken);

        var config = GetEmptyConfig();
        config.RoamingId = roamingKey?.Id ?? (foundRoamingId is not null ? Cid.Decode(foundRoamingId) : null);
        config.RoamingKey = roamingKey;
        config.RoamingKeyName = roamingKey?.Name;
        config.LocalKey = localKey;
        config.LocalKeyName = localKey?.Name;
        return config;
    }

    /// <summary>
    /// Returns the known local and roaming key names for the provided roaming ID.
    /// </summary>
    /// <param name="roamingId">The ID of the roaming key to retrieve the local and roaming key names of.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public abstract Task<(string LocalKeyName, string RoamingKeyName)?> GetExistingKeyNamesAsync(string roamingId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Constructs a read-only instance from a handler configuration.
    /// </summary>
    public abstract TReadOnly ReadOnlyFromHandlerConfig(NomadKuboEventStreamHandlerConfig<TRoaming> handlerConfig);

    /// <summary>
    /// Constructs a modifiable instance from a handler configuration.
    /// </summary>
    public abstract TModifiable ModifiableFromHandlerConfig(NomadKuboEventStreamHandlerConfig<TRoaming> handlerConfig);
    
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
        cancellationToken.ThrowIfCancellationRequested();
        
        // Resolve roaming value if needed.
        // If roaming value is missing but the key is not, it may be published
        // Roaming value should always be provided on creation (when unpublished)
        if (config.CanAndShouldResolveRoamingValue)
        {
            Guard.IsNotNull(config.RoamingId);
            var (resolvedRoamingValue, _) = await Client.ResolveDagCidAsync<TRoaming>(config.RoamingId, nocache: !KuboOptions.UseCache, cancellationToken);
            config.RoamingValue = resolvedRoamingValue;

            // In this state, the local value may also be missing but resolvable.
            // Resolve local value if needed.
            if (config.CanAndShouldResolveLocalValue)
            {
                Guard.IsNotNull(config.LocalKey);
                var (resolvedLocalEventStream, _) = await Client.ResolveDagCidAsync<EventStream<DagCid>>(config.LocalKey.Id, nocache: !KuboOptions.UseCache, cancellationToken);
                config.LocalValue = resolvedLocalEventStream;
            }
        }

        // If the local event stream key/value pair are missing, assume readonly.
        // Otherwise, assume modifiable.
        if (!config.HasLocalKvp)
        {
            // An "in-memory" state is required at the callsite where ReadOnly is constructed.
            Guard.IsNotNull(config.RoamingValue);
            Guard.IsNotNull(config.RoamingId);
            
            return ReadOnlyFromHandlerConfig(config);
        }

        // Resolved entries list must be initialized before instantiating handlers.
        // Allows handlers to reference the list that we populate below.
        var resolvedEntriesWasNull = config.ResolvedEventStreamEntries is null;
        config.ResolvedEventStreamEntries ??= [];

        // Return modifiable if keys are present.
        // Key should not be null when data is null.
        //  - When data is null, it implies that either it hasn't been published or isn't needed yet, both of which are only possible for a modifiable instance (meaning a key should be present)
        //    - Initial data isn't always required since we can start from a seed state and advance the event stream handler.
        //    - Initial data may be desired anyway when:
        //      - Advancing from a checkpoint, especially the last known roaming state (e.g. live updates)
        //      - Using all published sources (including the original source node) instead of just the sources paired to you.
        var modifiable = ModifiableFromHandlerConfig(config);
        if (resolvedEntriesWasNull)
        {
            // Resolve entries and advance event stream.
            await foreach (var entry in modifiable.ResolveEventStreamEntriesAsync(cancellationToken)
                               .Where(x => (x.TimestampUtc ?? ThrowHelper.ThrowArgumentNullException<DateTime>()) <= DateTime.UtcNow)
                               .OrderBy(x => x.TimestampUtc)
                               .WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (modifiable.EventStreamHandlerId == entry.TargetId)
                    await modifiable.AdvanceEventStreamAsync(entry, cancellationToken);

                config.ResolvedEventStreamEntries.Add(entry);
            }
        }
        else
        {
            foreach (var entry in config.ResolvedEventStreamEntries
                         .Where(x => (x.TimestampUtc ?? ThrowHelper.ThrowArgumentNullException<DateTime>()) <= DateTime.UtcNow)
                         .OrderBy(x => x.TimestampUtc))
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
        cancellationToken.ThrowIfCancellationRequested();

        // Iterate through the managed configs to find the one with the specified roaming ID.
        var existingManagedConfig = ManagedConfigs.FirstOrDefault(x => x.RoamingId?.ToString() == id);
        if (existingManagedConfig is not null)
            return await GetAsync(existingManagedConfig, cancellationToken);

        // If given roaming id isn't managed by this repo, construct a config using roaming id, key names (if any) and ManagedKeys.
        var config = await GetExistingConfigAsync(id, cancellationToken);

        // Return read-only if keys aren't found.
        // Return modifiable if keys are found.
        // Data should not be null when key is null (readonly)
        //  - Key should be null when the node is unpaired, which means to create a read-only instance.
        //  - Data must be supplied to create read-only instance, must be pre-populated.
        return await GetAsync(config, cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async IAsyncEnumerable<TReadOnly> GetAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var managedConfig in ManagedConfigs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Logger.LogInformation($"Getting event stream handler for roaming key {managedConfig}.");

            // Get the roaming id for this node.
            Guard.IsNotNull(managedConfig.RoamingId);
            yield return await GetAsync(managedConfig, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public virtual async Task DeleteAsync(TModifiable item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingManagedConfig = ManagedConfigs.FirstOrDefault(x => x.RoamingId == item.EventStreamHandlerId);
        if (existingManagedConfig is null)
            throw new InvalidOperationException($"The item with {nameof(item.EventStreamHandlerId)} {item.EventStreamHandlerId} does not exist in this repository.");

        var config = await GetExistingConfigAsync(item.EventStreamHandlerId, cancellationToken);
        Guard.IsEqualTo(item.EventStreamHandlerId, config.RoamingId?.ToString() ?? string.Empty);

        Guard.IsNotNull(config.RoamingKeyName);
        Guard.IsNotNull(config.LocalKeyName);
        Logger.LogInformation($"Deleting roaming key {config.RoamingKeyName} and local key {config.LocalKeyName}.");
        
        // Delete the roaming and local keys
        await Client.Key.RemoveAsync(config.RoamingKeyName, cancellationToken);
        await Client.Key.RemoveAsync(config.LocalKeyName, cancellationToken);
        
        ManagedConfigs.Remove(existingManagedConfig);
        ItemsRemoved?.Invoke(this, [item]);
    }
}