using UnityEngine;

public class NetworkTimeControllerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject networkTimeControllerPrefab;

    private void Awake()
    {
        // If there's no existing instance, spawn one from the prefab
        if (FindObjectOfType<NetworkTimeController>() == null)
        {
            Instantiate(networkTimeControllerPrefab);
        }
    }
}
