namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Manages the lifecycle of items in a Nomad Kubo repository.
/// </summary>
public interface INomadKuboRepositoryBase<in TModifiable, TReadOnly> : IReadOnlyNomadKuboRegistry<TReadOnly>
    where TModifiable : TReadOnly
{
    /// <summary>
    /// Deletes an item from this repository.
    /// </summary>
    /// <param name="item">The item to delete.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous deletion of the item.</returns>
    Task DeleteAsync(TModifiable item, CancellationToken cancellationToken);
}
