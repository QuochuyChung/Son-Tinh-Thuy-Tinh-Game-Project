using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.Flow
{
    // Progress that must survive scene loads during one playthrough.
    public static class GameSession
    {
        public static CharacterId? SelectedCharacter { get; set; }

        public static void StartNewGame()
        {
            SelectedCharacter = null;
            GiftTracker.Reset();
            HorseQuest.Reset();
        }

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => SelectedCharacter = null;
    }
}
