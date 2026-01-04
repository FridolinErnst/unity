// questions. is the deltaTime and Processmovement function correct and how is this handled in professional games
// how can we stop the player from teleporting? right now if you spam Q it still teleports. do we need to check multilpe steps?
// i dont know why

//TODO make jitter better

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Utilities;
using Debug = UnityEngine.Debug;

namespace Kart
{
    public class KartControllerEx5 : NetworkBehaviour
    {
        public const float k_serverTickRate = 60f;
        private const int k_bufferSize = 1024;

        private readonly float extrapolationLimit = 0.5f; // 500ms
        private readonly float reconciliationCooldownTime = 1f;


        //ServerEcho
        [SerializeField] private GameObject ServerEchoPrediction;
        private readonly List<StatePayload> serverEchoPredictionBuffer = new();
        private int lastEchoPredictionBufferTick = -1;

        [Header("Netcode")] private readonly float reconciliationThreshold = 13.5f;
        private CarControllerEx5 carController;
        private CircularBuffer<InputPayload> clientInputBuffer;
        private ClientNetworkTransform clientNetworkTransform;


        // Netcode client specific
        private CircularBuffer<StatePayload> clientStateBuffer;
        private StatePayload extrapolationState;
        private List<StatePayload> interpolationBuffer;

        private int lastInterpolationBufferTick = -1;

        // stop extrapolating after a certain amount because player disconnects and we just got no updates
        private CountdownTimer extrapolationTimer;

        // get references to movement and input
        private PlayerInputHandlerEx5 inputHandler;

        private StatePayload lastProcessedState;
        private int lastProcessedTickForClient = -1;
        private StatePayload lastServerState;
        private NetworkTimeController networkTimeController;


        //Netcode general
        private NetworkTimer networkTimer;

        private CountdownTimer reconciliationTimer;
        private Queue<InputPayload> serverInputQueue;


        // Netcode server specific
        private CircularBuffer<StatePayload> serverStateBuffer;


        //Input exercise 6
        public bool allowInput = true;
        private CountdownTimer stopInputTimer;
        private readonly int stopReceiveInputTime = 4;
        private CountdownTimer invulnerabilityTimer;
        private readonly int invulnerabilityTime = 6;

        private void Awake()
        {
            networkTimer = new NetworkTimer(k_serverTickRate);
            clientStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
            clientInputBuffer = new CircularBuffer<InputPayload>(k_bufferSize);
            serverStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
            serverInputQueue = new Queue<InputPayload>();
            interpolationBuffer = new List<StatePayload>();
            inputHandler = GetComponent<PlayerInputHandlerEx5>();
            carController = GetComponent<CarControllerEx5>();
            networkTimer = new NetworkTimer(k_serverTickRate);
            reconciliationTimer = new CountdownTimer(reconciliationCooldownTime);
            networkTimeController = GetComponent<NetworkTimeController>();
            clientNetworkTransform = GetComponent<ClientNetworkTransform>();
            extrapolationTimer = new CountdownTimer(extrapolationLimit);
            reconciliationTimer.OnTimerStart += () => { extrapolationTimer.Stop(); };
            stopInputTimer = new CountdownTimer(stopReceiveInputTime);
            stopInputTimer.OnTimerStop += () => { AllowReceiveInput(); };
            stopInputTimer.OnTimerStart += () => { PrintTimerStart(); };
            invulnerabilityTimer = new CountdownTimer(invulnerabilityTime);
            invulnerabilityTimer.OnTimerStart += () => { PrintTimerStart(); };


            extrapolationTimer.OnTimerStart += () =>
            {
                reconciliationTimer.Stop();
                SwitchAuthorityMode(AuthorityMode.Server);
            };

            extrapolationTimer.OnTimerStop += () =>
            {
                extrapolationState = default;
                SwitchAuthorityMode(AuthorityMode.Client);
            };
        }

        private void Update()
        {
            networkTimer.Update(Time.deltaTime);
            reconciliationTimer.Tick(Time.deltaTime);
            extrapolationTimer.Tick(Time.deltaTime);
            stopInputTimer.Tick(Time.deltaTime);
            invulnerabilityTimer.Tick(Time.deltaTime);
            //Extrapolate();

            // this manages local cars that are in the past
            if (!IsOwner && Globals.networkingRoleSuperset == NetworkingRole.IsClient)
            {
                // handle remote player position and transforms based on estimated server time - interpolationDelay 100 ms
                if (interpolationBuffer.Count < 2)
                    return;
                var targetRenderTimeMs = networkTimeController.GetRemoteObjectRenderTime();
                while (interpolationBuffer.Count > 2 && targetRenderTimeMs > interpolationBuffer[1].timeStamp)
                    interpolationBuffer.RemoveAt(0);

                var lerpWeight = Mathf.Clamp(
                    Mathf.InverseLerp(
                        (float)interpolationBuffer[0].timeStamp, // from timestamp
                        (float)interpolationBuffer[1].timeStamp, // to timestamp
                        (float)targetRenderTimeMs // current target time
                    ),
                    0.01f,
                    1f
                );

                var fromState = interpolationBuffer[0];
                var toState = interpolationBuffer[1];

                // set remote player transforms
                transform.position = Vector3.Lerp(fromState.position, toState.position, lerpWeight);
                transform.rotation = Quaternion.Slerp(fromState.rotation, toState.rotation, lerpWeight);
            }


            HandleServerEchoPrediction();
        }

        private void FixedUpdate()
        {
            while (networkTimer.ShouldTick())
            {
                HandleClientTick();
                HandleServerTick();
            }

            //Extrapolate();
        }

        private void SwitchAuthorityMode(AuthorityMode mode)
        {
            clientNetworkTransform.authorityMode = mode;
            var shouldSync = mode == AuthorityMode.Client;
            clientNetworkTransform.SyncPositionX = shouldSync;
            clientNetworkTransform.SyncPositionY = shouldSync;
            clientNetworkTransform.SyncPositionZ = shouldSync;
        }

        private void HandleServerTick()
        {
            if (Globals.networkingRoleSuperset != NetworkingRole.IsShard) return;

            //only run if the shard is responsible for this client
            if (Globals.networkingRole != ProxyScript.Instance.GetCorrespondingShardRole(OwnerClientId))
                return;

            var bufferIndex = -1;
            var lastProcessedTick = -1;
            InputPayload inputPayload = default;
            while (serverInputQueue.Count > 0)
            {
                inputPayload = serverInputQueue.Dequeue();

                if (inputPayload.tick <= lastProcessedTick)
                    continue;

                bufferIndex = inputPayload.tick % k_bufferSize;

                var statePayload = ProcessMovement(inputPayload, networkTimer.MinTimeBetweenTicks);
                serverStateBuffer.Add(statePayload, bufferIndex);

                lastProcessedTick = inputPayload.tick;
            }


            if (bufferIndex == -1) return;

            SendServerStateBufferToServerRpc(serverStateBuffer.Get(bufferIndex));
            serverEchoPredictionBuffer.Add(serverStateBuffer.Get(bufferIndex));
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendServerStateBufferToServerRpc(StatePayload statePayload, RpcParams p = default)
        {
            var senderId = p.Receive.SenderClientId;
            var clientId = ProxyScript.Instance.GetCorrespondingClientId(senderId);
            SendServerStateBufferToResponsibleClientRpc(statePayload, RpcTarget.Single(clientId, RpcTargetUse.Temp));
            if (ProxyScript.Instance.ClientIdToRole.ContainsKey(clientId))
            {
                var otherClientId = ProxyScript.Instance.GetOtherClientId(clientId);
                if (NetworkObject.IsNetworkVisibleTo(otherClientId))
                    SendServerStateBufferToOtherClientRpc(statePayload,
                        RpcTarget.Single(otherClientId, RpcTargetUse.Temp));
            }

            if (ProxyScript.Instance.GetOtherShardId(senderId) != ProxyScript.Instance.ErrorClientId)
            {
                var otherShardId = ProxyScript.Instance.GetOtherShardId(senderId);
                SyncCarWithOtherShardClientRpc(statePayload, RpcTarget.Single(otherShardId, RpcTargetUse.Temp));
            }
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void SyncCarWithOtherShardClientRpc(StatePayload statePayload,
            RpcParams rpcParams = default)
        {
            transform.position = statePayload.position;
            transform.rotation = statePayload.rotation;
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void SendServerStateBufferToResponsibleClientRpc(StatePayload statePayload,
            RpcParams rpcParams = default)
        {
            if (statePayload.tick >= lastEchoPredictionBufferTick)
            {
                serverEchoPredictionBuffer.Add(statePayload);
                lastEchoPredictionBufferTick = statePayload.tick;
            }

            lastServerState = statePayload;
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void SendServerStateBufferToOtherClientRpc(StatePayload statePayload, RpcParams rpcParams = default)
        {
            if (IsOwner) return;
            if (lastInterpolationBufferTick < statePayload.tick)
            {
                interpolationBuffer.Add(statePayload);
                lastInterpolationBufferTick = statePayload.tick;
            }
        }

        private void Extrapolate()
        {
            if (IsServer && extrapolationTimer.IsRunning)
            {
                // extrapolate
            }
        }

        private void HandleExtrapolation(StatePayload latest, float latency, InputPayload latestInput)
        {
            // extrapolate as long as client is not lagging like crazy and more than what is being handled by unity
            if (latency < extrapolationLimit && latency > Time.fixedDeltaTime)
            {
                if (extrapolationState.position != default) latest = extrapolationState;
                Debug.Log("were extrapolating with latency: " + latency);
                extrapolationState = ProcessMovement(latestInput, latency);
                extrapolationTimer.Start();
            }
            else
            {
                extrapolationTimer.Stop();
                //reconcile if desired
            }
        }


        private void HandleClientTick()
        {
            if (!allowInput) // so it doesnt continue afterwards with the input from before we stopped
            {
                var currentTick = networkTimer.CurrentTick;
                var bufferIndex = currentTick % k_bufferSize;
                var inputPayload = new InputPayload
                {
                    tick = currentTick,
                    movement = Vector2.zero,
                    look = Vector2.zero,
                    boost_time = 0.0f,
                    break_time = 0.0f,
                    timeStamp = NetworkManager.Singleton.LocalTime.Time,
                    position = transform.position,
                    networkObjectId = NetworkObjectId
                };

                clientInputBuffer.Add(inputPayload, bufferIndex);

                SendToServerRpc(inputPayload);

                // otherwise we apply movement twice for the host, once here and once in HandleServerTick
                if (!IsServer)
                {
                    var statePayload = ProcessMovement(inputPayload, networkTimer.MinTimeBetweenTicks);
                    clientStateBuffer.Add(statePayload, bufferIndex);
                }
            }
            else if (IsOwner)
            {
                var currentTick = networkTimer.CurrentTick;
                var bufferIndex = currentTick % k_bufferSize;
                var inputs = inputHandler.Inputs;

                var inputPayload = new InputPayload
                {
                    tick = currentTick,
                    movement = inputs.movement,
                    look = inputs.look,
                    boost_time = inputs.boost_time,
                    break_time = inputs.break_time,
                    timeStamp = NetworkManager.Singleton.LocalTime.Time,
                    position = transform.position,
                    networkObjectId = NetworkObjectId
                };

                clientInputBuffer.Add(inputPayload, bufferIndex);

                SendToServerRpc(inputPayload);

                // otherwise we apply movement twice for the host, once here and once in HandleServerTick
                if (!IsServer)
                {
                    var statePayload = ProcessMovement(inputPayload, networkTimer.MinTimeBetweenTicks);
                    clientStateBuffer.Add(statePayload, bufferIndex);
                }

                HandleServerReconciliation();
            }
        }

        private void HandleServerEchoPrediction()
        {
            // handle remote player position and transforms based on estimated server time - interpolationDelay 100 ms
            if (serverEchoPredictionBuffer.Count < 2)
                return;
            var targetRenderTimeMs = networkTimeController.GetRemoteObjectRenderTime();
            while (serverEchoPredictionBuffer.Count > 2 && targetRenderTimeMs > serverEchoPredictionBuffer[1].timeStamp)
                serverEchoPredictionBuffer.RemoveAt(0);

            var lerpWeight =
                Mathf.InverseLerp(
                    (float)serverEchoPredictionBuffer[0].timeStamp, // from timestamp
                    (float)serverEchoPredictionBuffer[1].timeStamp, // to timestamp
                    (float)targetRenderTimeMs // current target time
                );

            var fromState = serverEchoPredictionBuffer[0];
            var toState = serverEchoPredictionBuffer[1];

            var positionInPast = Vector3.Lerp(fromState.position, toState.position, lerpWeight);

            var quaternionInPast = Quaternion.Slerp(fromState.rotation, toState.rotation, lerpWeight);

            // Extrapolation: predict position and rotation based on last two states
            var stateDelta = (float)(toState.timeStamp - fromState.timeStamp);
            if (stateDelta < 0.0001f) stateDelta = 0.0001f; // Prevent divide by zero

            var velocity = (toState.position - fromState.position) / stateDelta;
            var smoothingFactor = 7f;
            // Position extrapolation
            var pos = positionInPast + velocity * ExtrapolationFactor();


            //TODO check here if its smooth when the states change
            // also what is time.delta time * smoothing factor? you should interpolate what the current time is, because
            // your time right now is above the old one and below the new one

            // Rotation extrapolation (approximate angular velocity)
            var deltaRot = Quaternion.Inverse(fromState.rotation) * toState.rotation;
            deltaRot.ToAngleAxis(out var angle, out var axis);
            if (axis.sqrMagnitude == 0f) axis = Vector3.up; // fallback if no rotation

            var angularSpeed = angle / stateDelta; // degrees per ms
            var rotation = quaternionInPast * Quaternion.AngleAxis(angularSpeed * ExtrapolationFactor(), axis);

            // Smooth step toward predicted position
            ServerEchoPrediction.transform.position = Vector3.Lerp(
                ServerEchoPrediction.transform.position, pos, Time.deltaTime * smoothingFactor
            );
            ServerEchoPrediction.transform.rotation = Quaternion.Slerp(ServerEchoPrediction.transform.rotation,
                rotation, Time.deltaTime * smoothingFactor);
        }

        private float ExtrapolationFactor()
        {
            var rttMs = networkTimeController.rttEMA * 1000f;

            if (rttMs < 50f)
                return networkTimeController.rttEMA * 10f;
            if (rttMs < 100f)
                return networkTimeController.rttEMA * 5f;
            if (rttMs < 150f)
                return networkTimeController.rttEMA * 4f;
            if (rttMs < 200f)
                return networkTimeController.rttEMA * 4f;

            return networkTimeController.rttEMA;
        }


        private bool ShouldReconcile()
        {
            var isNewServerState = !lastServerState.Equals(default);
            var isLastStateUndefinedOrDifferent =
                lastProcessedState.Equals(default) || !lastProcessedState.Equals(lastServerState);

            return isNewServerState && isLastStateUndefinedOrDifferent && !reconciliationTimer.IsRunning &&
                   !extrapolationTimer.IsRunning;
        }

        public static float DistanceSmoothing(Vector3 a, Vector3 b,
            float maxDistance,
            float minSmooth,
            float maxSmooth)
        {
            // Distance between positions (cheap enough for this use‑case)
            var dist = Vector3.Distance(a, b);

            // Normalize distance 0..1, clamped
            var t = Mathf.Clamp01(dist / maxDistance);

            // Option 1: slightly ease in (quadratic)
            t = t * t; // small dist -> much smaller t, big dist -> near 1

            // Map to smoothing range
            return Mathf.Lerp(minSmooth, maxSmooth, t);
        }


        private void HandleServerReconciliation()
        {
            if (!ShouldReconcile()) return;

            float positionError;
            int bufferIndex;
            StatePayload rewindState = default;
            bufferIndex = lastServerState.tick % k_bufferSize;
            if (bufferIndex - 1 < 0) return; //Not enough information to reconcile

            // this cancels out teleportation
            rewindState =
                IsHost
                    ? serverStateBuffer.Get(bufferIndex - 1)
                    : lastServerState; // Host rpcs execute immediately so we need to use the last server state
            var clientStateAtTick =
                IsHost ? clientStateBuffer.Get(bufferIndex - 1) : clientStateBuffer.Get(bufferIndex);
            //rewindState = lastServerState; // Host rpcs execute immediately so we can use the last server state
            //Debug.Log(
            //             $"RewindState[tick={rewindState.tick}, pos={rewindState.position}, rot={rewindState.rotation}, speed={rewindState.speed}, turn={rewindState.turning}] | " + "\n" +
            //            $"ClientState[tick={clientStateBuffer.Get(bufferIndex).tick}, pos={clientStateBuffer.Get(bufferIndex).position}, rot={clientStateBuffer.Get(bufferIndex).rotation}, speed={clientStateBuffer.Get(bufferIndex).speed}, turn={clientStateBuffer.Get(bufferIndex).turning}]"
            //             );

            positionError = Vector3.Distance(rewindState.position, clientStateBuffer.Get(bufferIndex).position);
            if (positionError > 55 * networkTimer.MinTimeBetweenTicks + 500 * networkTimeController.rttEMA)
                //reconciliationThreshold) //reconciliationThreshold should not  47 * networkTimer.MinTimeBetweenTicks) be enough
            {
                //Debug.Break();
                Debug.Log("reconciling due to position error: ");
                ReconcileState(rewindState);
                reconciliationTimer.Start();
            }

            lastProcessedState = rewindState;
        }

        public void PrintStatePayload(StatePayload payload)
        {
            Debug.Log(
                $"tick: {payload.tick}\n" +
                $"position: {payload.position}\n" +
                $"rotation: {payload.rotation}\n" +
                $"speed: {payload.speed}\n" +
                $"turning: {payload.turning}\n" +
                $"networkObjectId: {payload.networkObjectId}\n" +
                $"timeStamp: {payload.timeStamp}"
            );
        }

        private void ReconcileState(StatePayload rewindState)
        {
            if (!rewindState.Equals(lastServerState))
                return;
            transform.position = rewindState.position;
            transform.rotation = rewindState.rotation;

            carController.maxSpeed = 45;
            carController.rotationSpeed = 90;

            serverEchoPredictionBuffer.Add(rewindState);
            clientStateBuffer.Add(rewindState, rewindState.tick);

            /* somewhere here is a player syncing with client problem


            // Replay all inputs from the rewind state to the current state
            var tickToReplay = lastServerState.tick;

            // to avoid jittering we resimulate on a dummy player and lerp to the dummy player position
            while (tickToReplay < networkTimer.CurrentTick)
            {
                var bufferIndex = tickToReplay % k_bufferSize;
                //Debug.Log("position before replay: " + DummyPlayer.transform.position);
                var statePayload =
                    ProcessMovement(clientInputBuffer.Get(bufferIndex), networkTimer.MinTimeBetweenTicks);
                clientStateBuffer.Add(statePayload, bufferIndex);
                //Debug.Log("position after replay: " + DummyPlayer.transform.position);
                tickToReplay++;
            }
            // player syncing with client problem end




            //transform.position = DummyPlayer.transform.position;
            //transform.rotation = DummyPlayer.transform.rotation;
            /*
             StartCoroutine(LerpTransformOverTime(
                transform,
                transform.position,
                DummyPlayer.transform.position,
                transform.rotation,
                DummyPlayer.transform.rotation,
                0.2f)); // 0.2 seconds
            */
        }

        public static IEnumerator LerpTransformOverTime(
            Transform target,
            Vector3 startPosition, Vector3 endPosition,
            Quaternion startRotation, Quaternion endRotation,
            float duration)
        {
            var time = 0f;
            while (time < duration)
            {
                var t = time / duration;
                target.position = Vector3.Lerp(startPosition, endPosition, t);
                target.rotation = Quaternion.Lerp(startRotation, endRotation, t);
                time += Time.deltaTime;
                yield return null;
            }

            target.position = endPosition;
            target.rotation = endRotation;
        }


        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void SendToServerRpc(InputPayload inputPayload, RpcParams p = default)
        {
            var senderId = p.Receive.SenderClientId;
            var shardId = ProxyScript.Instance.GetCorrespondingShardId(senderId);
            SendToShardClientRpc(inputPayload, RpcTarget.Single(shardId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void SendToShardClientRpc(InputPayload inputPayload, RpcParams rpcParams = default)
        {
            // only enqueue if its a new state
            if (inputPayload.tick <= lastProcessedTickForClient)
                return;

            serverInputQueue.Enqueue(inputPayload);
            lastProcessedTickForClient = inputPayload.tick;
        }


        // this is where the client gets reconciled to
        private StatePayload ProcessMovementDummyPlayer(InputPayload inputPayload, float deltaTime)
        {
            var inputs = new Inputs
            {
                movement = inputPayload.movement,
                look = inputPayload.look,
                boost_time = inputPayload.boost_time,
                break_time = inputPayload.break_time
            };


            return new StatePayload
            {
                tick = inputPayload.tick,
                position = transform.position,
                rotation = transform.rotation,
                speed = carController.m_speed,
                turning = carController.m_turning,
                networkObjectId = inputPayload.networkObjectId
            };
        }

        // server simulates movement with larger deltaTime timer.MinTimeBetweenTicks
        // clients simulate locally with Time.deltaTime
        private StatePayload ProcessMovement(InputPayload inputPayload, float deltaTime)
        {
            var inputs = new Inputs
            {
                movement = inputPayload.movement,
                look = inputPayload.look,
                boost_time = inputPayload.boost_time,
                break_time = inputPayload.break_time
            };

            carController.ApplyInputs(inputs, deltaTime);

            return new StatePayload
            {
                tick = inputPayload.tick,
                position = transform.position,
                rotation = transform.rotation,
                speed = carController.m_speed,
                turning = carController.m_turning,
                networkObjectId = inputPayload.networkObjectId,
                timeStamp = NetworkManager.Singleton.LocalTime.Time
            };
        }

        public void StopReceivingInput()
        {
            if (invulnerabilityTimer.IsRunning) return;
            allowInput = false;
            stopInputTimer.Start();
            invulnerabilityTimer.Start();
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