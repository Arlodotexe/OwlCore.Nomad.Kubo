namespace OwlCore.Nomad.Kubo.Events;

/// <summary>
/// A base class for content stored in the event stream.
/// </summary>
/// <param name="TargetId">A unique identifier for the target object which this event is applied to.</param>
/// <param name="EventId">A unique identifier of the event. Used for further deserialization.</param>
public abstract record EventEntryContent(string TargetId, string EventId);