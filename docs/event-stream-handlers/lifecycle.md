## Lifecycle management of Event Stream Handlers via `IRepository`

### Intent and purpose
- New scenarios covered:
    - Streamline the instantiation, creation and deletion of event stream handlers.
    - Support for single-device many-operator (server-hosted data) scope isolation.
- Glossary references:
    - "[Operator](../glossary/nomad.md#operator)" as a user analogy
    - "[IRepository](../glossary/nomad.md#repository)" as a factory for modifiable vs readonly event stream handlers

### Create, Get, and Delete Event Stream Handler instances
- By definition, a repository handles ownership of the lifecycle of the items it contains for creation, retrieval (async and live), and deletion.
- The [`NomadKuboRepository`](../../src/NomadKuboRepository.cs) base class must be derived and implemented by a particular application to be used. 
- Event stream handlers are constructed by the methods in:
    - [`NomadKuboRepository`](../../src/NomadKuboRepository.cs)`.CreateAsync` creates new [local](../glossary/nomad.md#local-state) and [roaming](../glossary/nomad.md#roaming-state) [keys](../glossary/ipfs.md#ipns) if needed and returns a modifiable-only instance, not yet paired to any other device.
    - [`NomadKuboRepositoryBase`](../../src/NomadKuboRepositoryBase.cs).`GetAsync` overloads, both the id/instance lookups and the async enumerator, will yield or return:
        - Existing readonly (owned by other operator, no keys) instances,
        - or Existing modifiable (owned by current operator, has keys) instances.
        - Depending on data in instances of [`NomadKuboEventStreamHandlerConfig`](../../src/NomadKuboEventStreamHandlerConfig.cs) in [`NomadKuboRepositoryBase`](../../src/NomadKuboRepositoryBase.cs).`ManagedConfigs` and `ManagedKeys`. 
- End-to-end for [`NomadKuboRepository`](../../src/NomadKuboRepository.cs) implementations, repositories automatically return/yield ReadOnly or Modifiable instances based on whether the repository is provided the necessary keys and config to make changes or not. 

### Persisting and restoring repository state

#### **Data handling before and after use**:

The instantiated event stream handler never knows about the `NomadKuboEventStreamHandlerConfig` that was used to configure it.

The `ManagedKeys` and `ManagedConfigs` properties exist only on `NomadKuboRepositoryBase`, and are updated automatically as the repository creates and deletes instances. It's expected that the consuming application:
- Read their values after create/update repository operations to persist them for the next run.
    - Optionally scoped to some ID at a higher level
    - See also [`StorageHostedRepository.cs`](https://github.com/WindowsAppCommunity/WindowsAppCommunity.Discord.ServerCompanion/blob/main/src/StorageHostedRepository.cs)/[`WacNomadRepoGroupRepository.cs`](https://github.com/WindowsAppCommunity/WindowsAppCommunity.Discord.ServerCompanion/blob/main/src/WacNomadRepoGroupRepository.cs). 
- Load and provide persisted values to them to restore after runtime save/reset. 
- Use them via the repository methods to construct instances of the data they represent.

#### **Scoping to one or more operators**:

Since repositories own the objects contained in them for their lifetime from create to delete, the created `ManagedConfigs` and `ManagedKeys` that appear on a repository:
- Can be saved in a well-isolated scope (e.g. a dedicated folder)
- Then reused to scope new repository instances:
    - So that specific event stream handlers return/yield readonly or modifiable.
- to different individual operators all sharing the same node/machine,
    - depending on how you save/restore the data
    - Optionally scoped to some ID at a higher level.
    - See also [`StorageHostedRepository.cs`](https://github.com/WindowsAppCommunity/WindowsAppCommunity.Discord.ServerCompanion/blob/main/src/StorageHostedRepository.cs)/[`WacNomadRepoGroupRepository.cs`](https://github.com/WindowsAppCommunity/WindowsAppCommunity.Discord.ServerCompanion/blob/main/src/WacNomadRepoGroupRepository.cs). 
- Common for server-hosted scenarios where p2p is optional and interaction is gated by additional code (e.g. Discord bot). 


### "Forking" another operator's event stream to diff a patch
- Locally modifiable instead of readonly instance, with writes contained to custom/temp event stream
- Local event stream entries added can be submitted through a sidecar channel (e.g. Patches library) to original operator
- TODO
