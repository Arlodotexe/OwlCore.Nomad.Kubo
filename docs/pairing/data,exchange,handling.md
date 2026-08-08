# Pairing data, exchange and handling

See [pairing.md](../pairing.md) if you haven't already for a primer overview of the process. 

## How it currently works

Consider two nodes/devices A and B and the data they hold or transfer at each step:

| Step | Data | Action |
| - | - | - |
| 1 | **A**:L=1,R=1 <br> **B**:L=0,R=0  | Init, A creates a local event stream and roaming data and publishes both to ipns. |
| 2 | **A**:L=1,R=1 <br> **B**:L=2,R=0 | Pairing begins, B creates a local event stream and sends it to A. |
| 3 | **A**:L=1,R=1 <br> **B**:L=2,R=0 | A recieves B's freshly created local event stream Id, adds it to its own local event stream via reference in [this](../src/ReservedEventIds.cs) event, and responds with both the 1) roaming private key and 2) local event stream Id of A.  |
| 4 | **A**:L=1,R=1 <br> **B**:L=2,R=1 | B receives the roaming private key ~~and the Id of A's local event stream~~, importing the key, ~~adding A's to its own local event stream via reference in [this](../src/ReservedEventIds.cs) event~~. |

- **L**: Local Event Stream
- **R**: Roaming data
- **0**: Null or n/a
- **1+**: Instance index

### The problem with this

Notice the crossed-out part of step 4:
- In the current setup, we only ever transfer the *new* event stream Id to the *existing* device (B to A), never the other way (A to B). 
- In current code, we rely on the `.Sources` property in the published roaming data for B to learn about A's other paired nodes's local event streams. 
- We do this by resolving the sources list once from roaming during the pairing process, persisting it to disk, and using it on subsequent runs while simply letting the ESH handle adding/removing on the source list. 
- This is a workaround for a problem that Nomad was built to solve.
    - Reading roaming data to inform how to build roaming data can become inconsistent over time without clean recourse to correct it. 
    - This is why we use separate local event streams to record changes.  
    - If the data on-disk is ever corrupted or glitched, it could be misaligned or maliciously aligned *before* the ESH has a say in what to add/remove.
- That it's stored on disk instead of being built from event streams is a problematic attack surface. 

## The minimum we need 

(In terms of data swapped during pairing)

In simple terms, we need to be able to 100% rely on the event streams themselves to compute the correct list of paired devices. 

To do this, we must exhaust possibility states to build cleanly composable layers out of the constraint spaces. 

### "Weak" pairing

Consider three devices A, B and C and their corresponding pairing states:

| A | B | C | Pairing State |
| - | - | - | - |
| `[___]` | `[___]` | `[___]` | Unpaired
| `[B__]` | `[A__]` | `[___]` | A pairs to B
| `[B__]` | `[AC_]` | `[B__]` | B pairs to C


- This creates an *indirect* pairing between A and C through B. 
- A cannot know about C without reading the event stream update from B.
    - Requires B to be online at the same time as A after the pairing process from B to C.
- C *could* know about A from the pairing process with B. 
    - C and B are connected during pairing, B already knows about A. 
    - No other requirements.
- If B were ever unpaired or removed, it would sever A from reading the event stream of C and vice-versa. 
    - To resolve that, we need *Strong pairing*. 

### Unpairing

_Unpairing a device means *none* of its event stream entries are used in computing the roaming data going forward, even event stream entries that pair other devices._

When a weakly paired device is removed, any devices that *it* had paired to are also removed.

**It's not recommended to ever "unpair" a node except to recover from a security incident.**

Simply letting a device go offline forever (while other devices hold the old data) is preferred to unpairing, especially if there are event stream entries in it from user action.

Unpairing should only be leveraged as a **recovery option** in the event that a malicious device is paired. 

### "Strong" pairing

Malicious devices that aren't strongly paired are easy to cut off, such as devices the user doesn't control. 

But what do we do when a strongly paired device the user *does* control is compromised? 

Under the weak pairing setup from above:
- A and C only see each other's event streams because they are mutually paired to B. 
- This means that "unpairing" B would remove the path for A to find C, and vice versa. 

A *strong* pairing would:
- Allow "B" to be unpaired without A and C being unpaired as a side effect.

To set this up:
- When A can't reach B, C *could* notify A about pairing to B (offline, network disjoint, etc.) if **both**:
    1. A is listening on a known topic at the same time C is online (e.g. pubsub)
    2. C has a content-addressed and ipns signed backup of B's event stream
- The end user could choose to "Accept" or "Reject" the pairing of C on device/node A
    - "Accept" of C on A would add a [`ReservedEventIds.NomadEventStreamSourceAddEvent`](../src/ReservedEventIds.cs) to A's event stream entry containing C's event stream ID. 
    - "Reject" of C would add a [`ReservedEventIds.NomadEventStreamSourceRemoveEvent`](../src/ReservedEventIds.cs) to A's event stream entry containing C's event stream ID. 

Now, if a *strongly* paired device is compromised, the end user has the options:
- Simply reset the device/node and start again, pairing as a new node after reset.
    - Recommended approach.
    - The data survives thanks to your other paired devices. 
    - Only the local event stream is thrown away.
    - Roaming ID stays the same. 
- Recover the device from the attacker and pause application usage until then. 
- Unpair the device entirely (?) and let the strong pairing of *other* devices keep things together.  
    - Unpairing a device would prevent an attacker from using it to pair more malicious devices. 
        - But would remove all event stream entries previously contributed by that device. 
        - Likely to break application due to missing user data. 
    - All other devices would need to be strongly paired to each other, none weakly paired through the compromised device.
    - Each device strongly paired to the compromised device would need to individually remove the compromised device. 

Open questions:
- Would this need to be done on every existing device for every added device? 
- Is this really necessary? 
    - Is the weak/strong distinction necessary?
    - How does the actual algorithm work for tracing source add/removes starting at the origin node? 
        - Currently a hashset
            - Minimizes the ability to "fight" between nodes that:
                - create branches with adds
                - or prune branches with removes.
        - Needs to be different or more specific? 
- What about if the attacker publishes to the local event stream and the other devices pick it up for backup before the stream is retired/replaced? 
    - The compromised event stream can't be updated after it's retired/replaced.
    - Frozen in backup form, at last published value. 
    - Another non-malicious paired node will need to add a [`ReservedEventIds.NomadEventStreamSourceRemoveEvent`](../src/ReservedEventIds.cs) to its own event stream referencing the malicious node to unpair it. 


