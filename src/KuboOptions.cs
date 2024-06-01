namespace OwlCore.Nomad.Kubo;

/// <inheritdoc cref="IKuboOptions"/>
public record KuboOptions : IKuboOptions
{
    /// <inheritdoc />
    public required bool ShouldPin { get; set; }
    
    /// <inheritdoc />
    public required TimeSpan IpnsLifetime { get; set; }

    /// <inheritdoc />
    public required bool UseCache { get; set; }
}