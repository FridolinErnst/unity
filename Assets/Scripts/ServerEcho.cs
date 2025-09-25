using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ServerEcho : NetworkBehaviour
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

    void Update()
    {
        if (IsOwner && should_spawn)
        {
            should_spawn = false;
            InstanceSpawnRpc();
        }
        if (IsServer && echoInstance != null)
        {
            echoInstance.transform.position = this.transform.position;
            echoInstance.transform.rotation = this.transform.rotation;
        }
    }
    void LateUpdate()
    {
        if (IsServer && echoInstance != null)
        {
            echoInstance.transform.position = this.transform.position;
            echoInstance.transform.rotation = this.transform.rotation;
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
