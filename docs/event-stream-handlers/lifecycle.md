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
- Scoping repositories to individual operators

### "Forking" another operator's event stream to diff a patch
- Locally modifiable instead of readonly instance, with writes contained to custom/temp event stream
- Local event stream entries added can be submitted through a sidecar channel (e.g. Patches library) to original operator

