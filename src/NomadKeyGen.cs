using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Kubo;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A helper for creating Nomad keys.
/// </summary>
public static class NomadKeyGen
{
    /// <summary>
    /// Creates roaming and local keys and yields a default recommended value, but does not publish the value to the newly created keys.
    /// </summary>
    /// <param name="localKeyName">The name of the local key to create.</param>
    /// <param name="roamingKeyName">The name of the roaming key to create.</param>
    /// <param name="getDefaultRoamingValue">Given a local and roaming key, returns the default roaming data.</param>
    /// <param name="eventStreamLabel">The label to use for the created local event stream.</param>
    /// <param name="client">A client to use for communicating with ipfs.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task<((IKey Key, EventStream<Cid> Value) Local, (IKey Key, TRoaming Value) Roaming)> CreateAsync<TRoaming>(string localKeyName, string roamingKeyName, string eventStreamLabel, Func<IKey, IKey, TRoaming> getDefaultRoamingValue, ICoreApi client, CancellationToken cancellationToken)
    {
        // Get or create ipns key
        var enumerableKeys = await client.Key.ListAsync(cancellationToken);
        var keys = enumerableKeys as IKey[] ?? enumerableKeys.ToArray();

        var localKey = keys.FirstOrDefault(x => x.Name == localKeyName);
        var roamingKey = keys.FirstOrDefault(x => x.Name == roamingKeyName);
        
        // Key should not be created yet
        Guard.IsNull(localKey);
        Guard.IsNull(roamingKey);
        
        localKey = await client.Key.CreateAsync(localKeyName, "ed25519", size: 4096, cancellationToken);
        roamingKey = await client.Key.CreateAsync(roamingKeyName, "ed25519", size: 4096, cancellationToken);

        // Get default value and cid
        // ---
        // Roaming should not be exported/imported without including at least one local source,
        // otherwise we'd have an extra step during pairing, exporting local separately from roaming from A to B.
        // ---
        // This is can be retroactively handled when publishing an event stream to roaming, but it's best if done in the seed values.
        var defaultLocalValue = new EventStream<Cid>
        {
            Label = eventStreamLabel,
            Entries = [],
        };

        var defaultRoamingValue = getDefaultRoamingValue(localKey, roamingKey);

        return ((localKey, defaultLocalValue), (roamingKey, defaultRoamingValue));
    }

    /// <summary>
    /// Resolves or creates and publishes a local event stream for an event handler.
    /// </summary>
    /// <remarks>This method should only be used to instantiate a node that will be paired with a roaming key from a different node. If you're creating a roaming key for the first time, use <see cref="CreateAsync"/> instead to get recommended roaming values. </remarks>
    /// <param name="localKeyName">The name of the local key to create.</param>
    /// <param name="kuboOptions">Options interacting with ipfs.</param>
    /// <param name="eventStreamLabel">The label to use for the created local event stream.</param>
    /// <param name="client">A client to use for communicating with ipfs.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static Task<(IKey Key, EventStream<Cid> Value)> GetOrCreateLocalAsync(string localKeyName, string eventStreamLabel, IKuboOptions kuboOptions, ICoreApi client, CancellationToken cancellationToken)
    {
        return client.GetOrCreateKeyAsync(localKeyName, _ => new EventStream<Cid>
        {
            Label = eventStreamLabel,
            Entries = [],
        }, kuboOptions.IpnsLifetime, nocache: !kuboOptions.UseCache, size: 4096, cancellationToken);
    }
}