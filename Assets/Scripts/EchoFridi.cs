using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EchoFridi : NetworkBehaviour
{
    public GameObject echoPrefab;
    private GameObject echoInstance = null;
    private bool should_spawn = true;

    public override void OnNetworkSpawn()
    {
        should_spawn = true;
    }

    public override void OnNetworkDespawn()
    {
        if (echoInstance != null && echoInstance.GetComponent<NetworkObject>().IsOwner)
        {
            Destroy(echoInstance);
        }
    }


    [Rpc(SendTo.Server)]
    void InstanceSpawnRpc()
    {
        if (IsServer)
        {
            echoInstance = Instantiate(echoPrefab);
            echoInstance.GetComponent<NetworkObject>().Spawn();
        }
    }
}
