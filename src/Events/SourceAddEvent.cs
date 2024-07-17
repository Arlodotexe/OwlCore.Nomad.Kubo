namespace OwlCore.Nomad.Kubo.Events;

/// <summary>
/// Content for an event entry that signifies a source being added to the event stream.
/// </summary>
/// <param name="TargetId">A unique identifier for the runtime object this event was applied to.</param>
/// <param name="AddedSourcePointer">A pointer to the source that was added.</param>
public record SourceAddEvent(string TargetId, string AddedSourcePointer) : EventEntryContent(TargetId, nameof(SourceAddEvent));