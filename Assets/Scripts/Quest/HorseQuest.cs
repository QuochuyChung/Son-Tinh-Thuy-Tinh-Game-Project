using System;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The old horse keeper's errand on Sơn Tinh's map: find his bronze bell, get Ngựa Chín Hồng Mao in return.
    // Static like GiftTracker, so leaving the map and coming back keeps the progress (the horse is never given twice).
    public enum HorseQuestState
    {
        NotStarted,   // the keeper has not asked yet
        Accepted,     // asked: the bell is marked on the mountain path
        HasBell,      // the bell is picked up, go back to the keeper
        Completed,    // the horse is given (it stays in the stable)
    }

    public static class HorseQuest
    {
        public static HorseQuestState State { get; private set; }

        public static event Action<HorseQuestState> Changed;

        public static void Set(HorseQuestState state)
        {
            if (state == State) return;
            State = state;
            Changed?.Invoke(state);
        }

        public static void Reset() => State = HorseQuestState.NotStarted;

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            State = HorseQuestState.NotStarted;
            Changed = null;
        }
    }
}
