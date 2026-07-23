## How Event Stream Handlers work

### Background

See "New Scenarios Covered" in [intent,purpose.md](./intent,purpose.md) if you haven't. 

### Problems

- Eventual consistency for you and others even as devices go offline and online. 
- Publish the same data to the same public-facing ipns address *from multiple devices* without a persistent live connection between devices.
- Keep public-facing "roaming" IPNS keys alive using all online and paired devices. 

### Solutions

**For you as a multi-node operator:**
- Changes stay in dedicated ipns event streams per device/node they happen on. 
- Operator nodes observe each others' event streams for entries.
- Each operator node individually but fully advances the event streams into the handler locally to get the latest (known) object state, composed from its local view of the event streams for the other operator nodes.
    - This final state is published by each operator node to ipns as the "roaming" data.
    - Same set of input event stream entries = same output roaming data
    - Conflict handling is delegated to an implementation detail. 
        - Use CDRTs or handroll your own solution fit to your needs, just be sure to cover all scenarios. 

**For all other node operators:**
- Don't need to resolve your event streams or replay the object to get the latest state. 
- Can instead resolve the ipns of the published final "roaming data" object state.
    - All paired operator devices publish to the roaming state based on observed event stream entries.
    - If all operator devices were in agreement on the event stream entries and have published their resulting roaming state,
        - then that one published ipns address will be the same until further changes are made
        - no matter how disjoint the network
        - as long as there's at least one operator device that can be reached. 

This is the problem space and constraint set that Event Stream Handlers aim to cover as a solution. 

In a nutshell, event stream handlers are:
- **Input:** One or more local event stream sources published per application per device on IPNS.
- **Output:** A single eventually consistent "roaming data" object published across all operator devices on IPNS.
- **In-between:** Application functionality over data, "remoting with an abstraction" via multi-device event stream replay to reach a consistent roaming state. 
- **Contained by:** The `IRepository` pattern for Get/Create/Delete and permission scoping of event stream handler instances. See [lifecycle.md](./lifecycle.md).
