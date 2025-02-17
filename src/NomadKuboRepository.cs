using CommunityToolkit.Diagnostics;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A repository that can be used to create (with parameter), read, update, and delete items under a specific context in Kubo.
/// </summary>
/// <typeparam name="TModifiable">The type of item that can be modified in this repository.</typeparam>
/// <typeparam name="TReadOnly">The type of item that can be read from this repository.</typeparam>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
/// <typeparam name="TEventEntryContent">The base content type of the event stream entries.</typeparam>
/// <typeparam name="TCreateParam"></typeparam>
public abstract class NomadKuboRepository<TModifiable, TReadOnly, TRoaming, TEventEntryContent, TCreateParam> : NomadKuboRepositoryBase<TModifiable, TReadOnly, TReadOnly, TEventEntryContent>, INomadKuboRepository<TModifiable, TReadOnly, TCreateParam>
    where TReadOnly : notnull
    where TModifiable : TReadOnly, INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <inheritdoc/>
    public abstract Task<TModifiable> CreateAsync(TCreateParam createParam, CancellationToken cancellationToken);
}

/// <summary>
/// A repository that can be used to create, read, update, and delete items under a specific context in Kubo.
/// </summary>
/// <typeparam name="TModifiable">The type of item that can be modified in this repository.</typeparam>
/// <typeparam name="TReadOnly">The type of item that can be read from this repository.</typeparam>
/// <typeparam name="TRoaming">The published roaming value type.</typeparam>
/// <typeparam name="TEventEntryContent">The base content type of the event stream entries.</typeparam>
public class NomadKuboRepository<TModifiable, TReadOnly, TRoaming, TEventEntryContent> : NomadKuboRepositoryBase<TModifiable, TReadOnly, TRoaming, TEventEntryContent>, INomadKuboRepository<TModifiable, TReadOnly>
    where TReadOnly : notnull
    where TModifiable : TReadOnly, INomadKuboEventStreamHandler<TEventEntryContent>
{
    /// <inheritdoc/>
    public virtual async Task<TModifiable> CreateAsync(CancellationToken cancellationToken)
    {
        var selfEventStreamHandlerConfig = await GetEventStreamHandlerConfigAsync(SelfRoamingId, cancellationToken);

        // If local is null, roaming should be too, and vice versa
        Guard.IsEqualTo(selfEventStreamHandlerConfig.LocalKey is null, selfEventStreamHandlerConfig.RoamingKey is null);

        // Setup local and roaming data
        var needsSetup = selfEventStreamHandlerConfig.LocalKey is null || selfEventStreamHandlerConfig.RoamingKey is null;
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

            // To avoid resolving an unpublished ipns key, set the resolved entries to empty.
            selfEventStreamHandlerConfig.ResolvedEventStreamEntries = [];
        }

        Guard.IsNotNullOrWhiteSpace(selfEventStreamHandlerConfig.RoamingId?.ToString());

        // Created default or retrieved values are used.
        // Reuse existing handler config instance (for new unpublished data)
        var modifiable = await GetAsync(selfEventStreamHandlerConfig, cancellationToken);

        return (TModifiable)modifiable;
    }
}