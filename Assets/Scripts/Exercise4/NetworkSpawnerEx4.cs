using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkSpawnerEx4 : NetworkBehaviour
{
    private readonly int spawns = 1;

    private bool should_spawn = true;
    private GameObject NetworkedInstance;

    public GameObject aiCarPrefab;
    public int aiCarCount = 3;
    private readonly List<GameObject> spawnedAICars = new();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            for (var i = 0; i < aiCarCount; i++)
            {
                // Adjust position/rotation as needed
                var aiCar = Instantiate(aiCarPrefab, GetSpawnPoint(i), Quaternion.identity);
                aiCar.GetComponent<NetworkObject>().Spawn(); // false: don't assign ownership to any client
                spawnedAICars.Add(aiCar);
            }
    }

    private Vector3 GetSpawnPoint(int idx)
    {
        return new Vector3(idx * 5f, 0, 0); // Example
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkedInstance != null && NetworkedInstance.GetComponent<NetworkObject>().IsOwner)
            Destroy(NetworkedInstance);
    }
}