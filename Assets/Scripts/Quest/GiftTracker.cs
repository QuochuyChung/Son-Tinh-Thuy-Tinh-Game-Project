using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Gifts collected in this playthrough. Static so progress survives scene loads between branch maps.
    public static class GiftTracker
    {
        static readonly HashSet<GiftItem> collected = new();

        public static event Action<GiftItem> Collected;

        public static int Count => collected.Count;

        public static bool Has(GiftItem gift) => collected.Contains(gift);

        public static bool Collect(GiftItem gift)
        {
            if (gift == null || !collected.Add(gift)) return false;

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
        }
    }
}
