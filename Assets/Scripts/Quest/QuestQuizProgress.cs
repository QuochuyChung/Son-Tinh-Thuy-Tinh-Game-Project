using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // Mission permission earned from the keeper's quiz. Kept separately from
    // GiftTracker: knowing a mission and owning its gift are different states.
    public static class QuestQuizProgress
    {
        static readonly HashSet<string> unlocked = new();

        public static event Action<GiftItem> Changed;

        public static bool IsUnlocked(GiftItem gift) => gift != null && unlocked.Contains(gift.name);

        public static bool Unlock(GiftItem gift)
        {
            if (gift == null || !unlocked.Add(gift.name)) return false;
            Changed?.Invoke(gift);
            return true;
        }

        public static GiftItem NextGift(GiftQuest quest)
        {
            if (quest == null) return null;
            foreach (GiftItem gift in quest.RequiredGifts)
                if (!GiftTracker.Has(gift)) return gift;
            return null;
        }

        public static void AnnounceLocked(GiftItem gift)
        {
            string name = gift != null ? gift.DisplayName : "sính lễ này";
            GiftTracker.Announce($"<b>{name}</b> chưa được mở khóa\nHãy gặp Người Giữ Nhiệm Vụ và vượt qua 3 câu hỏi trước");
        }

        public static void Reset() => unlocked.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Reset();
            Changed = null;
        }
    }
}
