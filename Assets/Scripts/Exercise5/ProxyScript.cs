using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

//TODO
// in allen skripten entweder is owner checken und sonst zu proxy schicken, proxy dann checken von welchem client die nachricht kommt und an den richtigen shard weiterleiten
// alle server rpc lesen client sender id und schicken an richtigen shard
// der code der bei isserver laueft
// jedes mal wenn shard mit simulation fertig sind an alle clients schicken
// not is owner wird zu not is owner or prox or shard
// shards muessen mirroren, also direkt updaten. shard schickt nachm simulieren an proxy, proxy dann an alle anderen mit original absender id, empfaenger checkt ob sender shard ist und selbst shard, wenn ja einfach transport und
//         rotation updaten und return, sonst
//         lassen wir beim client code laufen bzw updaten buffer
// bei shard code: if (NetworkObject.OwnerClientId == map von deinem shard zu der repsonsible client id && !IsOwner ) dann update

namespace Kart
{
    public class ProxyScript : NetworkBehaviour
    {
        public event Action<ulong, NetworkingRole> OnRoleRegistered;
        private readonly HashSet<ulong> _pendingSpawn = new();

        public static ProxyScript Instance { get; private set; }

        private readonly Dictionary<NetworkingRole, ulong> _roleToClientId = new();
        private readonly Dictionary<ulong, NetworkingRole> _clientIdToRole = new();
        public const ulong ErrorClientId = 1000;

        // Expose read-only view
        public IReadOnlyDictionary<NetworkingRole, ulong> RoleToClientId => _roleToClientId;
        public IReadOnlyDictionary<ulong, NetworkingRole> ClientIdToRole => _clientIdToRole;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Optional: keep the first one
                Debug.LogWarning("Duplicate ProxyScript detected, destroying the new one");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Optional: persist across scene loads (host/server recommended)
            DontDestroyOnLoad(gameObject);
        }


        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += cid => _pendingSpawn.Add(cid);
                NetworkManager.OnClientDisconnectCallback += cid => _pendingSpawn.Remove(cid);
                _roleToClientId[Globals.networkingRole] = NetworkManager.Singleton.LocalClientId;
                Debug.Log("server: " + _roleToClientId.Keys + ": " + _roleToClientId.Values);
            }
        }

        public void RegisterRoleServer(NetworkingRole role, ulong senderId)
        {
            if (_clientIdToRole.ContainsKey(senderId)) return;
            _roleToClientId[role] = senderId;
            _clientIdToRole[senderId] = role;
            Debug.Log("roletoclientid count: " + _roleToClientId.Count);
            Debug.Log(string.Join(", ",
                _roleToClientId.Select(kv => $"{kv.Key}={kv.Value}")));
            Debug.Log(string.Join(", ", _clientIdToRole.Select(kv => $"{kv.Key}={kv.Value}")));
            OnRoleRegistered?.Invoke(senderId, role); // notify listeners
            if (role == NetworkingRole.IsClient1 || role == NetworkingRole.IsClient2)
            {
                if (_roleToClientId.ContainsKey(NetworkingRole.IsShard1))
                    RegisterClientsOnShardsClientRpc(role, senderId,
                        RpcTarget.Single(_roleToClientId[NetworkingRole.IsShard1], RpcTargetUse.Temp));
                _pendingSpawn.Remove(senderId);
                if (_roleToClientId.ContainsKey(NetworkingRole.IsShard2))
                    RegisterClientsOnShardsClientRpc(role, senderId,
                        RpcTarget.Single(_roleToClientId[NetworkingRole.IsShard2], RpcTargetUse.Temp));
                _pendingSpawn.Remove(senderId);
            }
        }


        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void RegisterClientsOnShardsClientRpc(NetworkingRole role, ulong clientId,
            RpcParams rpcParams = default)
        {
            RegisterClient(clientId, role);
        }

        public ulong GetCorrespondingShardId(ulong clientId)
        {
            if (_clientIdToRole.TryGetValue(clientId, out var role))
                switch (role)
                {
                    case NetworkingRole.IsClient1:
                        return _roleToClientId[NetworkingRole.IsShard1];
                    case NetworkingRole.IsClient2:
                        return _roleToClientId[NetworkingRole.IsShard2];
                    default:
                        Debug.LogWarning("Client role does not have a corresponding shard.");
                        return 100000;
                }

            Debug.LogWarning("Client ID not found in mapping.");
            return 100000;
        }

        public NetworkingRole GetCorrespondingShardRole(ulong clientId)
        {
            if (_clientIdToRole.TryGetValue(clientId, out var role))
                switch (role)
                {
                    case NetworkingRole.IsClient1:
                        return NetworkingRole.IsShard1;
                    case NetworkingRole.IsClient2:
                        return NetworkingRole.IsShard2;
                    default:
                        Debug.LogWarning("Client role does not have a corresponding shard.");
                        return NetworkingRole.None;
                }

            Debug.LogWarning("Cant return shard role for this client id. " + clientId);
            return NetworkingRole.None;
        }

        public ulong GetCorrespondingClientId(ulong shardId)
        {
            if (_clientIdToRole.TryGetValue(shardId, out var role))
                switch (role)
                {
                    case NetworkingRole.IsShard1:
                        return _roleToClientId[NetworkingRole.IsClient1];
                    case NetworkingRole.IsShard2:
                        return _roleToClientId[NetworkingRole.IsClient2];
                    default:
                        Debug.LogWarning("Shard role does not have a corresponding client.");
                        return 0;
                }

            Debug.LogWarning("Shard ID not found in mapping.");
            return 0;
        }


        public ulong GetOtherClientId(ulong clientId)
        {
            if (_clientIdToRole.TryGetValue(clientId, out var role))
                switch (role)
                {
                    case NetworkingRole.IsClient1:
                        if (_roleToClientId.ContainsKey(NetworkingRole.IsClient2))
                            return _roleToClientId[NetworkingRole.IsClient2];
                        return ErrorClientId;
                    case NetworkingRole.IsClient2:
                        if (_roleToClientId.ContainsKey(NetworkingRole.IsClient1))
                            return _roleToClientId[NetworkingRole.IsClient1];
                        return ErrorClientId;
                    default:
                        Debug.LogWarning("Client role does not have a corresponding other client.");
                        return ErrorClientId;
                }

            Debug.LogWarning("Client ID not found in mapping.");
            return ErrorClientId;
        }

        public ulong GetOtherShardId(ulong shardId)
        {
            if (_clientIdToRole.TryGetValue(shardId, out var role))
                switch (role)
                {
                    case NetworkingRole.IsShard1:
                        if (_roleToClientId.ContainsKey(NetworkingRole.IsShard2))
                            return _roleToClientId[NetworkingRole.IsShard2];
                        return ErrorClientId;
                    case NetworkingRole.IsShard2:
                        if (_roleToClientId.ContainsKey(NetworkingRole.IsShard1))
                            return _roleToClientId[NetworkingRole.IsShard1];
                        return ErrorClientId;
                    default:
                        Debug.LogWarning("Shard role does not have a corresponding other shard.");
                        return ErrorClientId;
                }

            Debug.LogWarning("Shard ID not found in mapping.");
            return ErrorClientId;
        }

        public void RegisterClient(ulong clientId, NetworkingRole role)
        {
            _roleToClientId[role] = clientId;
            _clientIdToRole[clientId] = role;
        }

        public List<ulong> GetAllOtherClients(ulong clientId)
        {
            var otherClients = new List<ulong>();
            foreach (var kv in _clientIdToRole)
                if (kv.Key != clientId)
                    otherClients.Add(kv.Key);
            return otherClients;
        }
    }
}