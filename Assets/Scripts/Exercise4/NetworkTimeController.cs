using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;


// check if packages are unordered that its ordering them the right way or discards packages that are older than already received ones

public class NetworkTimeController : NetworkBehaviour
{
    private const int RTT_HISTORY_SIZE = 10;
    private const int InterpolationBufferMS = 100;

    [Header("Adaptive Timing")] public float rttEMA;

    public float smoothingFactor = 0.1f;
    private readonly float interpolationDelay = 0.1f;
    public float safetyMargin = 0.02f;

    [Header("Clock Sync")] public double clockOffsetEMA; // serverTime - localTime

    [Header("Diagnostics")] public float lastMeasuredRTT;

    public float jitter;
    private readonly float alphaDown = 0.6f; // fast when offset decreases (lag returns to normal)
    private readonly float alphaUp = 0.1f; // slow when offset increases (lag spike)
    private readonly float maxInterpolationDelay = 0.25f;
    private readonly float maxRTT = 0.5f;
    private readonly float minInterpolationDelay = 0.002f;
    private readonly float minRTT = 0.001f;
    private readonly float resetTimer = 0.8f;
    private readonly Queue<RttSample> rttHistory = new();
    private readonly float safetyMarginMultiplicator = 1.5f;
    private int counter;
    private double lastPingSentTime;
    private float resendTimer = 0.8f;
    public double EstimatedServerTimeNow => NetworkManager.Singleton.LocalTime.Time + clockOffsetEMA;
    public double getClockOffset => clockOffsetEMA;

    private void Start()
    {
        SendPing();
    }

    private void Update()
    {
        if (IsClient)
        {
            resendTimer -= Time.deltaTime;
            if (resendTimer < 0)
            {
                resendTimer = resetTimer;
                SendPing();
            }
        }
    }


    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 400, 1000), GUI.skin.box);
        GUILayout.Label($"RTT: {rttEMA * 1000f:F1} ms");
        GUILayout.Label($"Jitter: {jitter * 1000f:F1} ms");
        GUILayout.Label($"Safety Margin: {safetyMargin * 1000f:F1} ms");
        GUILayout.Label($"Interpolation Delay: {interpolationDelay * 1000f:F1} ms");
        GUILayout.Label($"Clock Offset: {clockOffsetEMA:F4} s");
        GUILayout.Label($"Server Time Now: {EstimatedServerTimeNow:F4}");
        GUILayout.Space(10);

        GUILayout.Label("RTT History (ms):");
        var i = 0;
        foreach (var sample in rttHistory)
        {
            GUILayout.Label($"[{sample.counter}] {sample.rtt * 1000f:F2}");
            i++;
            // Optional: limit rows to avoid huge GUI
            // if (i > 30) break;
        }

        GUILayout.EndArea();
    }


    private void SendPing()
    {
        if (!IsClient) return;
        lastPingSentTime = NetworkManager.Singleton.LocalTime.Time;
        //Debug.Log("time: " + lastPingSentTime);
        counter++;
        SendPingServerRpc(lastPingSentTime, counter);
    }

    [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    private void SendPingServerRpc(double clientTime, int counter, ServerRpcParams rpcParams = default)
    {
        var serverTime = NetworkManager.Singleton.ServerTime.Time;
        var clientId = rpcParams.Receive.SenderClientId;
        SendPongClientRpc(clientTime, serverTime, counter, clientId);
    }

    [ClientRpc(Delivery = RpcDelivery.Unreliable)]
    private void SendPongClientRpc(double clientTime, double serverTime, int counter, ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        // return if this is not the latest ping
        if (rttHistory.Count > 0 && counter < rttHistory.Last().counter)
            return;

        var clientNow = NetworkManager.Singleton.LocalTime.Time;

        // --- RTT ---
        var newRTT = (float)(clientNow - clientTime);
        newRTT = Mathf.Clamp(newRTT, minRTT, maxRTT); // 1 ms–500 ms range

        lastMeasuredRTT = newRTT;

        // --- Update RTT history ---
        rttHistory.Enqueue(new RttSample(newRTT, counter));
        if (rttHistory.Count > RTT_HISTORY_SIZE)
            rttHistory.Dequeue();

        var mean = ComputeWeightedMean(rttHistory);
        var variance = ComputeWeightedVariance(rttHistory, mean);
        jitter = Mathf.Sqrt(variance);

        // --- Smoothed RTT ---
        smoothingFactor = newRTT < mean ? alphaDown : alphaUp;
        if (rttEMA == 0) rttEMA = newRTT;
        else rttEMA = Mathf.Lerp(rttEMA, newRTT, smoothingFactor);
        rttEMA = Mathf.Clamp(rttEMA, minRTT, maxRTT);

        /* removed this since it would create unfairness when a player has a stable low ping
        // --- Adaptive safety margin ---
        safetyMargin = safetyMarginMultiplicator * jitter;


        // --- Adaptive interpolation delay ---
        interpolationDelay = rttEMA * 0.5f + safetyMargin;
        interpolationDelay =
            Mathf.Clamp(interpolationDelay, minInterpolationDelay, maxInterpolationDelay); // 2–250 ms window
        */

        // --- Clock offset calculation ---
        var estimatedOneWay = rttEMA * 0.5;
        var newOffset = serverTime + estimatedOneWay - clientNow;
        clockOffsetEMA = Mathf.Lerp((float)clockOffsetEMA, (float)newOffset, smoothingFactor);
    }

    public float GetRenderDelay()
    {
        return interpolationDelay;
    }

    public float GetRTT()
    {
        return rttEMA;
    }

    public float GetJitter()
    {
        return jitter;
    }

    public double GetClockOffset()
    {
        return clockOffsetEMA;
    }

    public double GetServerTimeNow()
    {
        return EstimatedServerTimeNow;
    }

    public double GetRemoteObjectRenderTime()
    {
        return EstimatedServerTimeNow - interpolationDelay;
    }

    private float ComputeWeightedMean(IEnumerable<RttSample> samples)
    {
        var weightedSum = 0f;
        var totalWeight = 0f;
        var n = samples.Count();
        var index = 1;

        foreach (var sample in samples)
        {
            float weight = index * index * index;
            weightedSum += sample.rtt * weight;
            totalWeight += weight;
            index++;
        }

        return weightedSum / totalWeight;
    }

    private float ComputeWeightedVariance(IEnumerable<RttSample> samples, float mean)
    {
        var weightedSum = 0f;
        var totalWeight = 0f;
        var index = 1;

        foreach (var sample in samples)
        {
            var diff = sample.rtt - mean;
            float weight = index * index * index;
            weightedSum += diff * diff * weight;
            totalWeight += weight;
            index++;
        }

        return weightedSum / totalWeight;
    }
}

public struct RttSample
{
    public float rtt;
    public int counter;

    public RttSample(float rtt, int counter)
    {
        this.rtt = rtt;
        this.counter = counter;
    }
}