using Unity.Netcode;
using UnityEngine;

namespace Kart
{
    public class PlayerVisiblityControllerEx5 : NetworkBehaviour
    {
        /*
        [SerializeField] private LayerMask playerLayer; // Filter for player objects

        private void OnTriggerEnter(Collider other)
        {
            if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;
            if (Globals.networkingRole != ProxyScript.Instance.GetCorrespondingShardRole(OwnerClientId)) return;


            //if (OwnerClientId == NetworkManager.ServerClientId) return;

            Debug.Log("OnTriggerEnter called from player for " + other.gameObject.name);

            // Check if the other's layer is part of the playerLayer mask
            if (((1 << other.gameObject.layer) & playerLayer.value) == 0)
                return; // Not a player object (according to the mask)

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null)
                return; // Not a networked player, ignore

            var otherObjectClientId = netObj.OwnerClientId;

            // Add to the visible clients and show the object for this client
            if (!netObj.IsNetworkVisibleTo(OwnerClientId))
            {
                MakeObjectVisibleToClientServerRpc(netObj, OwnerClientId);
                Debug.Log("Client " + otherObjectClientId + " entered zone, showing object.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;
            if (Globals.networkingRole != ProxyScript.Instance.GetCorrespondingShardRole(OwnerClientId)) return;


            //if (OwnerClientId == NetworkManager.ServerClientId) return;

            Debug.Log("OnTriggerEnter called from player for " + other.gameObject.name);

            // Check if the other's layer is part of the playerLayer mask
            if (((1 << other.gameObject.layer) & playerLayer.value) == 0)
                return; // Not a player object (according to the mask)

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null)
                return; // Not a networked player, ignore

            var otherObjectClientId = netObj.OwnerClientId;

            // Add to the visible clients and show the object for this client
            if (!netObj.IsNetworkVisibleTo(OwnerClientId))
            {
                MakeObjectInvisibleToClientServerRpc(netObj, OwnerClientId);
                Debug.Log("Client " + otherObjectClientId + " entered zone, showing object.");
            }
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void MakeObjectVisibleToClientServerRpc(NetworkObjectReference targetRef, ulong clientId)
        {
            Debug.Log("making object visible to client " + clientId);
            if (targetRef.TryGet(out var target) && target.IsSpawned)
                if (!target.IsNetworkVisibleTo(clientId))
                    target.NetworkShow(clientId); // per-client show [web:28][web:21]
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void MakeObjectInvisibleToClientServerRpc(NetworkObjectReference targetRef, ulong clientId)
        {
            Debug.Log("making object invisible to client " + clientId);
            if (targetRef.TryGet(out var target) && target.IsSpawned)
                if (target.IsNetworkVisibleTo(clientId))
                    target.NetworkHide(clientId); // per-client hide [web:28][web:8]
        } */

        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private CarObserverGate carGate; // assign in inspector

        private void Reset()
        {
            if (carGate == null) carGate = GetComponentInParent<CarObserverGate>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;
            if (Globals.networkingRole != ProxyScript.Instance.GetCorrespondingShardRole(OwnerClientId)) return;

            if (((1 << other.gameObject.layer) & playerLayer.value) == 0) return;

            var playerNO = other.GetComponentInParent<NetworkObject>();
            if (playerNO == null) return;

            RequestAllowServerRpc(carGate != null ? playerNO : null, OwnerClientId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;
            if (Globals.networkingRole != ProxyScript.Instance.GetCorrespondingShardRole(OwnerClientId)) return;

            if (((1 << other.gameObject.layer) & playerLayer.value) == 0) return;

            var playerNO = other.GetComponentInParent<NetworkObject>();
            if (playerNO == null) return;

            RequestDenyServerRpc(carGate != null ? playerNO : null, OwnerClientId);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void RequestAllowServerRpc(NetworkObjectReference carRef, ulong clientId)
        {
            if (carRef.TryGet(out var carNO))
            {
                var gate = carNO.GetComponent<CarObserverGate>();
                gate?.AllowClient(clientId);
            }
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Reliable)]
        private void RequestDenyServerRpc(NetworkObjectReference carRef, ulong clientId)
        {
            if (carRef.TryGet(out var carNO))
            {
                var gate = carNO.GetComponent<CarObserverGate>();
                gate?.DenyClient(clientId);
            }
        }
    }
}