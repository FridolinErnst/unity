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
            Debug.Log("other gameobject layer: " + LayerMask.LayerToName(other.gameObject.layer));
            if (other.gameObject.layer != LayerMask.NameToLayer("Spike")) return;

            // only shards manage this for anticheat
            if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;

            // check self hit
            if (other.transform.root == transform.root) return;

            //TODO check for invulnerability timer so we dont unnecessarily send data to server shards and clients
            // but how do we check that here for a remote player?

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