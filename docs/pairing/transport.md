## Pairing Transport:

### [Via pubsub](./transport/pubsub.md) 
- Live connection
- Works inter-node on one single machine
- Works across machines via LAN or WAN where available

### [Via sneakernet](./transport/sneakernet.md)
- Disconnected, disjoint or airgapped networks
- Machines that aren't online simultaneously
- Either a third Kubo repo or via sets of CAR export(s)/import(s).
- A common Kubo node can't be run on both source and target machines