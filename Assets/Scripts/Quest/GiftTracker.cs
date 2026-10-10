using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Gifts collected in this playthrough. Static so progress survives scene loads between branch maps.
    public static class GiftTracker
    {
        // Store the asset name rather than the ScriptableObject reference. This keeps
        // progress stable when a runtime Resources copy represents the same gift.
        static readonly HashSet<string> collected = new();

        public static event Action<GiftItem> Collected;
        // A one-off line for the same banner (e.g. "Đã nhặt Chuông đồng gia truyền"), for quest items that are not gifts.
        public static event Action<string> Notice;

        public static void Announce(string message) => Notice?.Invoke(message);

        public static int Count => collected.Count;

        public static bool Has(GiftItem gift) => gift != null && collected.Contains(gift.name);

        public static bool Collect(GiftItem gift)
        {
            if (gift == null || !collected.Add(gift.name)) return false;

            Collected?.Invoke(gift);
            return true;
        }

        public static void Reset() => collected.Clear();

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            collected.Clear();
            Collected = null;
            Notice = null;
        }
    }
}
