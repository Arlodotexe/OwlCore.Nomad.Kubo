using Ipfs;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// An event stream with event entry content stored on ipfs.
/// </summary>
public record KuboNomadEventStream : EventStream<Cid>;
