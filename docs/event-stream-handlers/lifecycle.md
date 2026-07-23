## Lifecycle management of Event Stream Handlers via `IRepository`

### Intent and purpose
- New scenarios covered:
    - Streamline the instantiation, creation and deletion of event stream handlers.
    - Support for single-device many-operator (server-hosted data) scope isolation.
- Glossary references:
    - "Operator" as a user analogy
    - "IRepository" as factory for modifiable vs readonly event stream handlers

### Create, delete, get
- Automatic ReadOnly vs Modifiable instances

### Define known construct-able instances with `ManagedConfigs`
- Event stream handlers are constructed:
    - By the methods in:
        - [`NomadKuboRepository`](../../src/NomadKuboRepository.cs)`.CreateAsync` for brand-new modifiable-only local/roaming keys and instances, not yet paired to any other device.
        - [`NomadKuboRepositoryBase`](../../src/NomadKuboRepositoryBase.cs).`GetAsync` overloads for existing readonly (owned by other operator, no keys) or modifiable (owned by current operator, has keys) instances.
    - Based on data in instances of [`NomadKuboEventStreamHandlerConfig.cs`](../../src/NomadKuboEventStreamHandlerConfig.cs) in [`NomadKuboRepositoryBase`](../../src/NomadKuboRepositoryBase.cs).`ManagedKeys`. 
- Because of this, `ManagedKeys` can be used to scope repositories and all event stream handlers they create/retrieve to different individual operators on the same machine, such as in server-hosted scenarios (e.g. Discord bot where p2p is optional). 
- The created event stream handler knows nothing about the handler config that was used to configure its instantiation. 


### "Forking" another operator's event stream to diff a patch
- Locally modifiable instead of readonly instance, with writes contained to custom/temp event stream
- Local event stream entries added can be submitted through a sidecar channel (e.g. Patches library) to original operator

