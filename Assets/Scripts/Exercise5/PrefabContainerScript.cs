using UnityEngine;

public class PrefabContainerScript : MonoBehaviour
{
    public static PrefabContainerScript Instance { get; private set; }
    public GameObject aiCarPrefab;
    public GameObject playerCarPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Optional: persist across scene loads (host/server recommended)
        DontDestroyOnLoad(gameObject);
    }
}