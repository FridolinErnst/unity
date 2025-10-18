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
            Position.Value = transform.position;
            Rotation.Value = transform.rotation;
        }
        else
        {
            transform.position = Position.Value;
            transform.rotation = Rotation.Value;
        }
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