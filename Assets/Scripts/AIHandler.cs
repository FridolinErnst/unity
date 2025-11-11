//using System;

using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

[Serializable]
public struct AIInputs
{
    public Vector2 movement;
    public float breaking;
}

public class AIHandler : NetworkBehaviour
{
    public List<CarController> characters = new();

    private AIInputs m_AIInputs;
    private Inputs m_Inputs;

    private bool _IsOwner;
    private bool _IsServer;
    private bool _IsClient;


    private void Update()
    {
        _IsOwner = IsOwner;
        _IsServer = IsServer;
        _IsClient = IsClient;

        if (!IsServer) return;

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

            character.ApplyInputs(m_Inputs);
        }
    }
}