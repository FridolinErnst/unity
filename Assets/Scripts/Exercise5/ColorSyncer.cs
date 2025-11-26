using Kart;
using Unity.Netcode;
using UnityEngine;

public class ColorSyncer : NetworkBehaviour
{
    [SerializeField] private Material baseCarMaterial;

    public override void OnNetworkSpawn()
    {
        RequestColorServerRpc();
    }

    [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
    public void RequestColorServerRpc()
    {
        var color = ProxyScript.Instance.GetColorFromId(OwnerClientId);
        ApplyColorClientRpc(color);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ApplyColorClientRpc(Color color)
    {
        ColorizeCar(color);
    }


    private void ColorizeCar(Color color)
    {
        // Find the "car" root under the spawned prefab
        var carRoot = transform.Find("car");
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
}