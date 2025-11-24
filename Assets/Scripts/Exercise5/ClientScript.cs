using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Kart
{
    public class ClientScript : NetworkBehaviour
    {
        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                Debug.Log("Registering role from client: " + Globals.networkingRole);

                RegisterRoleServerRpc(Globals.networkingRole);
            }
        }

        [Rpc(SendTo.Server)]
        public void RegisterRoleServerRpc(NetworkingRole role, RpcParams p = default)
        {
            var senderId = p.Receive.SenderClientId;
            ProxyScript.Instance.RegisterRoleServer(role, senderId);
        }
    }


}
