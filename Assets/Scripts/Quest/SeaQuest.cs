using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonTinhThuyTinh.Quest
{
    public enum SeaQuestState
    {
        NotStarted,   // the old fisherman at the harbour has not asked yet: the pirates hold the harbour, the pier to the village is shut
        Accepted,     // asked: the pirates wait on the harbour deck, the chain across the pier stays up until they are beaten
        Cleared,      // the pirates are beaten and the chain is down: the village, the lighthouse and the rest of the bay are open
    }

    // Progress of Thủy Tinh's errand on Map_ThuyTinh (docs/ke-hoach-thuy-tinh-su-kien.md): dealing with the pirates at the harbour, the
    // lamp of the lighthouse, and which boss intros were already seen. Static like HorseQuest, so leaving the map for another scene and
    // coming back keeps the progress.
    public static class SeaQuest
    {
        public static SeaQuestState State { get; private set; }
        public static bool LampLit { get; private set; }
        public static bool GhostsCleared { get; private set; }   // the water ghosts at the foot of the lighthouse are beaten: the lamp can be lit
        public static bool ElderMet { get; private set; }        // the village elder has spoken to the player once

        public static event Action Changed;

        static readonly HashSet<string> introsSeen = new();

        public static void Set(SeaQuestState state)
        {
            if (state == State) return;
            State = state;
            Changed?.Invoke();
        }

        public static void ClearGhosts()
        {
            if (GhostsCleared) return;
            GhostsCleared = true;
            Changed?.Invoke();
        }

        public static void MeetElder()
        {
            if (ElderMet) return;
            ElderMet = true;
            Changed?.Invoke();
        }

        public static void LightLamp()
        {
            if (LampLit) return;
            LampLit = true;
            Changed?.Invoke();
        }

        public static bool IntroSeen(string key) => introsSeen.Contains(key);
        public static void MarkIntroSeen(string key) => introsSeen.Add(key);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            State = SeaQuestState.NotStarted;
            LampLit = false;
            GhostsCleared = false;
            ElderMet = false;
            introsSeen.Clear();
            Changed = null;
        }
    }
}
