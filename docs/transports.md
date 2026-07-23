## [**Transports**](./transports.md):

### [**Overview:**](./transports/overview.md)

#### **Internet vs Intranet vs Sneakernet**:
- Internet
    - WAN, ISP-routed.
    - Reliability, access speed/frequency, and geography.
    - Connection locality and lifetime: Live global data transfer.
- Intranet
    - LAN, Bluetooth, WiFi Direct, LoRA.
    - Reliability, access speed/frequency, and environment.
    - Connection locality and lifetime: Live local-nearby data transfer, limited global connection.
- Sneakernet
    - USB, physical media.
    - Reliability, access speed/frequency, and material.
    - Connection locality and lifetime: No live connection, direct transfer only.

#### **Sneakernet vs exported backup**:
- TODO
- TBD

### [**Usage**](./transports/usage.md)

#### Internet
- WAN built into Kubo, including p2p NAT traversal

#### Intranet
- LAN built into Kubo (tcp, udp, quic, websockets, etc.)
- Bluetooth, WiFi Direct, LoRA not built into Kubo

#### Sneakernet
- TODO

