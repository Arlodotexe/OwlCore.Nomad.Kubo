## Documentation

Docs for this project are organized into a sparse hierarchy of files, with sections/subsections and folders/files balanced to keep file lengths reasonable. 

Select a link to expand the content previewed in the outline below it.

### [**Event Stream Handlers**](./docs/event-stream-handlers.md)

#### [Intent and purpose](./docs/event-stream-handlers/intent,purpose.md)
- New scenarios covered:
    - Publish the same data to the same public-facing ipns address *from multiple devices* without a persistent live connection between devices.
    - Operator can make changes to the single public-facing ipns address from any owned device/node and they'll see it across all their connected devices while other operators see the "roaming" data published by all devices.
    - Keep public-facing "roaming" IPNS keys alive using all online and paired devices. 
- Glossary references:
    - "Operator" as a user analogy
    - "Local state" as event streams used to create the roaming state
    - "Roaming state" as data published for consumption by an application or another user.
    - "IPNS" as the immutability layer over ipfs

#### [How they work](./docs/event-stream-handlers/how,explainer.md)
- Input: One or more local event stream sources published per application per device on IPNS.
- Output: A single eventually consistent "roaming data" object published across all operator devices on IPNS.
- In-between: Application functionality over multi-device data interaction, "remoting with an abstraction" via multi-device event stream replay to reach a consistent roaming state. 

#### [Lifecycle management via `IRepository`](./docs/event-stream-handlers/lifecycle.md)
- Intent and purpose
    - New scenarios covered:
        - Streamline the instantiation, creation and deletion of event stream handlers.
        - Support for single-device many-operator (server-hosted data) scope isolation.
    - Glossary references:
        - "Operator" as a user analogy
        - "IRepository" as factory for modifiable vs readonly event stream handlers
- Create, delete, get
    - Automatic ReadOnly vs Modifiable instances
- Define known construct-able instances with `ManagedConfigs`
    - Scoping repositories to individual operators
- "Forking" another operator's event stream to diff a patch
    - Locally modifiable instead of readonly instance, with writes contained to custom/temp event stream
    - Local event stream entries added can be submitted through a sidecar channel (e.g. Patches library) to original operator

#### [Event Stream Handler variants](./docs/event-stream-handlers/variants.md)
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

### [**Pairing**](./docs/pairing.md):
- [Transport](./docs/pairing/transport.md):
    - [Via pubsub](./docs/pairing/transport/pubsub.md) (live connection)
    - [Via sneakernet](./docs/pairing/transport/sneakernet.md) (disjoint/airgapped networks)
- [Data, exchange and handling](./docs/pairing/data,exchange,handling.md)
- [Security and recovery](./docs/pairing/security/recovery.md)

### [**Backups**:](./docs/backups.md)
- [Data in a backup](./docs/backups/data.md):
    - [`ManagedConfigs`](./docs/backups/data/ManagedConfigs.md)
    - [Published IPNS records](./docs/backups/data/ipns/public-records.md)
    - [Private IPNS keys](./docs/backups/data/ipns/private-keys.md)
    - [Block sets (CARs)](./docs/backups/data/CARs.md)
- For each "Data in a backup", we ask: 
    - Where data lives
    - Who "owns" a backup
        - Primarily decided by who controls the ipns keys
        - May be an individual or an organization, unspecified.
    - Who does backup data represent
        - Specified during the export process
        - Could be:
            - Your own data
            - Data of another operator (linked or given)
            - Some or all adjacent resolvable ipns links in any given graph up to some depth. 
    - What can be done with backup data
- [Importing and Exporting](./docs/backups/importing,export.md)

### [**Transports**](./docs/transports.md):

#### [**Overview:**](./docs/transports/overview.md)

##### **Internet vs Intranet vs Sneakernet**:

Thanks to the content-addressed nature of IPFS, Kubo supports WAN, LAN and Sneakernet out of the box and could be extended to support other transport mechanisms like Bluetooth, WiFi direct. 

Content-addressed data (IPFS blocks, IPLD protobufs, unixfs folders, and DHT entries like signed IPNS records) all intrinsically supports these transports, even if not yet supported by Kubo: 
- Internet
    - Directly connecting two non-local machines with Kubo.
    - WAN, ISP-routed, Town, City or Country scale networks or meshes
    - Reliability, access speed/frequency, and geography.
    - Connection locality and lifetime: Live global data transfer.
- Intranet
    - Directly connecting two running local machines with Kubo.
    - LAN, Bluetooth, WiFi Direct, LoRA, home or person scale networks or meshes. 
    - Reliability, access speed/frequency, and environment.
    - Connection locality and lifetime: Live local-nearby data transfer, limited global connection.
- Sneakernet
    - Indirectly connecting two machines via portable media (USB, disk) to move content-addressed data between them via either a third Kubo repo or via sets of CAR export(s)/import(s).
    - USB, physical media.
    - Reliability, access speed/frequency, and material.
    - Connection locality and lifetime: No live connection, direct transfer only.

##### **Sneakernet vs exported backup**:
- TODO
- TBD

#### [**Usage**](./docs/transports/usage.md)

##### Internet
- WAN built into Kubo, including p2p NAT traversal

##### Intranet
- LAN built into Kubo (tcp, udp, quic, websockets, etc.)
- Bluetooth, WiFi Direct, LoRA not built into Kubo

##### Sneakernet
- TODO
- ipfs/kubo repo itself is capable of being put onto a flash drive or external disk and "sneakernet" between machines before Nomad or any particular application is ever involved

### [**Concept Glossary (Terminology):**](./docs/glossary.md)
- [ipfs](./docs/glossary/ipfs.md)
    - Protocol Labs
    - The IPFS Shipyard
    - MultiFormats
    - Content addressing
    - IPLD
        - Data Model, Codecs
        - Linking
        - CAR exporting
    - IPFS
    - Kubo
    - IPNS
- [nomad](./docs/glossary/nomad.md)
    - Event Stream Sourcing
    - Local state
    - Roaming state
    - Event Stream Handlers
    - Event Streams
    - Event Stream Entries
    - "Root" Event Stream Handler
    - "Virtual" Event Stream Handler
    - Repository
    - Registry
    - "Operator" 


