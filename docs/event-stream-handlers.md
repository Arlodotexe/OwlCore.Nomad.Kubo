## [**Event Stream Handlers**](./event-stream-handlers.md)

### [Intent and purpose](./event-stream-handlers/intent,purpose.md)
- New scenarios covered:
    - Publish the same data to the same public-facing [ipns](./glossary/ipfs.md#ipns) address *from multiple devices* without a persistent live connection between devices.
    - [Operator](./glossary/nomad.md#operator) can make changes to the single public-facing ipns address from any owned device/node and they'll see it across all their connected devices while other operators see the ["roaming" data](./glossary/nomad.md#roaming-state) published by all their devices.
    - Keep public-facing "roaming" IPNS keys alive using all online and [paired](./pairing.md) devices. 
- Glossary references:
    - "[Operator](./glossary/nomad.md#operator)" as a user analogy
    - "[Local state](./glossary/nomad.md#local-state)" as event streams used to create the roaming state
    - "[Roaming state](./glossary/nomad.md#roaming-state)" as data published for consumption by an application or another user.
    - "[IPNS](./glossary/ipfs.md#ipns)" as the immutability layer over ipfs

### [How they work](./event-stream-handlers/how,explainer.md)
- Input: One or more local event stream sources published per application per device on IPNS.
- Output: A single eventually consistent "roaming data" object published across all operator devices on IPNS.
- In-between: Application functionality over data, "remoting with an abstraction" via multi-device event stream replay to reach a consistent roaming state. 

### [Lifecycle management via `IRepository`](./event-stream-handlers/lifecycle.md)
- Intent and purpose
    - New scenarios covered:
        - Streamline the instantiation, creation and deletion of event stream handlers.
        - Support for single-device many-operator (server-hosted data) scope isolation.
    - Glossary references:
        - "[Operator](./glossary/nomad.md#operator)" as a user analogy
        - "[IRepository](./glossary/nomad.md#repository)" as factory for modifiable vs readonly event stream handlers
- Create, delete, get
    - Automatic ReadOnly vs Modifiable instances
- Define known construct-able instances with `ManagedConfigs`
    - Scoping repositories to individual operators
- "Forking" another operator's event stream to diff a patch
    - Locally modifiable instead of readonly instance, with writes contained to custom/temp event stream
    - Local event stream entries added can be submitted through a sidecar channel (e.g. Patches library) to original operator

### [Event Stream Handler variants](./event-stream-handlers/variants.md)
- Root
    - New scenarios covered
    - How to code one
- Virtual
    - New scenarios covered
    - How to code one
- Hybrid (root-composed-as-virtual)
    - New scenarios covered
    - How to code one
- Glossary references

