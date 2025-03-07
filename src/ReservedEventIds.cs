/// <summary>
/// This class contains all the reserved event ids that are used by the system.
/// </summary>
public static class ReservedEventIds
{
    /// <summary>
    /// Returns all the reserved event ids.
    /// </summary>
    public static IEnumerable<string> GetAll()
    {
        yield return NomadEventStreamSourceAddEvent;
        yield return NomadEventStreamSourceRemoveEvent;
    }

    /// <summary>
    /// The event id for when a source is added.
    /// </summary>
    public const string NomadEventStreamSourceAddEvent = nameof(NomadEventStreamSourceAddEvent);

    /// <summary>
    /// The event id for when a source is removed.
    /// </summary>
    public const string NomadEventStreamSourceRemoveEvent = nameof(NomadEventStreamSourceRemoveEvent);
}