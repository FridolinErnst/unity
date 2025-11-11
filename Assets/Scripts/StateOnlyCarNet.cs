using System;
using Unity.Netcode;
using UnityEngine;

// Mark as struct, no managed refs, implement INetworkSerializable
public struct StateSample : INetworkSerializable
{
    public int tick;
    public double serverTime;
    public Vector3 position;
    public Quaternion rotation;
    public float speed;
    public float rotationSpeed;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref tick);
        serializer.SerializeValue(ref serverTime);
        serializer.SerializeValue(ref position);
        serializer.SerializeValue(ref rotation);
        serializer.SerializeValue(ref speed);
        serializer.SerializeValue(ref rotationSpeed);
    }
}

public class StateOnlyCarNet : NetworkBehaviour
{
    private EchoFridi echoFridi; // sibling spawner
    private Transform echoTransform; // cached transform from spawner

    [Header("Tick")] [Range(20, 120)] public int tickRate = 60;
    public int CurrentTick { get; private set; }
    private float tickInterval;
    private float tickAccumulator;

    [Header("Authority Caps (Server)")] public float maxSpeed = 45f; // m/s
    public float maxTurnRateDeg = 180f; // deg/s

    [Header("Soft Reconciliation (Owner)")] [Range(0.01f, 0.5f)]
    public float reconcileTime = 0.15f; // seconds to blend corrections

    [Range(0f, 1f)] public float snapIfErrorMeters = 3f; // snap if error too large
    private Vector3 reconcileVel; // internal blend helpers
    private float reconcileAngularVel;

    [Header("Echo Rendering (Optional)")]
    public NetworkTimeController timeCtrl; // your timing script providing EstimatedServerTimeNow & interpolationDelay

    [Header("Buffers")] public int historySize = 20;
    private StateSample[] serverHistory; // authoritative ring buffer (client side copy for echo)
    private int serverHistoryCount;
    private int serverHistoryHead; // head points to last written

    // Local last frame data (owner) to compute speed/rotSpeed
    private Vector3 lastPos;
    private Quaternion lastRot;
    private bool haveLast;

    // Server-side authoritative last snapshot per object
    private StateSample serverLast;

    private void Awake()
    {
        tickInterval = 1f / Mathf.Max(1, tickRate);
        serverHistory = new StateSample[Mathf.Max(4, historySize)];
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        lastPos = transform.position;
        lastRot = transform.rotation;
        haveLast = true;

        // Initialize serverLast with current transform for sanity
        serverLast = new StateSample
        {
            tick = 0,
            serverTime = 0,
            position = transform.position,
            rotation = transform.rotation,
            speed = 0,
            rotationSpeed = 0
        };
    }

    private void Update()
    {
        if (timeCtrl == null)
            timeCtrl = FindObjectOfType<NetworkTimeController>(false);

        // Fixed tick loop decoupled from frame rate
        tickAccumulator += Time.deltaTime;
        while (tickAccumulator >= tickInterval)
        {
            tickAccumulator -= tickInterval;
            SimTick();
        }


        // Echo rendering (interpolate authoritative history at render time)
        if (IsOwner && echoTransform != null && serverHistoryCount > 0 && timeCtrl != null)
        {
            var renderTime = timeCtrl.EstimatedServerTimeNow - timeCtrl.GetRenderDelay();
            if (TrySampleHistory(renderTime, out var ePos, out var eRot))
            {
                echoTransform.SetPositionAndRotation(ePos, eRot);
            }
            else
            {
                // If cannot sample, hold last known
                var last = serverHistory[(serverHistoryHead - 1 + serverHistory.Length) % serverHistory.Length];
                echoTransform.SetPositionAndRotation(last.position, last.rotation);
            }
        }
    }

    private void SimTick()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            // Compute speed and yaw-rate based on local motion since last tick
            var dt = tickInterval;
            var curPos = transform.position;
            var curRot = transform.rotation;

            var speed = 0f;
            var yawRate = 0f;

            if (haveLast)
            {
                speed = (curPos - lastPos).magnitude / Mathf.Max(1e-6f, dt);
                var deltaYaw = Mathf.DeltaAngle(lastRot.eulerAngles.y, curRot.eulerAngles.y);
                yawRate = deltaYaw / Mathf.Max(1e-6f, dt);
            }

            lastPos = curPos;
            lastRot = curRot;
            haveLast = true;

            // Send state to server for tick
            SendStateServerRpc(new StateSample
            {
                tick = CurrentTick,
                serverTime = 0, // server fills this in
                position = curPos,
                rotation = curRot,
                speed = speed,
                rotationSpeed = yawRate
            });

            CurrentTick++;
        }
        else
        {
            // Non-owner: nothing special per tick here; state updates come via ClientRpc.
            CurrentTick++;
        }
    }

    // SERVER: receive client state, validate, clamp, store authoritative snapshot, and return to owner
    [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    private void SendStateServerRpc(StateSample clientState, ServerRpcParams rpcParams = default)
    {
        var senderId = rpcParams.Receive.SenderClientId;

        // 1) Drop stale or duplicate packets
        if (clientState.tick <= serverLast.tick)
            return;

        var dt = tickInterval;

        // 2) How many ticks were missed since our last authoritative tick?
        var gap = clientState.tick - serverLast.tick;
        var maxTickGap = 6; // cap to avoid huge jumps
        gap = Mathf.Clamp(gap, 1, maxTickGap);

        // 3) Start from last authoritative state
        var pos = serverLast.position;
        var rot = serverLast.rotation;

        // 4) Desired per-tick rates from client (hints only) � clamp to caps
        //    We use the same speed/yaw for each step through the gap
        var stepSpeed = Mathf.Clamp(clientState.speed, 0f, maxSpeed); // m/s
        var stepYawRate = Mathf.Clamp(clientState.rotationSpeed, -maxTurnRateDeg, maxTurnRateDeg); // deg/s

        // 5) Advance one tick at a time with per-step clamping (arc motion)
        for (var s = 0; s < gap; s++)
        {
            // Clamp linear motion this step
            var maxDist = maxSpeed * dt;
            var desiredDist = stepSpeed * dt;
            var dist = Mathf.Min(desiredDist, maxDist);

            // Clamp yaw this step
            var maxDeltaYaw = maxTurnRateDeg * dt;
            var yawStep = Mathf.Clamp(stepYawRate * dt, -maxDeltaYaw, maxDeltaYaw);

            // Apply yaw first to turn along an arc
            var newYaw = rot.eulerAngles.y + yawStep;
            rot = Quaternion.Euler(0f, newYaw, 0f);

            // Move forward in the new facing
            pos += rot * Vector3.forward * dist;
        }

        // 6) Build authoritative snapshot at server time
        var authSpeed = gap > 0 ? Mathf.Min(stepSpeed, maxSpeed) : 0f;
        var authYawRate = gap > 0 ? Mathf.Clamp(stepYawRate, -maxTurnRateDeg, maxTurnRateDeg) : 0f;

        var auth = new StateSample
        {
            tick = clientState.tick,
            serverTime = NetworkManager.ServerTime.Time,
            position = pos,
            rotation = rot,
            speed = authSpeed,
            rotationSpeed = authYawRate
        };

        // 7) Update server state and store in ring buffer
        serverLast = auth;
        ServerStoreHistory(auth); // your ring-buffer push

        // 8) Return authoritative to the owner (rubberband client toward this)
        SendAuthoritativeClientRpc(auth, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } }
        });

        // Optionally broadcast to others for remote interpolation
        // BroadcastAuthoritativeClientRpc(auth);
    }


    // CLIENT: receive authoritative state and apply soft reconciliation (rubberband)
    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendAuthoritativeClientRpc(StateSample state, ClientRpcParams _params = default)
    {
        // Keep echo history for interpolation/visualization
        ClientStoreHistory(state);

        if (!IsOwner) return; // only the owner reconciles their own object

        // Measure error relative to current transform
        var curPos = transform.position;
        var curRot = transform.rotation;

        var errPos = state.position - curPos;
        var errDist = errPos.magnitude;

        // If very large error, snap
        if (errDist > snapIfErrorMeters)
        {
            //UNCOMMENTtransform.SetPositionAndRotation(state.position, state.rotation);
            lastPos = state.position;
            lastRot = state.rotation;
            return;
        }

        // Otherwise, rubberband: move a fraction toward authoritative over reconcileTime
        // Do small steps immediately to keep visuals responsive
        var alpha = Mathf.Clamp01(tickInterval / Mathf.Max(0.01f, reconcileTime));
        var correctedPos = Vector3.Lerp(curPos, state.position, alpha);
        var correctedRot = Quaternion.Slerp(curRot, state.rotation, alpha);

        //UNCOMMENTtransform.SetPositionAndRotation(correctedPos, correctedRot);

        // Update lastPos/lastRot so outgoing speed/rotSpeed stay consistent
        lastPos = correctedPos;
        lastRot = correctedRot;
    }

    // ---------------- History (server echo on client) ----------------

    private void ServerStoreHistory(StateSample s) // server side if needed (not used on server in this example)
    {
        // On a full server, you�d keep per-client history dictionaries.
    }

    private void ClientStoreHistory(StateSample s)
    {
        serverHistoryHead = (serverHistoryHead + 1) % serverHistory.Length;
        serverHistory[serverHistoryHead] = s;
        if (serverHistoryCount < serverHistory.Length) serverHistoryCount++;
    }

    // Interpolate pose at a specific serverTime; small extrapolation allowed (clamped)
    private bool TrySampleHistory(double serverTime, out Vector3 pos, out Quaternion rot)
    {
        pos = default;
        rot = default;
        if (serverHistoryCount == 0) return false;

        // Find two samples around the requested time
        var n = serverHistoryCount;
        // Walk backward from newest to find first sample older than or equal to serverTime
        var idx = serverHistoryHead;
        var newer = serverHistory[idx];
        for (var i = 0; i < n; i++)
        {
            var older = serverHistory[idx];
            if (older.serverTime <= serverTime)
            {
                // We have older <= t, newer >= t
                // Find the next newer sample forward from older
                var newerIdx = (idx + 1) % serverHistory.Length;
                newer = serverHistory[newerIdx];

                var span = Math.Max(1e-6, newer.serverTime - older.serverTime);
                double t = Mathf.Clamp01((float)((serverTime - older.serverTime) / span));
                pos = Vector3.Lerp(older.position, newer.position, (float)t);
                rot = Quaternion.Slerp(older.rotation, newer.rotation, (float)t);
                return true;
            }

            idx = (idx - 1 + serverHistory.Length) % serverHistory.Length;
        }

        // If all samples are newer than requested time, interpolate toward the oldest (edge case)
        // Or extrapolate a tiny amount from newest
        var newest = serverHistory[serverHistoryHead];
        var prev = serverHistory[(serverHistoryHead - 1 + serverHistory.Length) % serverHistory.Length];

        var dt = newest.serverTime - prev.serverTime;
        var v = dt > 1e-6 ? newest.speed : 0f; // speed already per second
        var yawRate = dt > 1e-6 ? newest.rotationSpeed : 0f;

        var ahead = serverTime - newest.serverTime;
        // Cap extrapolation (e.g., 0.1 s)
        ahead = Math.Clamp(ahead, -0.1, 0.1);

        pos = newest.position + newest.rotation * Vector3.forward * (float)(v * ahead);
        var yawDelta = yawRate * (float)ahead;
        rot = Quaternion.Euler(0f, newest.rotation.eulerAngles.y + yawDelta, 0f);
        return true;
    }
}