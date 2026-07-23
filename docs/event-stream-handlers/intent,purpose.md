## Intent and purpose

### New scenarios covered:
- Publish the same data to the same public-facing ipns address *from multiple devices* without a persistent live connection between devices.
- Operator can make changes to the single public-facing ipns address from any owned device/node and they'll see it across all their connected devices while other operators see the "roaming" data published by all devices.
- Keep public-facing "roaming" IPNS keys alive using all online and paired devices. 

### Glossary references:
- "Operator" as a user analogy
- "Local state" as event streams used to create the roaming state
- "Roaming state" as data published for consumption by an application or another user.
- "IPNS" as the immutability layer over ipfs

