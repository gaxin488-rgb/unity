using UnityEngine;
using System.Collections.Generic;

// ============================================================
//  Extensions — Common extension methods
// ============================================================
namespace EchoesOfTheLost.Utils
{
    public static class Extensions
    {
        // ── Vector ──────────────────────────────────────────
        /// <summary>Project Vector3 onto XZ plane (ignore Y).</summary>
        public static Vector2 ToFlatVector2(this Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Rotate transform to face target on XZ plane only.</summary>
        public static void LookAtXZ(this Transform t, Vector3 target)
        {
            target.y = t.position.y;
            if (target != t.position)
                t.LookAt(target);
        }

        /// <summary>Returns true if distance is less than threshold (sqrMagnitude, cheaper).</summary>
        public static bool IsWithinDistance(this Vector3 a, Vector3 b, float distance)
            => (a - b).sqrMagnitude <= distance * distance;

        // ── Float ────────────────────────────────────────────
        /// <summary>Remap a float from one range to another.</summary>
        public static float Remap(this float value, float inMin, float inMax, float outMin, float outMax)
            => outMin + (value - inMin) * (outMax - outMin) / (inMax - inMin);

        /// <summary>Clamp01 shorthand.</summary>
        public static float Clamp01(this float value) => Mathf.Clamp01(value);

        // ── Collections ──────────────────────────────────────
        /// <summary>Fisher-Yates shuffle for any IList.</summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>Get random element from list.</summary>
        public static T RandomElement<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[Random.Range(0, list.Count)];
        }

        // ── GameObject ────────────────────────────────────────
        /// <summary>Get or Add component in one call.</summary>
        public static T GetOrAdd<T>(this GameObject go) where T : Component
            => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

        // ── String ───────────────────────────────────────────
        /// <summary>Simple string truncation with ellipsis.</summary>
        public static string Truncate(this string value, int maxLength)
            => value?.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
    }
}
