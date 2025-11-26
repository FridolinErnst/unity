using Unity.Netcode;
using UnityEngine;

namespace Kart
{
    public class DamageSpike : NetworkBehaviour
    {
        // important: a car does not hit another car, a car gets hit by another
        // so we check on a car if we should have been hit, not if we hit another car
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log(
                "client " + transform.name + " with id " + NetworkManager.LocalClientId + " and object entered " +
                other.name);
            // only allow spike to trigger
            if (other.gameObject.layer != LayerMask.NameToLayer("Spike")) return;

            // only shards manage this for anticheat
            if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;

            // check self hit
            if (other.transform.root == transform.root) return;

            // shard manages only own AI car
            if (IsOwner)
            {
                Debug.Log("StopreceivingInput received");

                var carController = transform.root.GetComponent<AIHandlerEx5>();
                if (carController != null)
                {
                    Debug.Log("Kartcontroller nut null andwe stop input");

                    carController.StopReceivingInput();
                }
            }
            // shard manages only respective client car
            else if (Globals.networkingRole ==
                     ProxyScript.Instance.GetCorrespondingShardRole(OwnerClientId))
            {
                StopClientInputServerRpc(OwnerClientId);
            }
        }


        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void StopClientInputServerRpc(ulong clientId, RpcParams p = default)
        {
            Debug.Log("StopClientInputServerRpc called for clientId: " + clientId + " with role " +
                      ProxyScript.Instance.GetCorrespondingShardRole(clientId));
            StopReceivingInputClientRpc(RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }


        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void StopReceivingInputClientRpc(RpcParams rpcParams = default)
        {
            Debug.Log("StopreceivingInput received on client");
            var root = transform.root;
            Debug.Log("Root object name: " + root.name);
            Debug.Log("ownerclientId: " + OwnerClientId);

            // Print all MonoBehaviour scripts attached to root
            var scripts = root.GetComponents<MonoBehaviour>();
            foreach (var script in scripts) Debug.Log("Root has script: " + script.GetType().Name);
            var carController = transform.root.GetComponent<KartControllerEx5>();
            Debug.Log("KartcontrollerEx5 " + carController);
            if (carController != null)
            {
                Debug.Log("Kartcontroller nut null andwe stop input");

                carController.StopReceivingInput();
            }
        }
    }
}
/*



         [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
private void StopOtherCarFromReceivingInputServerRpc(NetworkObjectReference carRef, RpcParams p = default)
{
    var carRefOwnerClientId = ProxyScript.Instance.ErrorClientId;
    if (carRef.TryGet(out var carNO)) carRefOwnerClientId = carNO.OwnerClientId;
    if (!ProxyScript.Instance.ClientIdToRole.ContainsKey(carRefOwnerClientId))
        return;
    var observgate = carNO.GetComponent<CarObserverGate>();
    Debug.Log("is client allowed " + observgate.CheckVisibility(carRefOwnerClientId));
    Debug.Log("NetworkingRole for carRefOwnerClientId: " +
              ProxyScript.Instance.ClientIdToRole[carRefOwnerClientId]);

    //log all other ids and roles
    foreach (var kvp in ProxyScript.Instance.ClientIdToRole)
        Debug.Log("ClientId: " + kvp.Key + " Role: " + kvp.Value);
    if (ProxyScript.Instance.ClientIdToRole[carRefOwnerClientId] == NetworkingRole.IsShard1 ||
        ProxyScript.Instance.ClientIdToRole[carRefOwnerClientId] == NetworkingRole.IsShard2)
    {
        Debug.Log("carrefownerclientid: " + carRefOwnerClientId + " has role " +
                  ProxyScript.Instance.ClientIdToRole[carRefOwnerClientId]);
        Debug.Log(" so we call for a shard");
        StopReceivingInputShardRpc(RpcTarget.Single(carRefOwnerClientId, RpcTargetUse.Temp));
    }

    if (ProxyScript.Instance.ClientIdToRole[carRefOwnerClientId] == NetworkingRole.IsClient1 ||
        ProxyScript.Instance.ClientIdToRole[carRefOwnerClientId] == NetworkingRole.IsClient2)
    {
        Debug.Log("carrefownerclientid: " + carRefOwnerClientId + " has role " +
                  ProxyScript.Instance.ClientIdToRole[carRefOwnerClientId]);
        Debug.Log(" so we call for a client");
        StopReceivingInputClientRpc(RpcTarget.Single(carRefOwnerClientId, RpcTargetUse.Temp));
    }
}
[Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
private void StopReceivingInputShardRpc(RpcParams rpcParams = default)
{
    Debug.Log("StopreceivingInput received");

    var carController = transform.root.GetComponent<AIHandlerEx5>();
    if (carController != null)
    {
        Debug.Log("Kartcontroller nut null andwe stop input");

        carController.StopReceivingInput();
    }
}*/

/*
private void OnTriggerEnter(Collider other)
{
    if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;
    var playerNO = other.transform.root.GetComponent<NetworkObject>();
    if (playerNO == null)
        return;
    //check if the other car that is hit is managed by this shard (ai cars && player car)
    if (OwnerClientId != playerNO.NetworkManager.LocalClientId && Globals.networkingRole !=
        ProxyScript.Instance.GetCorrespondingShardRole(OwnerClientId)) return;

    Debug.Log("DamageSpike OnTriggerEnter called on object: " + gameObject.name + "for object" +
              other.gameObject.name + "with owner id: " + OwnerClientId);

    if (other.gameObject.layer != LayerMask.NameToLayer("Spike")) return;


    Debug.Log(" other object " + other.gameObject.name + " other owner: " + playerNO.OwnerClientId);
    Debug.Log("the object with owner id: " + OwnerClientId + "hit the object: " + other.name + " with id " +
              playerNO.OwnerClientId);

    StopOtherCarFromReceivingInputServerRpc(playerNO);
}*/