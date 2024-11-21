namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Represents a read-only registry of items.
/// </summary>
/// <typeparam name="TReadOnly">The type of item that can be read from this registry.</typeparam>
public interface IReadOnlyNomadKuboRegistry<TReadOnly>
{
    /// <summary>
    /// Retrieves the items in the registry.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>An async enumerable containing the items in the registry.</returns>
    public IAsyncEnumerable<TReadOnly> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves an item by its roaming id.
    /// </summary>
    /// <param name="roamingId">The roaming ipns key to retrieve.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public Task<TReadOnly> GetAsync(string roamingId, CancellationToken cancellationToken);

    /// <summary>
    /// Raised when items in the registry are added.
    /// </summary>
    public event EventHandler<TReadOnly[]>? ItemsAdded;

    /// <summary>
    /// Raised when items in the registry are removed.
    /// </summary>
    public event EventHandler<TReadOnly[]>? ItemsRemoved;
}
