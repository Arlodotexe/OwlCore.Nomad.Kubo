using System.Text;
using CommunityToolkit.Diagnostics;
using Ipfs;
using Ipfs.CoreApi;
using OwlCore.Diagnostics;
using OwlCore.Kubo;
using OwlCore.Kubo.Extensions;
using OwlCore.Storage;
using OwlCore.Storage.System.IO;

namespace OwlCore.Nomad.Kubo;

/// <summary>
/// A delegate that, given a roaming key, gets or creates the corresponding local key containing an event stream.
/// </summary>
/// <param name="roamingKey">The roaming key to use to create the local key.</param>
/// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
/// <returns></returns>
public delegate Task<IKey> GetOrCreateLocalKeyFromRoamingAsync(IKey roamingKey, CancellationToken cancellationToken);

/// <summary>
/// Helpers for exchanging roaming and local Nomad ipns keys between nodes. 
/// </summary>
/// <remarks>
/// Primarily used to exchange keys to 'pair' and enable a new node to co-publish to the same roaming key. 
/// </remarks>
public static class KeyExchange
{
    /// <summary>
    /// Initiates an end-to-end Nomad pairing. Should be called on two separate nodes, one with both a roaming and local key (the roaming sender / local receiver) and one with only a local key (the roaming receiver / local sender).  
    /// </summary>
    /// <param name="kubo">The bootstrapper to use when importing or exporting keys.</param>
    /// <param name="kuboOptions">Options for data published to ipfs.</param>
    /// <param name="client">A client that can be used for communicating with ipfs.</param>
    /// <param name="genericApi">A generic API that can supply peer information.</param>
    /// <param name="getOrCreateLocalKeyAsync">The existing local key. This should be an event stream on both nodes. The node that receives the roaming key will send this local key in response, which will be picked up and added to this local key's event stream as a new source.</param>
    /// <param name="isRoamingReceiver">If true, this node will act as a roaming receiver / local sender, otherwise this node will act as a roaming sender / local receiver.</param>
    /// <param name="roamingKeyName">The name of the roaming key on this machine. If <paramref name="isRoamingReceiver"/> is true, this is the key name that the received key will be imported under and should not exist on this node yet. If false, the key should exist on this node.</param>
    /// <param name="roomName">The name of the pubsub room to join for pairing.</param>
    /// <param name="password">The password to use for encrypting and decrypting pubsub messages.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task PairWithEncryptedPubSubAsync(KuboBootstrapper kubo, IKuboOptions kuboOptions, ICoreApi client, IGenericApi genericApi, GetOrCreateLocalKeyFromRoamingAsync getOrCreateLocalKeyAsync, bool isRoamingReceiver, string roamingKeyName, string roomName, string password, CancellationToken cancellationToken = default)
    {
        // Setup encrypted pubsub
        var thisPeer = await genericApi.IdAsync(cancel: cancellationToken);
        var encryptedPubSub = new AesPasswordEncryptedPubSub(client.PubSub, password, salt: roomName);
        using var peerRoom = new PeerRoom(thisPeer, encryptedPubSub, $"{roomName}")
        {
            HeartbeatEnabled = false,
        };
        
        // Local key must be initialized prior to pairing
        // Roaming key must exist on the 'roaming sender' node, must not exist on 'roaming receiver' node.
        // The node that receives a roaming key should be a sender for local key, and vice versa.
        await KeyExchange.ExchangeRoamingKeyAsync(peerRoom, roamingKeyName, isReceiver: isRoamingReceiver, kubo, kuboOptions, client, cancellationToken);

        var enumerable = await client.Key.ListAsync(cancellationToken);
        var keys = enumerable as IKey[] ?? enumerable.ToArray();

        var roamingKey = keys.FirstOrDefault(x => x.Name == roamingKeyName);
        if (roamingKey is null)
            throw new InvalidOperationException($"Roaming key {roamingKeyName} couldn't be found after import.");

        var localKey = await getOrCreateLocalKeyAsync(roamingKey, cancellationToken);

        // The node that sends a roaming key should be receiver for local key, and vice versa.
        var isLocalReceiver = !isRoamingReceiver;
        await KeyExchange.ExchangeLocalSourceAsync(peerRoom, localKey, roamingKeyName, isReceiver: isLocalReceiver, kuboOptions, client, cancellationToken);
    }
    
    /// <summary>
    /// Initiates a roaming key exchange using the provided peer room.
    /// </summary>
    /// <remarks>
    /// It's HIGHLY recommended to use an encryption pubsub layer for your peer room. 
    /// </remarks>
    /// <param name="peerRoom">The room to perform the exchange in.</param>
    /// <param name="roamingKeyName">The name to use for the imported roaming key.</param>
    /// <param name="isReceiver">When true, this method will act as the receiver. When false, this method will act as the sender.</param>
    /// <param name="kuboBootstrapper">The bootstrapper to use when importing or exporting the roaming key.</param>
    /// <param name="kuboOptions">Options for data published to ipfs.</param>
    /// <param name="client">The client to use for communicating with ipfs.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task ExchangeRoamingKeyAsync(PeerRoom peerRoom, string roamingKeyName, bool isReceiver, KuboBootstrapper kuboBootstrapper, IKuboOptions kuboOptions, ICoreApi client, CancellationToken cancellationToken)
    {
        peerRoom.HeartbeatEnabled = false;
        peerRoom.HeartbeatMessage = roamingKeyName;
        await peerRoom.PruneStalePeersAsync(cancellationToken);
        
        if (isReceiver)
        {
            // Receiver must be ready to receive before joining room.
            var receivedKeyTask = KeyExchange.ReceiveRoamingKeyAsync(peerRoom, kuboBootstrapper, roamingKeyName, client, kuboOptions, cancellationToken);
            
            // Enable room heartbeat to signal room join.
            _ = await peerRoom.WaitForJoinAsync(cancellationToken);
            peerRoom.HeartbeatEnabled = true;
            
            await receivedKeyTask;
        }
        else
        {
            // Enable room heartbeat to signal room join.
            peerRoom.HeartbeatEnabled = true;
            
            // Joiners should be ready to receive, wait for join and send.
            _ = await peerRoom.WaitForJoinAsync(cancellationToken);
            await KeyExchange.SendRoamingKeyAsync(peerRoom, kuboBootstrapper, roamingKeyName, cancellationToken);
        }
    }

    /// <summary>
    /// Initiates a local source exchange using the provided peer room.
    /// </summary>
    /// <remarks>
    /// It's HIGHLY recommended to use an encryption pubsub layer for your peer room. 
    /// </remarks>
    /// <param name="peerRoom">The room to perform the exchange in.</param>
    /// <param name="localKey">The local key for this node. If <paramref name="isReceiver"/> is false, this key will be sent, otherwise the received key will be added in a new <see cref="ReservedEventIds.NomadEventStreamSourceAddEvent"/> to the event stream at this key.</param>
    /// <param name="roamingKeyName">The name to use for the imported roaming key.</param>
    /// <param name="isReceiver">When true, this method will act as the receiver. When false, this method will act as the sender.</param>
    /// <param name="kuboOptions">Options for data published to ipfs.</param>
    /// <param name="client">The client to use for communicating with ipfs.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task ExchangeLocalSourceAsync(PeerRoom peerRoom, IKey localKey, string roamingKeyName, bool isReceiver, IKuboOptions kuboOptions, ICoreApi client, CancellationToken cancellationToken)
    {
        peerRoom.HeartbeatEnabled = false;
        peerRoom.HeartbeatMessage = localKey.Name;
        await peerRoom.PruneStalePeersAsync(cancellationToken);
        
        // Load roaming/local keys
        var enumerable = await client.Key.ListAsync(cancellationToken);
        var keys = enumerable as IKey[] ?? enumerable.ToArray();
        var roamingKey = keys.First(x => x.Name == roamingKeyName);
        
        if (isReceiver)
        {
            // Receiver must be ready to receive before joining room.
            var receivedKeyTask = KeyExchange.ReceiveLocalKeyAsync(peerRoom, roamingKey.Id, localKey.Id, localKey.Name, kuboOptions, client, cancellationToken);
            
            // Enable room heartbeat to signal room join.
            _ = await peerRoom.WaitForJoinAsync(cancellationToken);
            peerRoom.HeartbeatEnabled = true;
            
            await receivedKeyTask;
        }
        else
        {
            // Enable room heartbeat to signal room join.
            peerRoom.HeartbeatEnabled = true;
            
            _ = await peerRoom.WaitForJoinAsync(cancellationToken);
            await KeyExchange.SendLocalSourceAsync(peerRoom, localKey.Id, cancellationToken);
        }
    }
    
    /// <summary>
    /// Publishes the provided source key to the peer room.
    /// </summary>
    /// <remarks>
    /// Receiver should already be listening for the key to be sent.
    /// </remarks>
    /// <param name="room">The room to send the key to.</param>
    /// <param name="localSourceKeyId">The key cid to send.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task SendLocalSourceAsync(PeerRoom room, Cid localSourceKeyId, CancellationToken cancellationToken)
    {
        // Publish local key
        var bytes = Encoding.UTF8.GetBytes(localSourceKeyId);
        await room.PublishAsync(bytes, cancellationToken);
    }

    /// <summary>
    /// Listens for a local key to be sent to the room and updates the provided <paramref name="localKeyName"/> with the received source.
    /// </summary>
    /// <param name="room">The room to listen to for receiving the source.</param>
    /// <param name="roamingKeyId">The roaming key that this source is being added to.</param>
    /// <param name="localKeyId">A resolvable id for the local event stream to append.</param>
    /// <param name="localKeyName">The name of the local key to update.</param>
    /// <param name="kuboOptions">Options for data published to ipfs.</param>
    /// <param name="client">The client to use for communicating with ipfs.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task ReceiveLocalKeyAsync(PeerRoom room, Cid roamingKeyId, Cid localKeyId, string localKeyName, IKuboOptions kuboOptions, ICoreApi client, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<object?>();

        room.MessageReceived += OnMessageReceived;
        
#if NET5_0_OR_GREATER
        await taskCompletionSource.Task.WaitAsync(cancellationToken);
#elif NETSTANDARD
        await taskCompletionSource.Task;
#endif
        
        // Receive and import local key from other node
        async void OnMessageReceived(object? _, IPublishedMessage message)
        {
            room.MessageReceived -= OnMessageReceived;
        
            var messageStr = Encoding.UTF8.GetString(message.DataBytes);
            var newSourceCid = (Cid)messageStr;
            Logger.LogInformation($"Received event stream source {newSourceCid} from peer {message.Sender.Id}");
            
            // Open local event stream source
            Logger.LogInformation("Opening local event stream");
            var (localEventStream, _) = await client.ResolveDagCidAsync<EventStream<DagCid>>(localKeyId, nocache: !kuboOptions.UseCache, cancellationToken);
            Guard.IsNotNull(localEventStream);

            var newSourceDagCid = await client.Dag.PutAsync(newSourceCid, pin: kuboOptions.ShouldPin, cancel: cancellationToken);

            var eventEntry = new EventStreamEntry<DagCid>
            {
                TargetId = roamingKeyId,
                EventId = ReservedEventIds.NomadEventStreamSourceAddEvent,
                Content = (DagCid)newSourceDagCid,
                TimestampUtc = DateTime.UtcNow,
            };
            
            var eventEntryCid = await client.Dag.PutAsync(eventEntry, pin: kuboOptions.ShouldPin, cancel: cancellationToken);
            localEventStream.Entries.Add((DagCid)eventEntryCid);

            Logger.LogInformation($"Added new event {eventEntry.EventId} to local event stream, getting updating event stream CID.");
            var updatedLocalEventStreamCid = await client.Dag.PutAsync(localEventStream, pin: kuboOptions.ShouldPin, cancel: cancellationToken);
            
            Logger.LogInformation($"Publishing local event stream {localKeyName}");
            await client.Name.PublishAsync(updatedLocalEventStreamCid, key: localKeyName, lifetime: kuboOptions.IpnsLifetime, cancellationToken);
                
            // Finished
            taskCompletionSource.SetResult(null);
        }
    }

    /// <summary>
    /// Listens for a roaming key to be sent to the room and imports the key using the provided kubo bootstrapper.
    /// </summary>
    /// <param name="room">The room to listen to for receiving the source.</param>
    /// <param name="kuboBootstrapper">The bootstrapper to use when importing the received key.</param>
    /// <param name="roamingKeyName">The name to use for the imported roaming key.</param>
    /// <param name="client">The client to use for communicating with ipfs.</param>
    /// <param name="kuboOptions">Options for data published to ipfs.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task ReceiveRoamingKeyAsync(PeerRoom room, KuboBootstrapper kuboBootstrapper, string roamingKeyName, ICoreApi client, IKuboOptions kuboOptions, CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<object?>();

        room.MessageReceived += OnMessageReceived;
        
#if NET5_0_OR_GREATER
        await taskCompletionSource.Task.WaitAsync(cancellationToken);
#elif NETSTANDARD
        await taskCompletionSource.Task;
#endif
        
        // import roaming from other node
        async void OnMessageReceived(object? _, IPublishedMessage message)
        {
            room.MessageReceived -= OnMessageReceived;
            
            var outputFile = new SystemFile(Path.GetTempFileName());
            await outputFile.WriteBytesAsync(message.DataBytes, cancellationToken);

            Guard.IsNotNull(kuboBootstrapper.KuboBinaryFile);
            var (output, error) = ProcessHelpers.RunExecutable(kuboBootstrapper.KuboBinaryFile.Path, $"key import {roamingKeyName} {outputFile.Path} --repo-dir \"{kuboBootstrapper.RepoFolder.Path}\"", true);
            Guard.IsNullOrWhiteSpace(error);
            Logger.LogInformation($"Key {output} imported from peer {message.Sender.Id} as {roamingKeyName}, cleaning up");
                
            // Wipe content from temp key file
            var parent = (SystemFolder?)await outputFile.GetParentAsync(cancellationToken);
            Guard.IsNotNull(parent);
            await parent.DeleteAsync(outputFile, cancellationToken);
            Logger.LogInformation($"Cleaned up {outputFile.Id}");
            
            // Resolve and republish existing content from this node
            var resolvedIpfsPath = await client.Name.ResolveAsync(output, recursive: true, nocache: !kuboOptions.UseCache, cancellationToken);
            var resolvedCid = resolvedIpfsPath.Replace("/ipfs/", "");
            Logger.LogInformation("Resolved imported key");

            await client.Name.PublishAsync(resolvedCid, roamingKeyName, kuboOptions.IpnsLifetime, cancellationToken);
            Logger.LogInformation($"Republished imported key to {resolvedCid}");

            // Finished
            taskCompletionSource.SetResult(null);
        }
    }

    /// <summary>
    /// Exports and publishes a roaming key to a peer room.
    /// </summary>
    /// <remarks>
    /// Receiver should already be listening for the key to be sent.
    /// </remarks>
    /// <param name="room">The room to listen to for receiving the source.</param>
    /// <param name="kuboBootstrapper">The bootstrapper to use when importing the received key.</param>
    /// <param name="roamingKeyName">The name of the roaming key to export.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the ongoing operation.</param>
    public static async Task SendRoamingKeyAsync(PeerRoom room, KuboBootstrapper kuboBootstrapper, string roamingKeyName, CancellationToken cancellationToken)
    {
        var outputFile = new SystemFile(Path.GetTempFileName());
        Guard.IsNotNull(kuboBootstrapper.KuboBinaryFile);
        _ = ProcessHelpers.RunExecutable(kuboBootstrapper.KuboBinaryFile.Path, $"key export {roamingKeyName} --output={outputFile.Path} --repo-dir \"{kuboBootstrapper.RepoFolder.Path}\"", true);
        Logger.LogInformation($"Roaming key {roamingKeyName} exported to {outputFile.Path}, publishing to encrypted room");
            
        // Publish roaming key
        var bytes = await outputFile.ReadBytesAsync(cancellationToken);
        await room.PublishAsync(bytes, cancellationToken);
        Logger.LogInformation("Published to encrypted room, check receiving node");
                
        // Cleanup
        var parent = (SystemFolder?)await outputFile.GetParentAsync(cancellationToken);
        Guard.IsNotNull(parent);
        await parent.DeleteAsync(outputFile, cancellationToken);
        Logger.LogInformation($"Cleaned up {outputFile.Id}");
    }
}