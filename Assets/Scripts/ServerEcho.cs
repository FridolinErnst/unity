using Kart;
using Unity.Netcode;
using UnityEngine;

public class ServerEcho : NetworkBehaviour
{
    public GameObject echoPrefab;
    private GameObject echoInstance;
    private KartController kartController;
    private bool should_spawn = true;

    private void Update()
    {
        if (IsOwner && should_spawn)
        {
            should_spawn = false;
            InstanceSpawnRpc();
        }

        if (IsServer && echoInstance != null)
        {
            echoInstance.transform.position = transform.position;
            echoInstance.transform.rotation = transform.rotation;
        }
    }

    private void LateUpdate()
    {
        if (IsServer && echoInstance != null)
        {
            echoInstance.transform.position = transform.position;
            echoInstance.transform.rotation = transform.rotation;
        }
    }

    public override void OnNetworkSpawn()
    {
        should_spawn = true;
        if (!IsServer) return;
        kartController = GetComponent<KartController>();
    }

    public override void OnNetworkDespawn()
    {
        if (echoInstance != null && echoInstance.GetComponent<NetworkObject>().IsOwner) Destroy(echoInstance);
    }

    [Rpc(SendTo.Server)]
    private void InstanceSpawnRpc()
    {
        if (IsServer)
        {
            echoInstance = Instantiate(echoPrefab);
            echoInstance.GetComponent<NetworkObject>().Spawn();
        }
    }
}