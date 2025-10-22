using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;

public class NetworkSync : NetworkBehaviour
{
    NetworkVariable<Vector3> Position = new NetworkVariable<Vector3>(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );
    NetworkVariable<Quaternion> Rotation = new NetworkVariable<Quaternion>(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    public bool _IsOwner;
    public bool _IsServer;
    public bool _IsClient;
    
    public bool isPlayerCar = true; // Set to false for Echos
    private Vector3 lastPosition = Vector3.zero;
    public float maxAllowedSpeed = 200f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (!IsOwner) // Only non-owners should use prediction
        {
            Position.OnValueChanged += (oldValue, newValue) =>
            {
                OnNetworkPositionUpdate(newValue);
            };
        }
    }

    void Update()
    {
        _IsOwner = IsOwner;
        _IsServer = IsServer;
        _IsClient = IsClient;

        if (IsOwner)
        {
            // Owner sends position to server for validation
            if (isPlayerCar)
            {
                ValidateSpeedServerRpc(transform.position);
            }
            
            Position.Value = transform.position;
            Rotation.Value = transform.rotation;
        }
        else
        {
            transform.position = Position.Value;
            transform.rotation = Rotation.Value;
        }
    }

    [Rpc(SendTo.Server)]
    private void ValidateSpeedServerRpc(Vector3 clientPosition)
    {
        // Server-side anti-cheat: Only validate player cars
        float distance = Vector3.Distance(clientPosition, lastPosition);
        float speed = distance / Time.deltaTime;
        
        if (speed > maxAllowedSpeed)
        {
            Debug.Log($"Anti-Cheat: Player {OwnerClientId} driving too fast! Speed: {speed}. Resetting position.");
            // Reset the player to the last valid position via ClientRpc
            ResetPositionClientRpc(lastPosition);
        }
        else
        {
            lastPosition = clientPosition;
        }
    }

    [Rpc(SendTo.Owner)]
    private void ResetPositionClientRpc(Vector3 validPosition)
    {
        // Reset player's position on their client
        transform.position = validPosition;
        Position.Value = validPosition;
    }

    // When client receives position update from server
    void OnNetworkPositionUpdate(Vector3 serverPos)
    {
        EchoPositionPredictor predictor = GetComponent<EchoPositionPredictor>();
        if (predictor != null)
        {
            predictor.OnServerPositionChanged(serverPos);
        }
    }
}