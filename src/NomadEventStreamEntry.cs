using Ipfs;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Represents a single entry in an event stream and the data needed to reconstruct it.
/// </summary>
public record KuboNomadEventStreamEntry : EventStreamEntry<Cid>
{
}
