namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Represents a modifiable registry of items.
/// </summary>
/// <typeparam name="TReadOnly">The type of item that can be read from this registry.</typeparam>
public interface IModifiableNomadKuboRegistry<TReadOnly> : IReadOnlyNomadKuboRegistry<TReadOnly>
{
    /// <summary>
    /// Saves an item to this registry.
    /// </summary>
    /// <param name="item">The item to register.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddAsync(TReadOnly item, CancellationToken cancellationToken);

    /// <summary>
    /// Marks an item for removal from the registry.
    /// </summary>
    /// <param name="peer">The item to remove from the registry.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RemoveAsync(TReadOnly peer, CancellationToken cancellationToken);
}