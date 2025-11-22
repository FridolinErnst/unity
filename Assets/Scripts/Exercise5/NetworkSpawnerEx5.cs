using System.Collections.Generic;
using Kart;
using Unity.Netcode;
using UnityEngine;

public class NetworkSpawnerEx5 : NetworkBehaviour
{
    private readonly int spawns = 1;

    private bool should_spawn = true;
    private GameObject NetworkedInstance;

    public GameObject aiCarPrefab;
    [SerializeField] private GameObject playerCarPrefab;
    public int aiCarCount = 3;
    private readonly List<GameObject> spawnedAICars = new();
    private readonly Color shard1Color = new(0.2f, 0.6f, 1f);
    private readonly Color shard2Color = new(1f, 0.5f, 0.2f);
    [SerializeField] private Material baseCarMaterial;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        ProxyScript.Instance.OnRoleRegistered -= HandleRoleRegistered;
        ProxyScript.Instance.OnRoleRegistered += HandleRoleRegistered;
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkedInstance != null && NetworkedInstance.GetComponent<NetworkObject>().IsOwner)
            Destroy(NetworkedInstance);
        if (IsServer)
            ProxyScript.Instance.OnRoleRegistered -= HandleRoleRegistered;
    }

    private void HandleRoleRegistered(ulong clientId, NetworkingRole role)
    {
        switch (role)
        {
            case NetworkingRole.IsClient1:
                SpawnClientCar(clientId, shard1Color);
                break;
            case NetworkingRole.IsClient2:
                SpawnClientCar(clientId, shard2Color);
                break;
            case NetworkingRole.IsShard1:
                Debug.Log("spawned a car for shard " + clientId);
                SpawnShardCar(clientId, shard1Color);
                break;
            case NetworkingRole.IsShard2:
                SpawnShardCar(clientId, shard2Color);
                break;
        }
    }

    private void ColorizeCar(GameObject car, Color color)
    {
        // Find the "car" root under the spawned prefab
        var carRoot = car.transform.Find("car");
        if (carRoot == null) return;

        // Apply to all MeshRenderers under "car"
        var renderers = carRoot.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
            // Make a unique material instance for this object
            if (baseCarMaterial != null)
            {
                r.material = new Material(baseCarMaterial);
                r.material.color = color;
            }
            else
            {
                // Using renderer.material implicitly instances the material
                r.material.color = color;
            }
    }

    private void SpawnShardCar(ulong clientId, Color color)
    {
        for (var i = 0; i < aiCarCount; i++)
        {
            var aiCar = Instantiate(aiCarPrefab, GetSpawnPoint(i), Quaternion.identity);

            // Colorize before network spawn so clients see correct visuals immediately
            ColorizeCar(aiCar, color);

            aiCar.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
            spawnedAICars.Add(aiCar);
        }
    }

    private void SpawnClientCar(ulong clientId, Color color)
    {
        var playerCar = Instantiate(playerCarPrefab, GetSpawnPoint(0), Quaternion.identity);

        // Colorize before network spawn so clients see correct visuals immediately
        ColorizeCar(playerCar, color);

        playerCar.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
        spawnedAICars.Add(playerCar);
    }


    private Vector3 GetSpawnPoint(int idx)
    {
        return new Vector3(idx * 5f, 0, 0);
    }
}