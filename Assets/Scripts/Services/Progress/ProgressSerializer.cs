using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// Progress to and from JSON with Unity's built-in <c>JsonUtility</c> (no extra package). Reading is strict: anything
    /// that is not a complete, current-version save with sane values is refused with a reason, so the caller can fall
    /// back to fresh progress instead of trusting half a file.
    /// <code>{"version":1,"highestUnlocked":"w1_l004","levels":[{"id":"w1_l001","stars":3}, ...],"tutorialsDone":["match3"],"coins":45}</code>
    /// </summary>
    public static class ProgressSerializer
    {
        /// <summary>Bump when the format changes, and add a migration from the older version in <see cref="TryRead"/>.</summary>
        public const int CurrentVersion = 1;

        public static string Write(ProgressData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.version = CurrentVersion;
            return JsonUtility.ToJson(data);
        }

        /// <summary>
        /// Parses <paramref name="json"/>. Returns false with <paramref name="problem"/> set when it is empty, not JSON,
        /// truncated, of another version, or holds invalid entries (empty or repeated ids, stars outside
        /// 1..<see cref="ProgressRules.MaxStars"/>, empty or repeated tutorial keys, a negative coin balance).
        /// </summary>
        public static bool TryRead(string json, out ProgressData data, out string problem)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                problem = "file is empty";
                return false;
            }

            string trimmed = json.Trim();
            // JsonUtility accepts some truncated objects without complaint; a complete save is one closed object.
            if (trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
            {
                problem = "not a complete JSON object";
                return false;
            }

            // Parse over an instance with version 0: JsonUtility keeps field initialisers for missing fields, so a file
            // without "version" would otherwise read as the current version.
            var parsed = new ProgressData { version = 0 };
            try
            {
                JsonUtility.FromJsonOverwrite(trimmed, parsed);
            }
            catch (ArgumentException e)
            {
                problem = "not valid JSON (" + e.Message + ")";
                return false;
            }

            if (parsed.version != CurrentVersion)
            {
                problem = parsed.version <= 0
                    ? "no save version"
                    : $"save version {parsed.version} is not supported (this build reads {CurrentVersion})";
                return false;
            }

            parsed.highestUnlocked ??= string.Empty;
            parsed.levels ??= new List<LevelStars>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in parsed.levels)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                {
                    problem = "a level entry has no id";
                    return false;
                }
                if (!seen.Add(entry.id))
                {
                    problem = $"level {entry.id} is listed twice";
                    return false;
                }
                if (entry.stars < 1 || entry.stars > ProgressRules.MaxStars)
                {
                    problem = $"level {entry.id} has {entry.stars} stars";
                    return false;
                }
            }

            parsed.tutorialsDone ??= new List<string>();
            var tutorials = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in parsed.tutorialsDone)
            {
                if (string.IsNullOrEmpty(key))
                {
                    problem = "a completed tutorial has no key";
                    return false;
                }
                if (!tutorials.Add(key))
                {
                    problem = $"tutorial {key} is listed twice";
                    return false;
                }
            }

            if (parsed.coins < 0)
            {
                problem = $"coin balance is {parsed.coins}";
                return false;
            }

            data = parsed;
            problem = null;
            return true;
        }
    }
}
