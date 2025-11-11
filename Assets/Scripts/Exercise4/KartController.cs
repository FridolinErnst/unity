// questions. is the deltaTime and Processmovement function correct and how is this handled in professional games
// how can we stop the player from teleporting? right now if you spam Q it still teleports. do we need to check multilpe steps?
// i dont know why


//TODO
// zeit syncen
// mach handle extrapolation mal nur auf dem echo bzw client cube und nur client side
// dann schickt der server an clients alles 100 ms in der vergangenheit
// bei time stemps wird gecheckt fuer reconciliation (schauen ob hier zeit oder ticks)
// wenn laenger als 100 ms keine pakete kommen, dann extrapolieren 0.25s
/*change time stuff everywhere to:
// Using long for Unix time in ms
public struct StatePayload : INetworkSerializable
{
    public int tick;
    public Vector3 position;
    public Quaternion rotation;
    public ulong networkObjectId;
    public long timeStamp; // <--- Use long/int64 for timestamps
}

// Setting timestamp
statePayload.timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

 */

using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Utilities;
using Debug = UnityEngine.Debug;

namespace Kart
{
    public struct InputPayload : INetworkSerializable
    {
        public int tick;
        public Vector2 movement;
        public Vector2 look;
        public float boost_time;
        public float break_time;
        public double timeStamp;
        public ulong networkObjectId;
        public Vector3 position;


        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref movement);
            serializer.SerializeValue(ref look);
            serializer.SerializeValue(ref boost_time);
            serializer.SerializeValue(ref break_time);
            serializer.SerializeValue(ref timeStamp);
            serializer.SerializeValue(ref networkObjectId);
            serializer.SerializeValue(ref position);
        }
    }

    public struct StatePayload : INetworkSerializable
    {
        public int tick;
        public Vector3 position;
        public Quaternion rotation;
        public float speed;
        public float turning;
        public ulong networkObjectId;
        public double timeStamp;


        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotation);
            serializer.SerializeValue(ref speed);
            serializer.SerializeValue(ref turning);
            serializer.SerializeValue(ref networkObjectId);
            serializer.SerializeValue(ref timeStamp);
        }
    }


    public class KartController : NetworkBehaviour
    {
        public const float k_serverTickRate = 60f;
        private const int k_bufferSize = 1024;

        [SerializeField] private GameObject clientCube;
        private readonly float extrapolationLimit = 0.5f; // 500ms
        private readonly float reconciliationCooldownTime = 1f;


        //ServerEcho
        private ServerEchoEx4 serverEchoEx4;
        [SerializeField] private GameObject serverCube;
        [SerializeField] private GameObject DummyPlayer;
        private CarControllerEx4 DummyPlayerCarController;
        [SerializeField] private GameObject ServerEchoPrediction;
        private readonly List<StatePayload> serverEchoPredictionBuffer = new();
        private int lastEchoPredictionBufferTick = -1;

        [Header("Netcode")] private readonly float reconciliationThreshold = 13.5f;
        private AIHandlerEx4 aiHandler;
        private CarControllerEx4 carController;
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
        private PlayerInputHandlerEx4 inputHandler;

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


        private void Awake()
        {
            DummyPlayerCarController = DummyPlayer.GetComponent<CarControllerEx4>();
            networkTimer = new NetworkTimer(k_serverTickRate);
            clientStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
            clientInputBuffer = new CircularBuffer<InputPayload>(k_bufferSize);
            serverStateBuffer = new CircularBuffer<StatePayload>(k_bufferSize);
            serverInputQueue = new Queue<InputPayload>();
            interpolationBuffer = new List<StatePayload>();
            inputHandler = GetComponent<PlayerInputHandlerEx4>();
            carController = GetComponent<CarControllerEx4>();
            aiHandler = GetComponent<AIHandlerEx4>();
            networkTimer = new NetworkTimer(k_serverTickRate);
            reconciliationTimer = new CountdownTimer(reconciliationCooldownTime);
            networkTimeController = GetComponent<NetworkTimeController>();
            clientNetworkTransform = GetComponent<ClientNetworkTransform>();
            extrapolationTimer = new CountdownTimer(extrapolationLimit);
            reconciliationTimer.OnTimerStart += () => { extrapolationTimer.Stop(); };

            serverEchoEx4 = GetComponent<ServerEchoEx4>();

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
            //Extrapolate();
            if (!IsOwner && !IsServer)
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
            if (!IsServer) return;
            var bufferIndex = -1;
            InputPayload inputPayload = default;
            while (serverInputQueue.Count > 0)
            {
                inputPayload = serverInputQueue.Dequeue();

                bufferIndex = inputPayload.tick % k_bufferSize;

                var statePayload = ProcessMovement(inputPayload, networkTimer.MinTimeBetweenTicks);

                if (IsOwner)
                {
                    clientStateBuffer.Add(statePayload, bufferIndex);
                    clientCube.transform.position = statePayload.position;
                }

                serverCube.transform.position = statePayload.position;
                serverStateBuffer.Add(statePayload, bufferIndex);
            }

            if (bufferIndex == -1) return;

            SendToClientRpc(serverStateBuffer.Get(bufferIndex));
            serverEchoPredictionBuffer.Add(serverStateBuffer.Get(bufferIndex));
            AddToInterpolationBufferClientRpc(serverStateBuffer.Get(bufferIndex));
            //HandleExtrapolation(serverStateBuffer.Get(bufferIndex), CalculateLatencyInMillis(inputPayload),
            //  inputPayload);
        }


        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void AddToInterpolationBufferClientRpc(StatePayload statePayload)
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


        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendToClientRpc(StatePayload statePayload)
        {
            if (statePayload.tick >= lastEchoPredictionBufferTick)
            {
                serverEchoPredictionBuffer.Add(statePayload);
                lastEchoPredictionBufferTick = statePayload.tick;
            }

            if (!IsOwner) return;
            lastServerState = statePayload;
        }

        private void HandleClientTick()
        {
            if (IsOwner)
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
                    clientCube.transform.position = statePayload.position;
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

            var lerpWeight = Mathf.Clamp(
                Mathf.InverseLerp(
                    (float)serverEchoPredictionBuffer[0].timeStamp, // from timestamp
                    (float)serverEchoPredictionBuffer[1].timeStamp, // to timestamp
                    (float)targetRenderTimeMs // current target time
                ),
                0.01f,
                1f
            );

            var fromState = serverEchoPredictionBuffer[0];
            var toState = serverEchoPredictionBuffer[1];
            /* without prediction
            ServerEchoPrediction.transform.position = Vector3.Lerp(fromState.position, toState.position, lerpWeight);
            ServerEchoPrediction.transform.rotation =
                Quaternion.Slerp(fromState.rotation, toState.rotation, lerpWeight);
            */

            // Extrapolation: predict position and rotation based on last two states
            var stateDelta = (float)(toState.timeStamp - fromState.timeStamp);
            if (stateDelta < 0.0001f) stateDelta = 0.0001f; // Prevent divide by zero

            var velocity = (toState.position - fromState.position) / stateDelta;
            var extrapolateDelta = (float)networkTimeController.EstimatedServerTimeNow + networkTimeController.rttEMA -
                                   (float)fromState.timeStamp;
            var smoothingFactor = 5f;
            // Position extrapolation
            var pos = toState.position + velocity * extrapolateDelta;

            // Rotation extrapolation (approximate angular velocity)
            var deltaRot = Quaternion.Inverse(fromState.rotation) * toState.rotation;
            deltaRot.ToAngleAxis(out var angle, out var axis);
            if (axis.sqrMagnitude == 0f) axis = Vector3.up; // fallback if no rotation

            var angularSpeed = angle / stateDelta; // degrees per ms
            var rotation =
                toState.rotation * Quaternion.AngleAxis(angularSpeed * extrapolateDelta, axis);
            // Smooth step toward predicted position
            ServerEchoPrediction.transform.position = Vector3.Lerp(
                ServerEchoPrediction.transform.position, pos, Time.deltaTime * smoothingFactor
            );
            ServerEchoPrediction.transform.rotation = Quaternion.Slerp(ServerEchoPrediction.transform.rotation,
                rotation, Time.deltaTime * smoothingFactor);
        }

        private bool ShouldReconcile()
        {
            var isNewServerState = !lastServerState.Equals(default);
            var isLastStateUndefinedOrDifferent =
                lastProcessedState.Equals(default) || !lastProcessedState.Equals(lastServerState);

            return isNewServerState && isLastStateUndefinedOrDifferent && !reconciliationTimer.IsRunning &&
                   !extrapolationTimer.IsRunning;
        }


        private void HandleServerReconciliation()
        {
            if (!ShouldReconcile()) return;

            float positionError;
            int bufferIndex;
            StatePayload rewindState = default;
            bufferIndex = lastServerState.tick % k_bufferSize;
            if (bufferIndex - 1 < 0) return; //Not enough information to reconcile

            //TODO

            //call calculate threshhold based on speed with this but i think with timer.MinTimeBetweenTicks and
            // if packages get lost or something take that into account
            // like if last 3 ticks got lost, multiply that by 3 and allow the position to be in a circle around that area
            //character.Move(Quaternion.LookRotation(transform.forward, transform.up) * (new Vector3(0, 0, m_speed * deltaTime)));


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
            if (positionError > 55 * networkTimer.MinTimeBetweenTicks)
                //reconciliationThreshold) //reconciliationThreshold should not  47 * networkTimer.MinTimeBetweenTicks) be enough
            {
                //Debug.Break();
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
            DummyPlayerCarController.maxSpeed = 45;
            DummyPlayerCarController.rotationSpeed = 90;
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


        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendToServerRpc(InputPayload inputPayload)
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

            DummyPlayerCarController.ApplyInputs(inputs, deltaTime);
            Debug.Log("DummyPlayer position after move: " + DummyPlayer.transform.position);
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
    }
}