//using System;

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Kart
{
    [Serializable]
    public struct AIInputs
    {
        public Vector2 movement;
        public float breaking;
    }

    public class AIHandlerEx4 : NetworkBehaviour
    {
        public List<CarControllerEx4> characters = new();

        private AIInputs m_AIInputs;
        private Inputs m_Inputs;

        private bool _IsOwner;
        private bool _IsServer;
        private bool _IsClient;

        [SerializeField] private LayerMask playerLayer; // Filter for player objects
        private readonly HashSet<ulong> clientsInZone = new();

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            //NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            // Now the object is hidden for everyone until you specifically call NetworkShow(clientId)
        }

        private void OnClientConnected(ulong clientId)
        {
            // Hide all player objects, AI, etc from this new client by default
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
                // Optionally skip the joining client's own player object,
                // or any objects meant to be always visible.

                if (netObj.IsSpawned && netObj.OwnerClientId != clientId && netObj.IsNetworkVisibleTo(clientId))
                    netObj.NetworkHide(clientId);
        }

        private void Update()
        {
            _IsOwner = IsOwner;
            _IsServer = IsServer;
            _IsClient = IsClient;

            if (!IsOwner) return;

            // get old controlls

            // adjust controlls
            m_AIInputs.movement.x += Random.Range(-0.5f, 0.5f);
            m_AIInputs.movement.x = Mathf.Clamp(m_AIInputs.movement.x, -1f, +1f);
            m_AIInputs.movement.y += Random.Range(-0.2f, 0.5f);
            m_AIInputs.movement.y = Mathf.Clamp(m_AIInputs.movement.y, -0f, +3f);
            m_AIInputs.breaking += Random.Range(-0.5f, 0.5f);
            m_AIInputs.breaking = Mathf.Clamp(m_AIInputs.breaking, -3f, +1f - m_Inputs.break_time / 10f);

            // force controlls to be 100%
            m_Inputs.movement.x = Mathf.Round(m_AIInputs.movement.x);
            m_Inputs.movement.y = Mathf.Round(m_AIInputs.movement.y);

            // update character Controllers
            foreach (var character in characters)
            {
                var distance = character.transform.position.magnitude;
                var rotation = Vector3.zero;
                if (character.transform.position != Vector3.zero)
                    rotation = Quaternion.Inverse(Quaternion.LookRotation(character.transform.position.normalized)) *
                               character.transform.forward;

                // no breaking when standing still
                if (character.m_speed == 0)
                {
                    m_AIInputs.breaking = -2;
                    m_AIInputs.movement.y = 2;
                }

                // clean controlls
                if (m_AIInputs.breaking > 0)
                {
                    m_AIInputs.movement.y = 0;
                    m_Inputs.break_time += Time.deltaTime;
                }
                else
                {
                    m_Inputs.break_time = 0.0f;
                }

                // make sure ai stays in range
                if (distance > 300 && rotation.z > 0.0f)
                {
                    if (rotation.x >= 0.0f)
                        m_Inputs.movement.x = 1.0f;
                    else
                        m_Inputs.movement.x = -1.0f;
                }

                character.ApplyInputs(m_Inputs, Time.deltaTime);
            }
        }


        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            //if (OwnerClientId == NetworkManager.ServerClientId) return;

            Debug.Log("OnTriggerEnter called for " + other.gameObject.name);

            // Check if the other's layer is part of the playerLayer mask
            if (((1 << other.gameObject.layer) & playerLayer.value) == 0)
                return; // Not a player object (according to the mask)

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null)
                return; // Not a networked player, ignore

            var clientId = netObj.OwnerClientId;
            // Add to the visible clients and show the object for this client
            if (clientId == NetworkManager.ServerClientId)
                return;
            if (clientsInZone.Add(clientId))
                if (!NetworkObject.IsNetworkVisibleTo(clientId))
                {
                    NetworkObject.NetworkShow(clientId);
                    Debug.Log("Client " + clientId + " entered zone, showing object.");
                }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsServer) return;
            //if (OwnerClientId == NetworkManager.ServerClientId) return;
            Debug.Log("OnTriggerExit called for " + other.gameObject.name);
            // Check if the other's layer is part of the playerLayer mask
            if (((1 << other.gameObject.layer) & playerLayer.value) == 0)
                return; // Not a player object (according to the mask)

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj == null)
                return; // Not a networked player, ignore

            var clientId = netObj.OwnerClientId;
            // Remove from the visible clients and hide the object for this client
            if (clientId == NetworkManager.ServerClientId)
                return;

            if (clientsInZone.Remove(clientId))
                if (NetworkObject.IsNetworkVisibleTo(clientId))
                    NetworkObject.NetworkHide(clientId);
        }

        private void LateUpdate()
        {
            if (!IsServer) return;

            foreach (var clientId in clientsInZone)
            {
                // Broadcast state only to clients in the zone
                Debug.Log("Broadcasting to client " + clientId);
                BroadcastStateClientRpc(transform.position, transform.rotation,
                    new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
                    });
            }
        }

        [ClientRpc]
        private void BroadcastStateClientRpc(Vector3 position, Quaternion rotation, ClientRpcParams clientRpcParams)
        {
            if (IsServer) return; // No need for server to update
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}