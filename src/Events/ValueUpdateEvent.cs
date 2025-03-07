using Ipfs;

namespace OwlCore.Nomad.Kubo.Events;

/// <summary>
/// Used to set property values, set an item in a bag or collection, or set the value for a key. 
/// </summary>
/// <remarks>
/// This one event should cover everything we need: properties, bags, lists, hashsets, dictionaries, sorted or unsorted, and so on. We took the individual common traits between them all and put them into a "trait space" where we could do a per-hyperedge complete bipartite component analysis with the other traits in the space. 
/// <para/>
/// It started out like this:
/// <para/>
/// ```
/// <para/>
/// - Ordered vs Unordered
/// <para/>
/// - Positional vs Non-positional
/// <para/>
/// - Unique/Distinguishable vs Non-Unique/Indistinguishable
/// <para/>
/// - Keyed vs Non-Keyed
/// <para/>
/// - Capacity-limit vs No capacity limit
/// <para/>
/// - Mutable vs Immutable
/// <para/>
/// - In/Add vs Out/Remove
/// <para/>
/// ```
/// <para/>
/// Then, picking one hyperedge (here, it's position) and doing the comparison, we get a DAG with one way to stack the types:
/// <para/>
/// ```
/// <para/>
/// - Ordered vs Unordered
/// <para/>
/// - Positional vs Non-positional:
/// <para/>
/// --- Positional indexing
/// <para/>
/// -----Integer indexing (ordered, non-unique)
/// <para/>
/// ------ Unrestricted access (list)
/// <para/>
/// ------ End-based access (queue, stack, deque)
/// <para/>
/// -------- First (in/out) (temporal/positional)
/// <para/>
/// -------- Last (in/out) (temporal/positional)
/// <para/>
/// ---- Key indexing (unique)
/// <para/>
/// ------ Ordered (SortedDictionary)
/// <para/>
/// ------ Unordered (Dictionary)
/// <para/>
/// --- Single position (properties)
/// <para/>
/// --- No positional indexing (unordered list, bag)
/// <para/>
/// - Unique/Distinguishable vs Non-Unique/Indistinguishable
/// <para/>
/// - Keyed vs Non-Keyed
/// <para/>
/// - Capacity-limit vs No capacity limit
/// <para/>
/// - Mutable vs Immutable
/// <para/>
/// - In/Add vs Out/Remove
/// <para/>
/// ```
/// <para/>
/// Notice how 'ordering' neatly disappears and 'uniqueness' neatly appears as you go from "Fully positional" to "No position of any kind".
/// <para/>
/// We're able to expand any of these top-level axes and explore the boundary to determine existing known configurations from other axes. For example, we expanded along "positional vs non-positional" here, but we could have also expanded along "Ordered vs Unordered" into the other axes to check for consistency. 
/// <para/>
/// In the scenario set up here, the hyperedge picked (positional --- non-positional) was enough information to understand how to flatten it down to a single record type that other types build on top on without changing, while being stored in a time-ordered event stream.
/// </remarks>
public record ValueUpdateEvent(DagCid? Key, DagCid? Value, bool Unset);
