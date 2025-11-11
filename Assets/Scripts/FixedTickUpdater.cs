/*using System;
using UnityEngine;
using Unity.Netcode;


public class FixedTickDriver : NetworkBehaviour
{
    [Header("Tick")]
    [Range(20, 120)] public int tickRate = 60;   // fixed sim Hz
    public int CurrentTick { get; private set; }
    float tickInterval;   // seconds per tick
    float tickAccumulator;


    // Local cached values
    Vector3 velocity;
    Quaternion orientation;
    Vector3 position;

    // Simple ring buffer for client input history (reconciliation)
    const int MaxHistory = 96;
    CarInput[] inputHistory = new CarInput[MaxHistory];
    int lastAckTick = -1;

    // Optional: show debug state
    public bool debugGizmos;

    void Awake()
    {
        tickInterval = 1f / Mathf.Max(1, tickRate);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        position = transform.position;
        orientation = transform.rotation;
    }

    void Update()
    {
        tickAccumulator += Time.deltaTime;
        while (tickAccumulator >= tickInterval)
        {
            tickAccumulator -= tickInterval;
            SimTick();
        }

        // Render from predicted state (owner) or reconciled state (non-owner)
        transform.SetPositionAndRotation(position, orientation);
    }

    void SimTick()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            // send current transform, rotation, m_speed, m_turning, currentTick
            // to server, unreliable
            ServerSaveState();


            // 1) Gather input for this tick
            CarInput input = ReadInput();

            // 2) Predict locally
            StepSimulation(ref position, ref orientation, ref velocity, input, tickInterval);

            // 3) Cache input for reconciliation
            int idx = (CurrentTick % MaxHistory);
            inputHistory[idx] = input;

            // 4) Send input to server tagged with tick
            SendInputServerRpc(CurrentTick, input);

            CurrentTick++;
        }
        else if (IsServer)
        {
            // Non-owners on the server are simulated by receiving inputs via RPC
            // If you want authoritative continuous sim on server, keep a dict of per-client states here.
        }
        else
        {
            // Non-owner clients just use the most recent reconciled/repredicted state
            CurrentTick++;
        }
    }


    [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    public void ServerSavesState()
    {
        // if tickrate is older than last used tickerate, discard this state and return
        ServerValidateMovement();
    }

    public void ServerExtrapolateOneTick()
    {
        //extrapolating
        ServerSendExtrapolationToClient();
    }

    [Client(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    public void ServerSendExtrapolationToClient();
    {
        // here we are interpolationdelay in the past and from there extrapolate so when the server echo on the client
        // receives the new transform and rotation, it matches the live client car state
    }

    public void ServerValidateMovement()
    {
        //check if position since last update and new position can be reached with max speed of 45
        // check that speed is < 45
        // check that turning is <90
        //if correct, then save state into ringbuffer of 20 elements
        //ServerExtrapolateOneTick, and ServerSendExtrapolationToClient otherwise extrapolate rtt/2 from last
        //then save state into ringbuffer of 20 elements
        //ServerExtrapolateOneTick, and ServerSendExtrapolationToClient
    }
}
*/