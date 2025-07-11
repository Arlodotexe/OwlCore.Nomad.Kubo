# Concept Glossary: Nomad

## Table of Contents

- [Event Stream Sourcing](#event-stream-sourcing)
- [Local state](#local-state)
- [Roaming state](#roaming-state)
- [Event Stream Handlers](#event-stream-handlers)
- [Event Streams](#event-streams)
- [Event Stream Entries](#event-stream-entries)
- ["Root" Event Stream Handler](#root-event-stream-handler)
- ["Virtual" Event Stream Handler](#virtual-event-stream-handler)
- [Repository](#repository)
- [Registry](#registry)

## Event Stream Sourcing

Event Sourcing and Event Streaming are established patterns that Nomad makes heavy use of in tandem.

See also:
- https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing

Rather than using an existing library, we've crafted a custom event sourcing solution under the name [OwlCore.Nomad](https://github.com/Arlodotexe/OwlCore.Nomad).

## Local state

- Published as IPNS keys from each device
- Each device publishes a unique local key
- Each local key contains event streams
- Each event stream only contains data about interactions that happened on *that* device.
- Each device observes the others, but doesn't modify the others
- Event streams are used to create the roaming state
  - Same data in, same data out
  - If given the same event stream entries for all devices, the same roaming state is reached. 


## Roaming state

- Data that is published for consumption by an application or another user
- Represents the sum of interactions from each users' device.
- Result of each device independently computing the same state given its' sources.
- If the same event stream sources are provided to each device (no partitioned updates), the same roaming state is reached.
- Each device publishes this value's CID to the same IPNS key

## Event Stream Handlers

- Takes local event streams as sources
- Produces a roaming state after advancing the stream from sources 
- Roaming state is always published to ipns.
- Virtual event stream handlers don't need to be published to ipns.
- Readonly roaming data (not published by you) only requires an event stream handler to receive updates.

## Event Streams

- Individual sources published separately by each device
- Read by each individual device
- Contain event stream entries

## Event Stream Entries

- Individual interactions a user has with an application
- Recorded in an event stream
- Unique per device, per published event stream
- From each device, aggregated and time ordered to converge on the same final roaming state as other devices

## "Root" Event Stream Handler

An event stream handler is considered 'root' when the handled roaming data is published to the root of an IPNS dag.

Interaction Characteristics:

- Get
  - Must use repo
  - Repo returns a modifiable or read only instance
  - Based on the permissions scoped for that repo.
- Create / Delete
  - Done via Kubo
  - Create: New local/roaming setup
  - Delete: Stop published, delete from Kubo.
- Add / Remove
  - This object can be contained by other event handlers, but is not managed here.
  - This object can contain other event handlers, which is virtual.

## "Virtual" Event Stream Handler

Virtual event stream handlers aren't published. They handle events for a subsection or sub-DAG of the root.

We do not instantiate these with a repository. Instead, these are manually constructed and the required scope of the root is passed down to it as required properties. The "published root" context doesn't change in a virtual event stream handler, so read-only vs modifiable are also persisted and trickled down.

For example, User and Project each have a collection of images. If the User or Project can be modified, so can the image collection.

Interaction Characteristics:

- Get
  - Uses data from a higher-level event handler (virtual or root).
  - Just instantiate normally via Nomad, not via repo.
- Create / Delete
  - Same as above
- Add / Remove
  - Same as above

## Repository

- Directly contrasts with the idea of a "registry".
- Manages "Root" event stream handler lifetime (Get/Create/Delete)
- Acts like a factory for modifiable vs readonly event stream handlers.
- Compares a config object of Kubo data against known scenarios to determine modifiability.
- Given a roaming ID, different config objects can be used based on the context.

Most repositories are created using the inbox [`NomadKuboRepository`](https://github.com/Arlodotexe/OwlCore.Nomad.Kubo/blob/ed523a9397913ad2eade2f61419743d740d0b6ac/src/NomadKuboRepository.cs#L28), for which example usage can be found in public implementations:
- [`RoamingFolderRepository`](https://github.com/Arlodotexe/OwlCore.Nomad.Storage.Kubo/blob/main/src/RoamingFolderRepository.cs) from `OwlCore.Nomad.Storage.Kubo`.
- [`PeerSwarmTrackerRepository`](https://github.com/Arlodotexe/OwlCore.Nomad.Kubo.PeerSwarm/blob/main/src/PeerSwarmTrackerRepository.cs) from `OwlCore.Nomad.Kubo.PeerSwarm`.

## Registry

- Directly contrasts with the idea of a "repository".
- Explicitly doesn't manage lifetime
- Used to track data managed by others.
- Instead of Create/Delete, uses Add/Remove.
- Common pattern in "virtual" event stream handlers.
- Interfaces for [`IModifiableNomadKuboRegistry`](https://github.com/Arlodotexe/OwlCore.Nomad.Kubo/blob/ed523a9397913ad2eade2f61419743d740d0b6ac/src/IModifiableNomadKuboRegistry.cs) and [`IReadOnlyNomadKuboRegistry`](https://github.com/Arlodotexe/OwlCore.Nomad.Kubo/blob/ed523a9397913ad2eade2f61419743d740d0b6ac/src/IReadOnlyNomadKuboRegistry.cs) are provided inbox, but are provided as a convenience for code consistency and nothing more.
