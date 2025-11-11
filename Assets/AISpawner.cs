using Unity.Netcode;
using UnityEngine;

public class AISpawner : NetworkBehaviour
{
    public GameObject spawnerAIPrefab; // assign in Inspector

    public void SpawnAI()
    {
        if (NetworkManager.Singleton.IsServer) // only server should spawn
        {
            var instance = Instantiate(spawnerAIPrefab);
            var netObj = instance.GetComponent<NetworkObject>();
            netObj.Spawn(); // spawns across network
        }
    }
}