using System.Collections.Generic;
using UnityEngine;

namespace KartCup
{
    /// Says a driver's line: picks one of the kind (never the one just said), plays it on the voice channel (the
    /// music dips under it) and tells whoever draws speech bubbles what was said.
    public static class Voices
    {
        static DriverVoices bank;
        static readonly Dictionary<string, int> last = new();

        /// driver, what they said, how long it takes to say
        public static event System.Action<string, string, float> Spoke;

        public static float Say(string driver, string kind)
        {
            if (string.IsNullOrEmpty(driver)) return 0f;
            if (bank == null) bank = Resources.Load<DriverVoices>("DriverVoices");
            if (bank == null) return 0f;
            var pool = new List<DriverVoices.Line>();
            foreach (var l in bank.lines) if (l.driver == driver && l.kind == kind && l.clip != null) pool.Add(l);
            if (pool.Count == 0) return 0f;
            string key = driver + "/" + kind;
            int was = last.TryGetValue(key, out int w) ? w : -1;
            int pick = Random.Range(0, pool.Count);
            if (pool.Count > 1 && pick == was) pick = (pick + 1 + Random.Range(0, pool.Count - 1)) % pool.Count;
            last[key] = pick;
            var line = pool[pick];
            AudioDirector.Instance?.Voice(line.clip);
            Spoke?.Invoke(driver, line.text, line.clip.length);
            return line.clip.length;
        }

        /// The driver the player is racing as (the select screen's choice).
        public static string Player
        {
            get
            {
                var roster = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
                if (roster.Length == 0) return null;
                var def = roster[0].At(CharacterDatabase.SelectedIndex);
                return def != null ? def.driverId : null;
            }
        }
    }
}
