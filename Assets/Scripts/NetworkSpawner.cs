using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor.PackageManager;
using UnityEngine;

public class NetworkSpawner : NetworkBehaviour
{
    public GameObject NetworkedPrefab;
    public bool getOwnership = false;
    public int spawns = 1;

    private bool should_spawn = true;
    private GameObject NetworkedInstance;

    // Start is called before the first frame update
    public override void OnNetworkSpawn()
    {
        should_spawn = true;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkedInstance != null && NetworkedInstance.GetComponent<NetworkObject>().IsOwner)
        {
            Destroy(NetworkedInstance);
        }
    }

    private void Update()
    {
        if (IsOwner && should_spawn)
        {
            should_spawn = false;
            var clientId = GetComponent<NetworkObject>().OwnerClientId;
            for (int i = 0; i < spawns; i++)
            {
                InstanceSpawnRpc(clientId, getOwnership);
            }
        }
    }

    [Rpc(SendTo.Server)]
    void InstanceSpawnRpc(ulong clientId, bool getOwnership)
    {
        if (IsServer)
        {
            NetworkedInstance = Instantiate(NetworkedPrefab);
            NetworkedInstance.GetComponent<NetworkObject>().Spawn();
            if (getOwnership)
                NetworkedInstance.GetComponent<NetworkObject>().ChangeOwnership(clientId);
        }
    }
}
