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
    - Based on data in instances of [`NomadKuboEventStreamHandlerConfig.cs`](../../src/NomadKuboEventStreamHandlerConfig.cs) in [`NomadKuboRepositoryBase`](../../src/NomadKuboRepositoryBase.cs).`ManagedConfigs` and `ManagedKeys`. 
- This allows `ManagedConfigs` and `ManagedKeys` to be used to scope repositories and subsequently all event stream handlers they create/retrieve to different individual operators on the same machine
    - Such as in server-hosted scenarios (e.g. Discord bot where p2p is optional). 
- The instantiated event stream handler knows nothing about the `NomadKuboEventStreamHandlerConfig` that was used to configure it. 
- `ManagedKeys` and `ManagedConfigs` properties and their `GetAsync` methods exist only on `NomadKuboRepositoryBase`, and are expected by the consuming application to:
    - Read from them after create/update to persist them.
        - Optionally scoped to some ID at a higher level
        - See also [`StorageHostedRepository.cs`](https://github.com/WindowsAppCommunity/WindowsAppCommunity.Discord.ServerCompanion/blob/main/src/StorageHostedRepository.cs)/[`WacNomadRepoGroupRepository.cs`](https://github.com/WindowsAppCommunity/WindowsAppCommunity.Discord.ServerCompanion/blob/main/src/WacNomadRepoGroupRepository.cs). 
    - Provide to them to restore after runtime save/reset. 
    - Use them via the repository methods to construct instances of the data they represent.


### "Forking" another operator's event stream to diff a patch
- Locally modifiable instead of readonly instance, with writes contained to custom/temp event stream
- Local event stream entries added can be submitted through a sidecar channel (e.g. Patches library) to original operator

