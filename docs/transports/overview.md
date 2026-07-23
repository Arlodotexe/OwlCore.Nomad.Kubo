## **Transports Overview:**

### **Internet vs Intranet vs Sneakernet**:

Thanks to the content-addressed nature of IPFS, Kubo supports WAN, LAN and Sneakernet out of the box and could be extended to support other transport mechanisms like Bluetooth, WiFi direct. 

Content-addressed data (IPFS blocks, IPLD protobufs, unixfs folders, and DHT entries like signed IPNS records) all intrinsically supports these transports, even if not yet supported by Kubo: 
- Internet
    - Directly connecting two non-local machines with Kubo.
    - WAN, ISP-routed, Town, City or Country scale networks or meshes
    - Reliability, access speed/frequency, and geography.
    - Connection locality and lifetime: Live global data transfer.
- Intranet
    - Directly connecting two running local machines with Kubo.
    - LAN, Bluetooth, WiFi Direct, LoRA, home or person scale networks or meshes. 
    - Reliability, access speed/frequency, and environment.
    - Connection locality and lifetime: Live local-nearby data transfer, limited global connection.
- Sneakernet
    - Indirectly connecting two machines via portable media (USB, disk) to move content-addressed data between them via either a third Kubo repo or via sets of CAR export(s)/import(s).
    - USB, physical media.
    - Reliability, access speed/frequency, and material.
    - Connection locality and lifetime: No live connection, direct transfer only.

### **Sneakernet vs exported backup**:
- TODO
- TBD

