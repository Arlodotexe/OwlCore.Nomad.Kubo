# OwlCore.Nomad.Kubo [![Version](https://img.shields.io/nuget/v/OwlCore.Nomad.Kubo.svg)](https://www.nuget.org/packages/OwlCore.Nomad.Kubo)

Build a distributed & modifiable application domain on ipfs via Kubo with eventual consistency.

## What is Nomad?

Put simply, this library was built to help you easily cover the gap between "User device" and "User".

This library was specially crafted to take advantage of content addressing under a changing device topology, especially considering the one-to-many nature of IPNS, the immutability of IPFS CIDs, and the need for a "shared" p2p-native state that reaches an eventually consistency defined by the application.

See also:
- [Nomad Concept Glossary](docs/glossary/nomad.md)
- [IPFS Concept Glossary](docs/glossary/ipfs.md)
- [Problems Faced](#problems-faced)
- [Solutions](#solutions)

## Featuring:
- Interfaces and models for building Nomad applications using Ipfs/Kubo.
- A base implementation `NomadKuboEventStreamHandler` that handles advancing the handler's event stream using resolved data from ipfs.
- Models and tooling for creating and updating local and roaming data.
- Tooling for creating readonly vs modifiable instances based on key availability.
- Tooling for pairing new devices and adding them as event stream sources.
- ...and more!

## Install

Published releases are available on [NuGet](https://www.nuget.org/packages/OwlCore.Nomad.Kubo). To install, run the following command in the [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console).

    PM> Install-Package OwlCore.Nomad.Kubo
    
Or using [dotnet](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet)

    > dotnet add package OwlCore.Nomad.Kubo

## Implementations

Several prototype implementations were created to test several scenarios we expected to serve.

The publicly available implementations are:

- A generic [base](https://github.com/Arlodotexe/OwlCore.Nomad) toolset for interacting with event stream handlers and entries.
- A toolset for using [Nomad with Kubo](https://github.com/Arlodotexe/OwlCore.Nomad.Kubo).
- A [base](https://github.com/Arlodotexe/OwlCore.Nomad.Storage) and [Kubo](https://github.com/Arlodotexe/OwlCore.Nomad.Storage.Kubo) implementation of [OwlCore.Storage](https://github.com/Arlodotexe/OwlCore.Storage).
- A [PeerSwarm](https://github.com/Arlodotexe/OwlCore.Nomad.Kubo.PeerSwarm/) manager for Kubo, useful for piggybacking off of public peer routing for private content routing.
- The [WindowsAppCommunity.Sdk](https://github.com/WindowsAppCommunity/WindowsAppCommunity.Sdk), roaming data for projects, publishers and users.

## Problems Faced

### Eventual Consistency

Given all the tools available in Kubo, we are unable to arrive at eventual consistency when publishing, reading and roaming data across devices.  

This happens because of the way the DHT works. It's designed to have one node publishing a value that propogates to other nodes, a bit like DNS. It's **not** designed to pull values from another device publishing to the same address, even if that value is newer. 

![](./docs/ipns-overview.png)

References
```
http://bafybeifugnn4vw5vmf4ehk5cqsqqeky4jujzhwy4vdutrx6bptgszswzey.ipfs.dweb.link/ipns/ipns-record/#overview
ipns://specs.ipfs.tech/ipfs/ipns-record/#overview
ipfs://bafybeifugnn4vw5vmf4ehk5cqsqqeky4jujzhwy4vdutrx6bptgszswzey/ipns/ipns-record/#overview
```

Above all else, the network can be partitioned, which means a "shared" state cannot achieve eventual consistency without replaying history to resolve conflicts.

### Many devices, one user

It's a given that:
- A user should be expected to have multiple devices they make changes from
- A user should have a single identifier that represents their changes across all devices for others to resolve and read (not write).

Due to partitioning and eventual consistency concerns, we can't roam data by publishing a single IPNS address from multiple devices.

This complicates the "Many devices, one user" scenario significantly. It's a hard requirement for any reasonable application to have a single (eventually consistent) resolvable id that represents the user. 

It's an impractical and unrealistic requirement to treat a list of device-specific IDs *as* the user id itself. This list may change, and it overcomplicates sharing and linking scenarios. 

## Solutions

### Eventual Consistency

To arrive at eventual consistency across devices, *including* after network partitions and reconnects, we need to leverage the topology and processes given by content addressing and IPNS.

Instead of reading from and modifying a "shared" state, each peer node maintains their own modifiable **event stream** while reading the read-only event streams published by other peers.

A "roaming" state is computed from a seed value by replaying and aggregating individual event streams published by individual devices, each containing an append-only history of interactions performed against an application domain on that device.

As peers come and go, publishing  peer-specific interactions with the application domain allows the consolidated state to eventually converge on each device without CDRTs or generalizable conflict resolution logic.

Conflict resolution is effectively delegated to an implementation detail.

### Many devices, one user

Our fix for eventual consistency also comes with the fix for publishing a single resolvable identifer.

The "consolidated state" that is arrived at with eventual consistency is called the **roaming state**, and acts as the resolvable "per-operator" IPNS that represents changes made from all paired devices.

This works across network partitions as well. If changes are made on two fully isolated partitions:
- Any roaming data published to IPNS represents the "best known value" for that particular partition.  
- The implementation knows how to handle any conflict resolution that might occur once partitions are joined.
- Since publishing a roaming state requires computing it from event stream sources, the published roaming state is always (locally for that partition) conflict-resolved. 

## Financing

We accept donations [here](https://github.com/sponsors/Arlodotexe) and [here](https://www.patreon.com/arlodotexe), and we do not have any active bug bounties.

## Versioning

Version numbering follows the Semantic versioning approach. However, if the major version is `0`, the code is considered alpha and breaking changes may occur as a minor update.

## License

All OwlCore code is licensed under the MIT License. OwlCore is licensed under the MIT License. See the [LICENSE](./src/LICENSE.txt) file for more details.
