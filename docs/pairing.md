## [**Pairing**](./pairing.md):

The closest analogy to pairing in the client-server model is "login", in the sense that all devices that are "paired" should **only ever** below to the same entity/operator. 

Every node or device running Nomad has [IPNS](./glossary/ipfs.md#ipns) keys for both:
1. A [local](./glossary/nomad.md#local-state) [event stream](./glossary/nomad.md#event-streams) only modified/published by the current node. 
2. A [roaming](./glossary/nomad.md#roaming-state) state published by all "paired" devices.

When a new device is paired to an existing device:
1. The new device creates a new local event stream and relays it to the existing device. 
2. The existing device adds the new device's event stream to its own event stream via [`ReservedEventIds.NomadEventStreamSourceAddEvent`](../src/ReservedEventIds.cs). 
3. The existing device provides the private IPNS key of the roaming state to the new device.
4. The new device replays the event stream to reach and republish the existing roaming value after import.

Once this process is complete:
- The new `NomadEventStreamSourceAddEvent` will be seen by the event stream handler while resolving events on any node. 
- The `Sources` collection on the roaming data will be updated to reflect the event stream sources used to contribute to the roaming state. 

### [Transport](./pairing/transport.md):
- [Via pubsub](./pairing/transport/pubsub.md) (live connection)
- [Via sneakernet](./pairing/transport/sneakernet.md) (disjoint/airgapped networks)


### [Data, exchange and handling](./pairing/data,exchange,handling.md)



### [Security and recovery](./pairing/security/recovery.md)

