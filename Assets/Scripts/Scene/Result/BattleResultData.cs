using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    public struct BattleResultEntry
    {
        public string PlayerName;
        public int Score;
        public ulong ClientId;
        public bool IsCpu;
        public int MachineIndex;
        public int DefeatedEnemies;
        public float DamageTaken;
        public int DeathCount;

        public BattleResultEntry(
            string playerName,
            int score,
            ulong clientId,
            bool isCpu,
            int machineIndex = 0,
            int defeatedEnemies = 0,
            float damageTaken = 0f,
            int deathCount = 0)
        {
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "PLAYER" : playerName;
            Score = score;
            ClientId = clientId;
            IsCpu = isCpu;
            MachineIndex = MachineProfileCatalog.ClampIndex(machineIndex);
            DefeatedEnemies = Mathf.Max(0, defeatedEnemies);
            DamageTaken = Mathf.Max(0f, damageTaken);
            DeathCount = Mathf.Max(0, deathCount);
        }
    }

    public static class BattleResultData
    {
        private const string ResultPayloadKey = "BattleResultData.Payload";
        private const string ResultWasOnlineKey = "BattleResultData.WasOnline";

        private static readonly List<BattleResultEntry> EntriesInternal = new List<BattleResultEntry>(4);

        public static bool HasResult { get; private set; }
        public static bool WasOnline { get; private set; }
        public static IReadOnlyList<BattleResultEntry> Entries => EntriesInternal;

        public static void SetResults(IEnumerable<BattleResultEntry> entries, bool wasOnline)
        {
            EntriesInternal.Clear();

            if (entries != null)
            {
                foreach (BattleResultEntry entry in entries)
                {
                    EntriesInternal.Add(entry);
                }
            }

            WasOnline = wasOnline;
            HasResult = EntriesInternal.Count > 0;

            if (HasResult)
            {
                PlayerPrefs.SetString(ResultPayloadKey, Serialize(EntriesInternal));
                PlayerPrefs.SetInt(ResultWasOnlineKey, WasOnline ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static List<BattleResultEntry> GetRankedEntries()
        {
            List<BattleResultEntry> ranked = new List<BattleResultEntry>(EntriesInternal);
            ranked.Sort((a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                if (scoreCompare != 0) return scoreCompare;
                return a.ClientId.CompareTo(b.ClientId);
            });

            return ranked;
        }

        public static string Serialize(IEnumerable<BattleResultEntry> entries)
        {
            StringBuilder builder = new StringBuilder();

            if (entries != null)
            {
                foreach (BattleResultEntry entry in entries)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(';');
                    }

                    builder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.PlayerName ?? string.Empty)));
                    builder.Append('|');
                    builder.Append(entry.Score);
                    builder.Append('|');
                    builder.Append(entry.ClientId);
                    builder.Append('|');
                    builder.Append(entry.IsCpu ? "1" : "0");
                    builder.Append('|');
                    builder.Append(entry.MachineIndex);
                    builder.Append('|');
                    builder.Append(entry.DefeatedEnemies);
                    builder.Append('|');
                    builder.Append(entry.DamageTaken.ToString(CultureInfo.InvariantCulture));
                    builder.Append('|');
                    builder.Append(entry.DeathCount);
                }
            }

            return builder.ToString();
        }

        public static void DeserializeAndSet(string payload, bool wasOnline)
        {
            List<BattleResultEntry> entries = new List<BattleResultEntry>(4);

            if (!string.IsNullOrWhiteSpace(payload))
            {
                string[] rows = payload.Split(';');
                for (int i = 0; i < rows.Length; i++)
                {
                    string[] columns = rows[i].Split('|');
                    if (columns.Length < 4) continue;

                    string playerName = DecodeName(columns[0]);
                    int.TryParse(columns[1], out int score);
                    ulong.TryParse(columns[2], out ulong clientId);
                    bool isCpu = columns[3] == "1";
                    int machineIndex = 0;
                    int defeatedEnemies = 0;
                    float damageTaken = 0f;
                    int deathCount = 0;

                    if (columns.Length >= 8)
                    {
                        int.TryParse(columns[4], out machineIndex);
                        int.TryParse(columns[5], out defeatedEnemies);
                        float.TryParse(
                            columns[6],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out damageTaken);
                        int.TryParse(columns[7], out deathCount);
                    }
                    else if (columns.Length >= 7)
                    {
                        int.TryParse(columns[4], out defeatedEnemies);
                        float.TryParse(
                            columns[5],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out damageTaken);
                        int.TryParse(columns[6], out deathCount);
                    }

                    entries.Add(new BattleResultEntry(
                        playerName,
                        score,
                        clientId,
                        isCpu,
                        machineIndex,
                        defeatedEnemies,
                        damageTaken,
                        deathCount));
                }
            }

            SetResults(entries, wasOnline);
        }

        public static bool TryRestoreSavedResults()
        {
            string payload = PlayerPrefs.GetString(ResultPayloadKey, string.Empty);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            bool wasOnline = PlayerPrefs.GetInt(ResultWasOnlineKey, 0) == 1;
            DeserializeAndSet(payload, wasOnline);
            return HasResult;
        }

        public static void Clear()
        {
            EntriesInternal.Clear();
            HasResult = false;
            WasOnline = false;
            PlayerPrefs.DeleteKey(ResultPayloadKey);
            PlayerPrefs.DeleteKey(ResultWasOnlineKey);
            PlayerPrefs.Save();
        }

        private static string DecodeName(string encoded)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "PLAYER";
            }
        }
    }
}
