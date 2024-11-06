using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;

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
    /// <param name="client">The client to use for communicating with IPFS.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task<(IKey? LocalKey, IKey? RoamingKey, string RoamingId)> RoamingIdToNomadKeysAsync(string? roamingId, string roamingKeyName, string localKeyName, ICoreApi client, CancellationToken cancellationToken)
    {
        IKey? roamingKey = null;
        IKey? localKey = null;

        // roamingId should be null to specify an object that hasn't been created.
        // Only load keys from this node if requested.
        var keys = await client.Key.ListAsync(cancellationToken);
        var existingRoamingKey = keys.FirstOrDefault(x => $"{x.Id}" == $"{roamingId}");

        if (roamingId is null || existingRoamingKey is not null)
        {
            // Get roaming id of this item.
            roamingKey = existingRoamingKey ?? keys.FirstOrDefault(x => x.Name == roamingKeyName);
            if (roamingKey is not null)
            {
                roamingId = roamingKey.Id;
                localKey = keys.FirstOrDefault(x => x.Name == localKeyName);
                return (localKey, roamingKey, roamingId);
            }
        }

        Guard.IsNotNullOrWhiteSpace(roamingId);
        return (localKey, roamingKey, roamingId);
    }
}
