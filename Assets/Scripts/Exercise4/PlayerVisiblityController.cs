using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerVisiblityController : NetworkBehaviour
{
    [SerializeField] private LayerMask playerLayer; // Filter for player objects
    private readonly HashSet<ulong> clientsInZone = new();

    public override void OnNetworkSpawn()
    {
        //if (!IsOwner) return;
        //NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

        // Now the object is hidden for everyone until you specifically call NetworkShow(clientId)
    }

    private void OnClientConnected(ulong clientId)
    {
        // Hide players from players
        foreach (var netObj in FindObjectsOfType<NetworkObject>())
            if (netObj.gameObject.layer == playerLayer)
                if (netObj.IsSpawned && netObj.OwnerClientId != clientId && netObj.IsNetworkVisibleTo(clientId))
                    netObj.NetworkHide(clientId);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        //if (OwnerClientId == NetworkManager.ServerClientId) return;

        Debug.Log("OnTriggerEnter called from player for " + other.gameObject.name);

        // Check if the other's layer is part of the playerLayer mask
        if (((1 << other.gameObject.layer) & playerLayer.value) == 0)
            return; // Not a player object (according to the mask)

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null)
            return; // Not a networked player, ignore

        var clientId = netObj.OwnerClientId;

        // Add to the visible clients and show the object for this client
        if (clientsInZone.Add(clientId))
            if (!NetworkObject.IsNetworkVisibleTo(clientId))
            {
                NetworkObject.NetworkShow(clientId);
                Debug.Log("Client " + clientId + " entered zone, showing object.");
            }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        //if (OwnerClientId == NetworkManager.ServerClientId) return;

        Debug.Log("OnTriggerExit called from player for  " + other.gameObject.name);
        // Check if the other's layer is part of the playerLayer mask
        if (((1 << other.gameObject.layer) & playerLayer.value) == 0)
            return; // Not a player object (according to the mask)

        var netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj == null)
            return; // Not a networked player, ignore

        var clientId = netObj.OwnerClientId;
        if (clientId == NetworkManager.ServerClientId)
            return;
        // Remove from the visible clients and hide the object for this client
        if (clientsInZone.Remove(clientId))
            if (netObj.IsNetworkVisibleTo(clientId))
                NetworkObject.NetworkHide(clientId);
    }
}