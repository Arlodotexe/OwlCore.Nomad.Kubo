namespace OwlCore.Nomad.Kubo;

/// <summary>
/// Manages the lifecycle of items in a Nomad Kubo repository.
/// </summary>
public interface INomadKuboRepository<TModifiable, TReadOnly> : INomadKuboRepositoryBase<TModifiable, TReadOnly>
    where TModifiable : TReadOnly
{
    /// <summary>
    /// Creates an item in this repository.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous creation of an item.</returns>
    Task<TModifiable> CreateAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Manages the lifecycle of items in a Nomad Kubo repository.
/// </summary>
public interface INomadKuboRepository<TModifiable, TReadOnly, in TCreateParam> : INomadKuboRepositoryBase<TModifiable, TReadOnly>
    where TModifiable : TReadOnly
{
    /// <summary>
    /// Creates an item in this repository.
    /// </summary>
    /// <param name="createParam">The parameter used for creation.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous creation of an item.</returns>
    Task<TModifiable> CreateAsync(TCreateParam createParam, CancellationToken cancellationToken);
}