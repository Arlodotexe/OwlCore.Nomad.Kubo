## [**Backups**:](./backups.md)

### [Data in a backup:](./backups/data.md)
    - [`ManagedConfigs`](./backups/data/ManagedConfigs.md)
    - [Published IPNS records](./backups/data/ipns/public-records.md)
    - [Private IPNS keys](./backups/data/ipns/private-keys.md)
    - [Block sets (CARs)](./backups/data/CARs.md)
- For each "Data in a backup", we ask: 
    - Where data lives
    - Who "owns" a backup
        - Primarily decided by who controls the ipns keys
        - May be an individual or an organization, unspecified.
    - Who does backup data represent
        - Specified during the export process
        - Could be:
            - Your own data
            - Data of another operator (linked or given)
            - Some or all adjacent resolvable ipns links in any given graph up to some depth. 
    - What can be done with backup data

### [Importing and Exporting](./backups/importing,export.md)

