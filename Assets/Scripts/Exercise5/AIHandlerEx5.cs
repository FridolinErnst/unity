//using System;

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Utilities;
using Random = UnityEngine.Random;

namespace Kart
{
    [Serializable]
    public struct AIInputs
    {
        public Vector2 movement;
        public float breaking;
    }

    public class AIHandlerEx5 : NetworkBehaviour
    {
        public List<CarControllerEx5> characters = new();

        private AIInputs m_AIInputs;
        private Inputs m_Inputs;

        private bool _IsOwner;
        private bool _IsServer;
        private bool _IsClient;

        [SerializeField] private LayerMask playerLayer; // Filter for player objects
        private readonly HashSet<ulong> clientsInZone = new();

        //Input exercise 6
        private bool allowInput = true;
        private CountdownTimer stopInputTimer;
        private readonly int stopReceiveInputTime = 4;
        private CountdownTimer invunerablityTimer;

        private void Awake()
        {
            stopInputTimer = new CountdownTimer(stopReceiveInputTime);
            stopInputTimer.OnTimerStop += () => { AllowReceiveInput(); };
            stopInputTimer.OnTimerStart += () => { PrintTimerStart(); };
            invunerablityTimer = new CountdownTimer(stopReceiveInputTime);
            invunerablityTimer.OnTimerStart += () => { PrintTimerStart(); };
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
            stopInputTimer.Tick(Time.deltaTime);
            invunerablityTimer.Tick(Time.deltaTime);
            _IsOwner = IsOwner;
            _IsServer = IsServer;
            _IsClient = IsClient;

            if (!IsOwner) return;
            if (!allowInput) return;
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

        private void LateUpdate()
        {
            if (NetworkManager.LocalClientId != OwnerClientId) return;

            SendAiCarStateToServerRpc(transform.position, transform.rotation);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendAiCarStateToServerRpc(Vector3 position, Quaternion rotation, RpcParams p = default)
        {
            var senderId = p.Receive.SenderClientId;

            foreach (var clientId in ProxyScript.Instance.GetAllOtherClients(senderId))
                if (NetworkObject.IsNetworkVisibleTo(clientId))
                    BroadcastStateClientRpc(position, rotation, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void BroadcastStateClientRpc(Vector3 position, Quaternion rotation, RpcParams p = default)
        {
            if (IsServer) return; // No need for server to update
            transform.position = position;
            transform.rotation = rotation;
        }

        public bool StopReceivingInput()
        {
            if (invunerablityTimer.IsRunning) return false;
            allowInput = false;
            stopInputTimer.Start();
            return true;
        }

        public void AllowReceiveInput()
        {
            allowInput = true;
            Debug.Log("Allow input set to true in player car");
        }

        public void PrintTimerStart()
        {
            Debug.Log("Stop input timer started");
        }
    }
}