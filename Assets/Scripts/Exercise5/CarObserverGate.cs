using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class CarObserverGate : NetworkBehaviour
{
    // Server-only list of clients allowed to observe this car
    private readonly HashSet<ulong> allowedClients = new();
    private NetworkObject _no;

    private void Awake()
    {
        _no = GetComponent<NetworkObject>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            // Wire the visibility callback; NGO consults this to decide per-client observation/spawn
            _no.CheckObjectVisibility = CheckVisibility;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            _no.CheckObjectVisibility -= CheckVisibility;
            allowedClients.Clear();
        }
    }

    // NGO calls this before sending spawn/updates to a client for this object
    private bool CheckVisibility(ulong clientId)
    {
        // Only clients in the allow-list are observers; others won’t get the spawn at all
        return IsSpawned && allowedClients.Contains(clientId);
    }

    // Server API to grant visibility
    public void AllowClient(ulong clientId)
    {
        if (!IsServer) return;
        if (allowedClients.Add(clientId))
            // If not already visible, add as observer (this triggers a spawn to that client)
            if (!_no.IsNetworkVisibleTo(clientId))
                _no.NetworkShow(clientId);
    }

    // Server API to revoke visibility
    public void DenyClient(ulong clientId)
    {
        if (!IsServer) return;
        if (allowedClients.Remove(clientId))
            if (_no.IsNetworkVisibleTo(clientId))
                _no.NetworkHide(clientId); // client will despawn the car
    }

    // Optional: helpers to batch-allow current connected clients, debug etc.
}