## Intent and purpose of Event Stream Handlers

### New scenarios covered:

**Publish one ipns address from multiple devices**:
- Publish the same data to the same public-facing ipns address *from multiple devices* without a persistent live connection between devices.

**Multi-device multi-view consistency**:
- Eventual consistency for you and others even as devices go offline and online.
- See [how,explainer.md](./how,explainer.md) if you haven't yet.

**Multi-device fallback of published data**
- Keep public-facing "roaming" IPNS keys alive using all online and paired devices. 

### Glossary references:
- "[Operator](../glossary/nomad.md#operator)" as a user analogy
- "[Local state](../glossary/nomad.md#local-state)" as event streams used to create the roaming state
- "[Roaming state](../glossary/nomad.md#roaming-state)" as data published for consumption by an application or another user.
- "[IPNS](../glossary/ipfs.md#ipns)" as the immutability layer over [ipfs](../glossary/ipfs.md#ipfs)

