using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ClientManager : NetworkBehaviour
{
    public GameObject playerPrefab;

    public static ClientManager Instance;

    Dictionary<ulong, NetworkObject> clients;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        clients = new Dictionary<ulong, NetworkObject>();
    }

    [Rpc(SendTo.Server)]
    public void SpawnClientRpc(ulong clientId, Vector3 pos)
    {
        GameObject player = Instantiate(playerPrefab, pos, Quaternion.identity);
        NetworkObject no = player.GetComponent<NetworkObject>();
        
        no.SpawnAsPlayerObject(clientId, true);
    }
    
    public static NetworkObject GetSelf()
    {
        return NetworkManager.Singleton.LocalClient.PlayerObject;
    }

    // Expects id to be from a client containing object
    public static NetworkObject GetClientByNetworkId(ulong networkObjectId)
    {
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects[networkObjectId];
    }

    [Rpc(SendTo.Server)]
    public void SendMessageRpc(string message)
    {
        print(message);
    }
}
