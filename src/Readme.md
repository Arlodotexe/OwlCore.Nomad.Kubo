# Container ownership and operation

## Container types

To illustrate with examples, the oc.storage abstraction has:

- IFolder
  - Get

- IModifiableFolder
  - Get
  - Create
  - Delete

Modifiability is given via _create_ and _delete_ operations. However, for the majority of item containers in other use cases, we see lists instead:
- **Peer**: List of Addresses
- **PeerSwarm**: List of Peers
- **PeerSwarmTracker**: List of PeerSwarms
- **User**: List of projects, publishers
- **Publisher**: List of projects, publishers, users, etc.
- etc.

"Create" and "Delete" aren't used. Instead, these use:
- Get
- Add
- Remove

---

These are two types of containers with two types of ownership and operations, Add/Remove and Create/Delete.

This makes a useful abstraction for dynamically scoping ownership and behavior of containers and child items, which we expand on more here. 

---

### Get/Add/Remove

- Registry
- Tracker-like, a simple list of references
- Container doesn't need to own the contained
- Operation
  - Get: Retrieves objects, modifiable or read-only.
  - Add: Existing instance is added to container.
  - Remove: Existing instance is removed from container
- Can hold read-only or modifiable objects
- Modifiable registry derives read-only registry, adds an add/remove method.

### Get/Create/Delete

- Repository
- Manages or 'owns' contained object lifecycle.
- Operations:
  - Get: Retrieve objects
  - Create: Instantiate, init and add
  - Delete: Remove and destroy
- Get, create and delete always return modifiable objects if the repo is not also a modifiable registry.
- Derives a read-only registry.

### All together now

Implementation derives registry and repository
- Add/Remove with Nomad
- Create/delete with repo
- Get with repo

There are three interfaces at play here:
- Registry (r/o)
    - Get (readonly)
- Registry (m)
    - Derives Registry (r/o)
    - Get (modifiable, readonly)
    - Add
    - Remove
    - Events
- Repository (m)
    - Derives Registry (r/o)
    - Get (modifiable, readonly)
    - Events
    - Create
    - Delete

See code for interface dependency graph.

## Repository usage in a registry

#### Roles
- Repo is used to manage 'owned' items
- Registry is used to track items owned by anyone.

#### Scenarios
- For a discord bot, one node is shared by many people.
  - Shared bot command: Get repo of user/project/etc scoped to requesting discord id. <--- any
- In a normal nomad setup, one user would have multiple nodes, each owned exclusively by that one user.
  - Cli command: Get repo scoped by static ID <---- self

## Examples

- Read-only item metadata can used to decide whether to build a read-only or modifiable container or item in a container.
  - Read-only if the request isn't the owner
  - Modifiable if the request matches the owner

- The requester must match item or container owner to be modifiable
- Requester repo _is_ passed to child instances.


## Notes

| Owned | Resolvable | Resolved | In-Mem | Notes |
| ---  |------------|----------|--------| --- |
| Yes | No | No | Yes | If not resolvable (only created, not published), should be in-memory  |
| Yes or No | Yes | No | No | Resolve when not in-mem. Might be owned by someone else or previously published by self. |
| Yes or No | Yes | Yes | No | Once resolved, keep in-mem. |
| Yes or No | Yes | Yes | Yes |

```
[Created] (self)  --b>    [Resolved]   <c-- [Created] (others)
  -a Not resolvable         |                -c Not published by self, resolvable
        on first use.       | 
  -b Can be resolved        | 
    after first publish.    | 
                   \        | 
                    \       | 
                     \      | 
                      \     | 
                       a    |
                        v   v
                         [In-Memory]
                           - Should be used whenever possible
```

## Open questions

- Where is "in-memory", maybe the first callsite where Roaming Key or Data is expected to not be null?
  - Data should not be null when key is null
    - Key should be null when the node is unpaired, which means to create a read-only instance.
    - Data must be supplied to create read-only instance, must be pre-populated.
  - Key should not be null when data is null.
     - When data is null, it implies that either it hasn't been published or isn't needed yet, both of which are only possible for a modifiable instance.
     - Initial data isn't always required since we can start from a seed state and advance the event stream handler.
     - Initial data may be desired anyway when:
       - Advancing from a checkpoint, especially the last known roaming state.
       - Using all published sources (including the original source node) instead of just the sources paired to you.
  - In summary, an "in-memory" state is required at the callsight where Modifiable vs ReadOnly is decided and constructed.

- How does roaming data get into memory under all scenarios?
  - Resolved from a previous publish
  - Created and not yet published

- When should a repo be retrieved via a delegate, by id?
  - When the repo instance depends on a dynamic context.
  - When yielding objects not owned by the repo.
- When should a repo be provided as an instance?
  - When it doesn't change between container and contained instances.