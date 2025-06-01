using CommunityToolkit.Diagnostics;
using Ipfs;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A repository that can be used to create (with parameter), read, update, and delete items under a specific context in Kubo.
/// </summary>
/// <typeparam name="TModifiable">The type of item that can be modified in this repository.</typeparam>
/// <typeparam name="TReadOnly">The type of item that can be read from this repository.</typeparam>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
/// <typeparam name="TEventEntryContent">The base content type of the event stream entries.</typeparam>
/// <typeparam name="TCreateParam"></typeparam>
public abstract class NomadKuboRepository<TModifiable, TReadOnly, TRoaming, TEventEntryContent, TCreateParam> : NomadKuboRepositoryBase<TModifiable, TReadOnly, TRoaming, TEventEntryContent>, INomadKuboRepository<TModifiable, TReadOnly, TCreateParam>
    where TReadOnly : notnull
    where TModifiable : TReadOnly, INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <inheritdoc/>
    public override event EventHandler<TReadOnly[]>? ItemsAdded;

    /// <summary>
    /// Gets a new set of local and roaming key names.
    /// </summary>
    public abstract (string LocalKeyName, string RoamingKeyName) GetNewKeyNames(TCreateParam createParam);

    /// <summary>
    /// Gets the initial event stream label used for a given set of newly created keys.
    /// </summary>
    public abstract string GetNewEventStreamLabel(TCreateParam createParam, IKey roamingKey, IKey localKey);

    /// <summary>
    /// Gets the initial roaming value for a given set of newly created keys.
    /// </summary>
    public abstract TRoaming GetInitialRoamingValue(TCreateParam createParam, IKey roamingKey, IKey localKey);

    /// <inheritdoc/>
    public virtual async Task<TModifiable> CreateAsync(TCreateParam createParam, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keyNames = GetNewKeyNames(createParam);
        var existingConfig = ManagedConfigs.FirstOrDefault(x => x.LocalKeyName == keyNames.LocalKeyName && x.RoamingKeyName == keyNames.RoamingKeyName);
        var config = existingConfig ?? GetEmptyConfig();

        // If an instance cache is set, check if the instance already exists.
        if (InstanceCache is not null && config.RoamingId is not null && InstanceCache.TryGetValue(config.RoamingId, out var cachedInstance))
        {
            // If it exists, return the cached instance.
            return (TModifiable)cachedInstance;
        }

        config.LocalKeyName = keyNames.LocalKeyName;
        config.RoamingKeyName = keyNames.RoamingKeyName;
        config.LocalKey = ManagedKeys.FirstOrDefault(x => x.Name == keyNames.LocalKeyName);
        config.RoamingKey = ManagedKeys.FirstOrDefault(x => x.Name == keyNames.RoamingKeyName);

        // If local is null, roaming should be too, and vice versa
        Guard.IsEqualTo(config.LocalKey is null, config.RoamingKey is null);

        // Setup local and roaming data
        var needsKeySetup = config.NoKeys;
        if (needsKeySetup)
        {
            // Key doesn't exist, create it and return data.
            var (local, roaming) = await NomadKeyGen.CreateAsync(keyNames.LocalKeyName, keyNames.RoamingKeyName, (l, r) => GetNewEventStreamLabel(createParam, r, l), (l, r) => GetInitialRoamingValue(createParam, r, l), Client, cancellationToken);

            // Data isn't published and won't resolve.
            // The known default value must be passed around manually
            // until it is ready to be published.
            config.RoamingId = roaming.Key.Id;
            config.LocalKey = new(local.Key);
            config.RoamingKey = new(roaming.Key);
            config.LocalValue = local.Value;
            config.RoamingValue = roaming.Value;

            // To avoid resolving the unpublished local ipns key, set the resolved entries to empty.
            config.ResolvedEventStreamEntries = [];
            config.Sources.Add(local.Key.Id);

            ManagedKeys.Add(new(local.Key));
            ManagedKeys.Add(new(roaming.Key));
            ManagedConfigs.Add(config);
        }

        Guard.IsNotNullOrWhiteSpace(config.RoamingId?.ToString());

        // Created default or retrieved values are used.
        // Reuse existing handler config instance (for new unpublished data)
        var modifiable = (TModifiable)await GetAsync(config, cancellationToken);

        // If instance cache is set, add the modifiable instance to it.
        if (InstanceCache is not null)
        {
            InstanceCache[config.RoamingId.ToString()] = modifiable;
        }

        // Only raise collection modified if something new was set up
        if (needsKeySetup)
        {
            ItemsAdded?.Invoke(this, [modifiable]);
        }

        return modifiable;
    }
}

/// <summary>
/// A repository that can be used to create, read, update, and delete items under a specific context in Kubo.
/// </summary>
/// <typeparam name="TModifiable">The type of item that can be modified in this repository.</typeparam>
/// <typeparam name="TReadOnly">The type of item that can be read from this repository.</typeparam>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
/// <typeparam name="TEventEntryContent">The base content type of the event stream entries.</typeparam>
public abstract class NomadKuboRepository<TModifiable, TReadOnly, TRoaming, TEventEntryContent> : NomadKuboRepositoryBase<TModifiable, TReadOnly, TRoaming, TEventEntryContent>, INomadKuboRepository<TModifiable, TReadOnly>
    where TReadOnly : notnull
    where TModifiable : TReadOnly, INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <inheritdoc/>
    public override event EventHandler<TReadOnly[]>? ItemsAdded;

    /// <summary>
    /// Gets a new set of local and roaming key names.
    /// </summary>
    public abstract (string LocalKeyName, string RoamingKeyName) GetNewKeyNames();

    /// <summary>
    /// Gets the initial event stream label used for a given set of newly created keys.
    /// </summary>
    public abstract string GetNewEventStreamLabel(IKey roamingKey, IKey localKey);

    /// <summary>
    /// Gets the initial roaming value for a given set of newly created keys.
    /// </summary>
    public abstract TRoaming GetInitialRoamingValue(IKey roamingKey, IKey localKey);

    /// <inheritdoc/>
    public virtual async Task<TModifiable> CreateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keyNames = GetNewKeyNames();
        var existingConfig = ManagedConfigs.FirstOrDefault(x => x.LocalKeyName == keyNames.LocalKeyName && x.RoamingKeyName == keyNames.RoamingKeyName);
        var config = existingConfig ?? GetEmptyConfig();

        config.LocalKeyName = keyNames.LocalKeyName;
        config.RoamingKeyName = keyNames.RoamingKeyName;
        config.LocalKey = ManagedKeys.FirstOrDefault(x => x.Name == keyNames.LocalKeyName);
        config.RoamingKey = ManagedKeys.FirstOrDefault(x => x.Name == keyNames.RoamingKeyName);

        // If local is null, roaming should be too, and vice versa
        Guard.IsEqualTo(config.LocalKey is null, config.RoamingKey is null);

        // Setup local and roaming data
        var needsKeySetup = config.NoKeys;
        if (needsKeySetup)
        {
            // Key doesn't exist, create it and return data.
            var (local, roaming) = await NomadKeyGen.CreateAsync(keyNames.LocalKeyName, keyNames.RoamingKeyName, GetNewEventStreamLabel, GetInitialRoamingValue, Client, cancellationToken);

            // Data isn't published and won't resolve.
            // The known default value must be passed around manually
            // until it is ready to be published.
            config.RoamingId = roaming.Key.Id;
            config.LocalKey = new(local.Key);
            config.RoamingKey = new(roaming.Key);
            config.LocalValue = local.Value;
            config.RoamingValue = roaming.Value;

            // To avoid resolving an unpublished ipns key, set the resolved entries to empty.
            config.ResolvedEventStreamEntries = [];

            ManagedKeys.Add(new(local.Key));
            ManagedKeys.Add(new(roaming.Key));
            ManagedConfigs.Add(config);
        }

        Guard.IsNotNullOrWhiteSpace(config.RoamingId?.ToString());

        // Created default or retrieved values are used.
        // Reuse existing handler config instance (for new unpublished data)
        var modifiable = (TModifiable)await GetAsync(config, cancellationToken);

        // Only raise collection modified if something new was set up
        if (needsKeySetup)
        {
            ItemsAdded?.Invoke(this, [modifiable]);
        }

        return modifiable;
    }
}