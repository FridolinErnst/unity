using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DetachComponent : NetworkBehaviour
{
    public Transform[] components;
    public override void OnNetworkSpawn()
    {
        foreach (var component in components)
        {
            component.parent = null;
        }
    }

    public override void OnNetworkDespawn()
    {
        foreach (var component in components)
        {
            if (component != null && (component.GetComponent<NetworkObject>() == null || component.GetComponent<NetworkObject>().IsOwner))
            {
                Destroy(component.gameObject);
            }
        }
    }
}
