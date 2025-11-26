using Unity.Netcode;
using UnityEngine;

namespace Kart
{
    public class PlayerVisiblityControllerEx5 : NetworkBehaviour
    {
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