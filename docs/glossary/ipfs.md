# Concept glossary: ipfs

## Table of Contents
- [Protocol Labs](#protocol-labs)
- [The IPFS Shipyard](#the-ipfs-shipyard)
- [MultiFormats](#multiformats)
- [Content addressing](#content-addressing)
- [IPLD](#ipld)
	- [Data Model, Codecs](#data-model-codecs)
	- [Linking](#linking)
	- [CAR exporting](#car-exporting)
- [IPFS](#ipfs)
- [Kubo](#kubo)
- [IPNS](#ipns)

These are technologies created by or heavily used by Protocol Labs or the IPFS Shipyard.

## Protocol Labs

tl;dr;: **Protocol Labs created IPFS**.

From [the website](https://www.protocol.ai/):

> Protocol Labs is an innovation network that connects over 600 tech startups, service providers, investment funds, accelerators, foundations, and other organizations developing breakthrough technologies and products in the frontiers of computing: web3, AI, AR, VR, BCI, hardware, and more.
> 
> Organizations collaborate across the network to solve common problems, share knowledge and resources, and accelerate the R&D process for a wide range of technological fields.

From [Wikipedia](https://en.wikipedia.org/wiki/Protocol_Labs):

> Protocol Labs is an open-source software research and development company, founded in 2014. It is best known for creating the InterPlanetary File System (IPFS), a peer-to-peer decentralized web protocol, and Filecoin, a decentralized file storage network.

## The IPFS Shipyard

You can find a full list of [what they do](https://ipshipyard.com/what-we-do) and their [initiatives](https://ipshipyard.com/initiatives) on their website.

From [their website](https://ipshipyard.com/):

> Interplanetary Shipyard is an engineering collective that delivers user agency through technical advancements in IPFS and libp2p.

> As the core maintainers of open source projects in the Interplanetary Stack (including IPFS, libp2p, Kubo, Rainbow, Boxo,  and Helia) and supporting performance measurement tooling (Probelab), we are committed to open source innovation and building bridges between traditional web frameworks and the decentralized ecosystem.

Notably:

> As of January 2024, many of the core maintainers of IPFS and libp2p have begun working as a separate team in the Protocol Labs network. In this next chapter as an independent entity, we are excited to embark upon advancements to the Interplanetary Stack in service of web2 and web3 development teams — and we need your help!

## MultiFormats

From [their website](ipfs://bafybeif4fyqju4oz7dsvv2b3tkb5nzuitbmp55kbeympjntcgnjqnrmu7q):

> The Multiformats Project is a collection of protocols which aim to future-proof systems, today. They do this mainly by enhancing format values with self-description.

This includes:
- [multiaddr](ipfs://bafybeif4fyqju4oz7dsvv2b3tkb5nzuitbmp55kbeympjntcgnjqnrmu7q/multiaddr) (WIP) - self-describing network addresses
- [multibase](https://github.com/multiformats/multibase) (WIP) - self-describing base encodings
- [multicodec](https://github.com/multiformats/multicodec) - self-describing serialization
- [multihash](ipfs://bafybeif4fyqju4oz7dsvv2b3tkb5nzuitbmp55kbeympjntcgnjqnrmu7q/multihash) - self-describing hashes

These technologies are used extensively in libp2p and ipfs tooling.

Reference links
```
https://multiformats.io
ipns://multiformats.io
ipfs://bafybeif4fyqju4oz7dsvv2b3tkb5nzuitbmp55kbeympjntcgnjqnrmu7q
```

## Content addressing

When you want to recommend a book to someone, you don't tell them the exact city, library, floor, bookshelf and position where the book is. Rather, you tell the name, or you send a unique identifer like the ISBN.

The core idea of content addressing is effectively this, but with data on the internet. Rather than telling me *where* the data is (e.g. an IP address), you tell me *what* the data is so I can find it nearby.

This concept is critical to peer-to-peer data transfer. From the ipfs concept docs:

> A content identifier, or CID, is a label used to point to material in IPFS. It doesn't indicate where the content is stored, but it forms a kind of address based on the content itself. 

The CID (content identifier) used to locate the data is based on the data itself. This means any data can be self-verified as blobs are receieved regardless of the source, enabling "receive from anyone, host for anyone" setups.

Content addressing also makes it possible to transmit content in a "sidecar" fashion as well, not just over traditional LAN and WAN. Any transmit medium can work with content addressing, including bluetooth and [sneakernet](https://en.wikipedia.org/wiki/Sneakernet). 

Reference links
```
https://en.wikipedia.org/wiki/Content-addressable_storage
https://docs.ipfs.tech/concepts/content-addressing/
ipfs://bafybeicv5tbyeahgm4pyskd2nverwsqxpiqloikqrufvof7vausglw6avm/concepts/content-addressing/
```

## IPLD

In a nutshell, IPLD is a **unified data model for content addressing**.

It allows us to treat all hash-linked data structures as subsets of a unified information space, unifying **all data models that link data** with hashes as instances of IPLD.

### Data Model, Codecs

IPLD is a system for understanding and working with data.

It's made up of a Data Model and Codecs, some tools for Linking, and then a handful of other Powerful Features which make developing decentralized applications a breeze.

Broadly, we can say the Data Model is "like JSON", and you've probably got the right idea -- maps, strings, lists, etc.

Through codecs, we're able to seamlessly serialize between raw IPLD and JSON, Protobuf, or CBOR. 

### Linking

A key part of IPLD is its ability to link from one DAG to another through content addressing.  

IPLD linking usees CIDs for this. In practice, this might looks like:
```json
{
    "name": "myproject",
    "users": [
        {"/": "bafy...."}
        {"/": "bafy...."}
        {"/": "bafy...."}
    ]
}
```

Where each `bafy...` resolves to a `user`:
```json
{
    "name": "Alice",
    "projects": [
        {"/": "bafy...."}
        {"/": "bafy...."}
        {"/": "bafy...."}
    ]
}
```

Note that the user has linked to several projects here, which themselves may link to other users. 

With content addressing, we're able to sparsely walk and resolve this object as needed, and since we're using immutable CIDs we can retrieve that data from anyone. Trust and mutability is added using a sperate layer-- IPNS.

### CAR exporting

One big benefit of using IPLD is CAR exporting.

Even in places where libp2p and Kubo aren't used, ipfs technologies like IPLD and content addressing still are. For example, BlueSky allows users to [export their data as CAR](https://docs.bsky.app/blog/repo-export#parse-records-from-car-file-as-json).

IPLD's content addressing and standardized linking allows for a partial or full graph crawl. It means you can export a project DAG and any linked user data can be automatically exported as well.

Reference links
```
ipfs://bafybeigz7xdtojjcpufxkibctnslx5x3yhqffye5fc3hs4ftmk4p37drei/docs/intro/hello-world/
```

## IPFS

IPFS stands for the "InterPlanetary File System", and is self-described as "an open system to manage data without a central server".

The [original whitepaper](https://github.com/ipfs/papers/raw/master/ipfs-cap2pfs/ipfs-p2p-file-system.pdf) ([permalink](ipfs://QmR8XDVsnRj92gp11PeAYWiFxyKX29zZXzweRdp8et1Lba)) for ipfs was released in 2014, was spearheaded by Juan Benet and Protocol Labs for about 10 years, then the project was passed to the community itself in the form of the IPFS Shipyard. 

Notably, much like HTTP, there many implementations of the IPFS spec, such as:
- [Boxo](https://github.com/ipfs/boxo) and [Kubo](https://github.com/ipfs/kubo/), the first and most widely used implementation built in Go. Uses the [Amino DHT](ipfs://bafybeidvtgmzp4eyllecgvfmg7eifnvlbc2h2m7g24pomo2wzoipmxdhhy/2023-09-amino-refactoring/) and libp2p.
- [Helia](ipfs://bafybeiez5cq7dooj4v35snsfgdgptj6ct6ikmtzknq224b7myii7jbefl4/), a JavaScript implementation. Uses the [Amino DHT](ipfs://bafybeidvtgmzp4eyllecgvfmg7eifnvlbc2h2m7g24pomo2wzoipmxdhhy/2023-09-amino-refactoring/) and libp2p.
- [Iroh](https://www.iroh.computer/docs/ipfs), a lean ipfs system for transferring data. Built in Rust, uses its own networking stack.
- [AT Protocol](https://atproto.com/guides/data-repos), a decentralized protocol for large-scale social web applications. Uses federated HTTP instead of p2p.

An "ipfs implementation" is any system that implements content addressing using multiformats. Not all implementations are built the same-- they're often specialized for a purpose.

Reference links
```
https://ipfs.io
ipns://ipfs.io
ipfs://bafybeig2htkx6trji2aast7x6bdymzdgm4gc4ouvp25n7fufr55nitci3y
```

## Kubo

From their [GitHub](https://github.com/ipfs/kubo/?tab=readme-ov-file#what-is-kubo):

> Kubo was the first IPFS implementation and is the most widely used one today. 
> 
> Implementing the Interplanetary Filesystem - the Web3 standard for content-addressing, interoperable with HTTP.
> 
> Thus powered by IPLD's data models and the libp2p for network communication. Kubo is written in Go.

Kubo is "full kit" p2p tooling. It contains most everything you'll need to create a distributed peer-to-peer application using ipfs and content addressing with minimal effort.


Since it's backed by the full force of the hundreds of projects and contributors in libp2p and the interplanetary shipyard, we rely on Kubo for our purposes and we build to extend it.

## IPNS

By nature, CIDs cannot be changed-- If the content changes, so does the identifier. CIDs are immutable, based on the content itself. 

IPNS adds a **mutability layer** to IPFS, allowing you to publish verifiable CID updates. This works by combining content addressing with basic asymmetric encryption.

When you generate an IPNS key, you create a public key and a private key.
- The public key becomes your public IPNS identifier-- it's encoded into a CID and is made resolvable via the Amino DHT.
- The private key is used to sign and broadcast a value for the IPNS public key.

The value broadcast to the DHT includes a private key signature alongside the public key itself, the value published, and some other metadata. 

Using the public key in conjunction with the private key signature, we can cryptographically verify that the published value was signed by the private key.  

This forms the basic of IPNS-- asymmetric encryption and content addressing.

Additionally, Kubo supplies experimental APIs for [signing](ipfs://bafybeihqzueokgapc75zpwoxl24byt6wiqi67x5opaefz7hxppbgqmcple/reference/kubo/rpc/#api-v0-key-sign) and [verifying](ipfs://bafybeihqzueokgapc75zpwoxl24byt6wiqi67x5opaefz7hxppbgqmcple/reference/kubo/rpc/#api-v0-key-verify) arbitrary bytes using existing IPNS keys. These aren't necessary for normal IPNS usage, but can be extraordinarily useful for more advanced setups. 

Reference links
```
ipfs://bafybeihqzueokgapc75zpwoxl24byt6wiqi67x5opaefz7hxppbgqmcple/concepts/ipns/
```

