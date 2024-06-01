namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Options for storing and retrieving data from Ipfs.
/// </summary>
public interface IKuboOptions
{
    /// <summary>
    /// Whether to pin content added to Ipfs.
    /// </summary>
    public bool ShouldPin { get; set; }

    /// <summary>
    /// The lifetime of the ipns key containing the local event stream. Your node will need to be online at least once every <see cref="IpnsLifetime"/> to keep the ipns key alive.
    /// </summary>
    public TimeSpan IpnsLifetime { get; set; }
    
    /// <summary>
    /// Whether to use the cache when resolving Ipns Cids.
    /// </summary>
    public bool UseCache { get; set; }
}