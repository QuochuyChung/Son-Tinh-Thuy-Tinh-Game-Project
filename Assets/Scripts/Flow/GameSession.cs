using SonTinhThuyTinh.Characters;
using SonTinhThuyTinh.Quest;
using UnityEngine;

namespace SonTinhThuyTinh.Flow
{
    public enum DuelOutcome { None, PlayerWon, BossWon }

    // Progress that must survive scene loads during one playthrough.
    public static class GameSession
    {
        public static CharacterId? SelectedCharacter { get; set; }
        public static DuelOutcome Outcome { get; set; }

        static bool hasEncounterReturn;
        static Vector3 encounterReturnPosition;
        static Quaternion encounterReturnRotation;
        static float encounterRetryAllowedAt;

        public static string EncounterReturnScene { get; private set; } = SceneNames.SonTinhMap;
        public static bool LastEncounterWon { get; private set; }
        public static bool CanStartEncounter => Time.unscaledTime >= encounterRetryAllowedAt;

        public static void BeginEncounter(string returnScene, Vector3 position, Quaternion rotation)
        {
            EncounterReturnScene = string.IsNullOrWhiteSpace(returnScene) ? SceneNames.SonTinhMap : returnScene;
            encounterReturnPosition = position;
            encounterReturnRotation = rotation;
            hasEncounterReturn = true;
            LastEncounterWon = false;
        }

        public static void CompleteEncounter(bool victory)
        {
            LastEncounterWon = victory;
            encounterRetryAllowedAt = Time.unscaledTime + 2.5f;
        }

        public static bool TryConsumeEncounterReturn(string sceneName, out Vector3 position, out Quaternion rotation)
        {
            position = encounterReturnPosition;
            rotation = encounterReturnRotation;
            if (!hasEncounterReturn || sceneName != EncounterReturnScene) return false;

            hasEncounterReturn = false;
            return true;
        }

        public static void StartNewGame()
        {
            SelectedCharacter = null;
            Outcome = DuelOutcome.None;
            GiftTracker.Reset();
            HorseQuest.Reset();
            VoiChinNgaQuest.Reset();
            ResetEncounter();
        }

        // Keeps state clean when Enter Play Mode Options skip the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            SelectedCharacter = null;
            Outcome = DuelOutcome.None;
            ResetEncounter();
        }

        static void ResetEncounter()
        {
            hasEncounterReturn = false;
            EncounterReturnScene = SceneNames.SonTinhMap;
            encounterReturnPosition = Vector3.zero;
            encounterReturnRotation = Quaternion.identity;
            encounterRetryAllowedAt = 0f;
            LastEncounterWon = false;
        }
    }
}
