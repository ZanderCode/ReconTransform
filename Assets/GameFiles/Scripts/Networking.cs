using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using Netcode.Transports.Facepunch;
using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using System.Net.Sockets;
using System;

public class Networking : MonoBehaviour
{
    public static Networking instance;
    public NetworkManager networkManager;

    private FacepunchTransport transport;

    public Lobby? currentLobby { get; private set; } = null;

    public ulong hostId;

    public GameObject playerPrefab;

    public ClientManager clientSpawner;

    Vector3 spawnPosition;


    private void Awake()
    { 
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        clientSpawner = GameObject.Find("ClientSpawner").GetComponent<ClientManager>();

        transport = GetComponent<FacepunchTransport>();
        transport.Initialize(NetworkManager.Singleton);

        SteamMatchmaking.OnLobbyCreated += LobbyCreated;
        SteamMatchmaking.OnLobbyEntered += LobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined += MemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave += MemberLeave;
        SteamMatchmaking.OnLobbyInvite += LobbyInvite;
        SteamMatchmaking.OnLobbyGameCreated += GameCreated;
        SteamFriends.OnGameLobbyJoinRequested += JoinRequested;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyCreated -= LobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= LobbyEntered;
        SteamMatchmaking.OnLobbyMemberJoined -= MemberJoined;
        SteamMatchmaking.OnLobbyMemberLeave -= MemberLeave;
        SteamMatchmaking.OnLobbyInvite -= LobbyInvite;
        SteamMatchmaking.OnLobbyGameCreated -= GameCreated;
        SteamFriends.OnGameLobbyJoinRequested -= JoinRequested;

        if (NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.OnConnectionEvent -= ClientConnectionEvent;

        NetworkManager.Singleton.Shutdown();
    }

    private void OnApplicationQuit()
    {
        Disconnected();
    }

    private async void JoinRequested(Lobby lobby, SteamId id)
    {
        RoomEnter joinedLobby = await lobby.Join();
        if (joinedLobby != RoomEnter.Success)
        {
            print("Failed to create lobby: " + joinedLobby.ToString());
        }
        else
        {
            currentLobby = lobby;
            print("Created lobby");
        }
    }

    public void Disconnected()
    {
        currentLobby?.Leave();
        if (NetworkManager.Singleton == null)
        {
            return;
        }
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
        else
        {
            NetworkManager.Singleton.OnConnectionEvent -= ClientConnectionEvent;
        }
        NetworkManager.Singleton.Shutdown(true);
        transport.Shutdown();
        print("Disconnecetd");
    } 

    private void GameCreated(Lobby lobby, uint ip, ushort port, SteamId id)
    {
        //print("Game Created");
    }

    private void MemberLeave(Lobby lobby, Friend friend)
    {
        print("Member Left");
    }

    private void MemberJoined(Lobby lobby, Friend friend)
    {
        print("member joined");
    }

    private void LobbyInvite(Friend friend, Lobby lobby)
    {
        print("Member Invited: " + friend.Name);
    }

    private void LobbyEntered(Lobby lobby)
    {
        if (NetworkManager.Singleton.IsHost)
        {
            print("Host Entered Lobby");
            clientSpawner.SpawnClientRpc(NetworkManager.Singleton.LocalClientId, spawnPosition);
            return;
        }
        clientSpawner.SendMessageRpc(NetworkManager.Singleton.LocalClientId.ToString());
        StartClient(currentLobby.Value.Owner.Id);
    }

    private void LobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            print("Lobby was not created");
            return;
        }
        lobby.SetPublic();
        lobby.SetJoinable(true);
        lobby.SetGameServer(lobby.Owner.Id);
    }

    bool hasClickedHost = false;
    public void StartHost(int maxMembers)
    {
        if (hasClickedHost) return;
        hasClickedHost = true;

        try
        {
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            IEnumerator startHost = StartHostEnumerator(maxMembers);
            StartCoroutine(startHost);
        }
        catch (SocketException ex)
        {
            Debug.LogError("Socket exception: " + ex.Message);
            hasClickedHost = false; // Reset in case of failure
            NetworkManager.Singleton.Shutdown(true);
            transport.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.LogError("Error starting host: " + ex.Message);
            hasClickedHost = false; // Reset in case of failure
        }
    }
    public IEnumerator StartHostEnumerator(int maxMembers)
    {
        Disconnected(); // Shutdown the current server

        // Wait until the server fully shuts down (ensure it is no longer listening)
        while (NetworkManager.Singleton.IsListening)
        {
            yield return null; // Continue checking until the server stops listening
        }

        // Optionally, wait a brief moment to ensure all network resources are fully released
        yield return new WaitForSeconds(1); // Wait for 1 second before starting the new host

        // Try starting the new host
        NetworkManager.Singleton.StartHost();

        // Now, create a Steam lobby after the host has started
        Task<Lobby?> lobbyTask = SteamMatchmaking.CreateLobbyAsync(maxMembers);

        // Wait for the lobby to be created
        while (!lobbyTask.IsCompleted)
        {
            yield return null;
        }

        // If lobby creation failed, log an error or handle the failure
        if (lobbyTask.Result == null)
        {
            Debug.LogError("Failed to create lobby.");
        }
        else
        {
            Debug.Log("Lobby created successfully.");
        }

        yield return null; // Final yield before finishing the coroutine
    }
    public void StartClient(SteamId id)
    {
        NetworkManager.Singleton.OnConnectionEvent += ClientConnectionEvent;
        transport.targetSteamId = id;
        if (NetworkManager.Singleton.StartClient())
        {
            print("Client has started");
        }
    }

    private void ClientConnectionEvent(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientConnected)
        {
            //startingCamera.SetActive(false);
            clientSpawner.SpawnClientRpc(data.ClientId,spawnPosition);
            clientSpawner.SendMessageRpc("client conencted " + data.ClientId.ToString());
        }
        else if (data.EventType == ConnectionEvent.ClientDisconnected)
        {
            clientSpawner.SendMessageRpc("client disconnected " + data.ClientId.ToString());
        }
    }

    private void OnServerStarted()
    {
        print("Host Server started");
    }
}
