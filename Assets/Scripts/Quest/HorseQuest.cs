using System;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    // The horse keeper's errand on Sơn Tinh's map (docs/progress.md 9.23): the horse bolted up the mountain trail behind the village, horse
    // thieves hold the clearing, the horse waits on the summit and only the keeper's bell calms it.
    // Static like GiftTracker, so leaving the map and coming back keeps the progress (the horse is never given twice).
    public enum HorseQuestState
    {
        NotStarted,   // the keeper has not asked yet: the horse is gone, the trail is empty
        Accepted,     // asked, the bell is in the pocket: the thieves wait in the clearing, a barricade shuts the way to the summit
        Cleared,      // the thieves are beaten and the barricade is down: climb to the horse and ring the bell
        Calmed,       // the bell was rung, the horse ran home to its stable: tell the keeper
        Completed,    // the horse is given (it leaves the stable)
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
