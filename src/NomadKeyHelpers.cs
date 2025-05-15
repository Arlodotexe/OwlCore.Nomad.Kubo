using CommunityToolkit.Diagnostics;
using Ipfs;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Various helpers for Nomad keys.
/// </summary>
public static class NomadKeyHelpers
{
    /// <summary>
    /// Given a roaming id that may or may not be this node depending on the provided <paramref name="roamingId"/>, retrieves the local keys, roaming keys and roaming id. 
    /// </summary>
    /// <param name="roamingId">The roaming ID of the item to retrieve. If this is null or is a value present in this node, a modifiable item will be returned.</param>
    /// <param name="roamingKeyName">The name of the roaming key to use if <paramref name="roamingId"/> is null.</param>
    /// <param name="localKeyName">The name of the local key to use if <paramref name="roamingId"/> is null.</param>
    /// <param name="keys">The keys to check.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task<(IKey? LocalKey, IKey? RoamingKey, string? RoamingId)> GetNomadKeysAsync(string? roamingId, string roamingKeyName, string localKeyName, IKey[] keys, CancellationToken cancellationToken)
    {
        IKey? roamingKey = null;
        IKey? localKey = null;

        // roamingId should be null to specify an object that hasn't been created.
        // Only load keys from this node if requested.
        var existingRoamingKey = keys.FirstOrDefault(x => $"{x.Id}" == $"{roamingId}");

        // Read-only configuration.
        // - Roaming key's ID is known (resolvable),
        // - but the roaming key isn't accessible (publishable).
        if (roamingId is not null && existingRoamingKey is null)
            return (localKey, roamingKey, roamingId);

        // Get roaming key via known key name if needed.
        roamingKey = existingRoamingKey ?? keys.FirstOrDefault(x => x.Name == roamingKeyName);
        
        // "New key" modifiable configuration
        // Roaming key cannot be found via roaming id or via roaming key name.
        if (roamingKey is null)
        {
            // roamingId should also be null if it's a new key 
            Guard.IsNull(roamingId);
            return (localKey, roamingKey, roamingId);
        }
    
        roamingId = roamingKey.Id;
        localKey = keys.FirstOrDefault(x => x.Name == localKeyName);

        // "Existing key" Modifiable configuration
        // The roaming id and key both exist, changes can be published.
        return (localKey, roamingKey, roamingId);
    }
}
