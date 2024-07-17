namespace OwlCore.Nomad.Kubo.Events;

/// <summary>
/// Content for an event entry that signifies a source being removed from the event stream.
/// </summary>
/// <param name="TargetId">A unique identifier for the runtime object this event was applied to.</param>
/// <param name="RemovedSourcePointer">A pointer to the source that was removed.</param>
public record SourceRemoveEvent(string TargetId, string RemovedSourcePointer) : EventEntryContent(TargetId, nameof(SourceRemoveEvent));