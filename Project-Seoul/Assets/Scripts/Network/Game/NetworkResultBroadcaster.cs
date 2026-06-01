using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Seoul.Network.Game
{
    public struct ResultEntry : INetworkSerializable, IEquatable<ResultEntry>
    {
        public ulong ClientId;
        public int   Score;
        public int   FinalRank;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ClientId);
            s.SerializeValue(ref Score);
            s.SerializeValue(ref FinalRank);
        }

        public bool Equals(ResultEntry other)
            => ClientId == other.ClientId && Score == other.Score && FinalRank == other.FinalRank;
    }

    public class NetworkResultBroadcaster : NetworkBehaviour
    {
        public static NetworkResultBroadcaster Instance { get; private set; }

        public NetworkList<ResultEntry> Entries;

        private void Awake()
        {
            Instance = this;
            Entries  = new NetworkList<ResultEntry>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkResultBroadcaster] OnNetworkSpawn IsServer={IsServer}");
            if (!IsServer) return;

            var store = SessionScoreStore.Instance;
            if (store == null)
            {
                Debug.LogWarning("[NetworkResultBroadcaster] SessionScoreStore.Instance is null — no scores to publish.");
                return;
            }

            var list = new List<(ulong id, int score)>();
            foreach (var kv in store.All) list.Add((kv.Key, kv.Value));
            list.Sort((a, b) => b.score.CompareTo(a.score));

            for (int i = 0; i < list.Count; i++)
            {
                Entries.Add(new ResultEntry
                {
                    ClientId  = list[i].id,
                    Score     = list[i].score,
                    FinalRank = i + 1
                });
            }

            Debug.Log($"[NetworkResultBroadcaster] Published {Entries.Count} entries.");
        }
    }
}
